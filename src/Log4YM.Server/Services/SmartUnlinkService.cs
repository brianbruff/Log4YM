using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Log4YM.Contracts.Events;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public class SmartUnlinkService : BackgroundService
{
    private readonly ILogger<SmartUnlinkService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly MongoDbContext _mongoContext;
    private readonly ConcurrentDictionary<string, SmartUnlinkRadioEntity> _radios = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastBroadcast = new();

    // FlexRadio discovery ports - broadcast to both for maximum compatibility
    // UDP 4992 = discovery port (command API port, SmartSDR listens here)
    // UDP 4991 = VITA-49 streaming port (also receives discovery in newer firmware)
    private const int DiscoveryPort = 4992;
    private const int StreamingPort = 4991;
    private const int BroadcastIntervalMs = 3000;

    public SmartUnlinkService(
        ILogger<SmartUnlinkService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        MongoDbContext mongoContext)
    {
        _logger = logger;
        _hubContext = hubContext;
        _mongoContext = mongoContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SmartUnlink service starting...");

        try
        {
            // Load radios from MongoDB
            await LoadRadiosAsync();

            // Start broadcast loop
            await BroadcastLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("SmartUnlink service stopping...");
        }
    }

    private async Task LoadRadiosAsync()
    {
        try
        {
            var radios = await _mongoContext.SmartUnlinkRadios.Find(_ => true).ToListAsync();

            foreach (var radio in radios)
            {
                _radios[radio.Id!] = radio;
                _logger.LogInformation("Loaded SmartUnlink radio: {Name} ({Model}) at {Ip}",
                    radio.Name, radio.Model, radio.IpAddress);
            }

            _logger.LogInformation("Loaded {Count} SmartUnlink radios from database", radios.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SmartUnlink radios from database");
        }
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        _logger.LogInformation("SmartUnlink broadcast loop started on port {Port}", DiscoveryPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(BroadcastIntervalMs, ct);

                foreach (var radio in _radios.Values.Where(r => r.Enabled))
                {
                    try
                    {
                        var packet = BuildVita49DiscoveryPacket(radio);

                        // Broadcast to both ports for maximum compatibility with all SmartSDR versions
                        var endpoint4992 = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                        var endpoint4991 = new IPEndPoint(IPAddress.Broadcast, StreamingPort);

                        await udpClient.SendAsync(packet, packet.Length, endpoint4992);
                        await udpClient.SendAsync(packet, packet.Length, endpoint4991);

                        _lastBroadcast[radio.Id!] = DateTime.UtcNow;

                        _logger.LogDebug("Broadcast VITA-49 discovery for {Name} ({Model}) - {Bytes} bytes to ports {Port1}/{Port2}",
                            radio.Name, radio.Model, packet.Length, DiscoveryPort, StreamingPort);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to broadcast discovery for radio {Name}", radio.Name);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SmartUnlink broadcast loop");
                await Task.Delay(1000, ct);
            }
        }
    }

    private byte _packetCount = 0;

    private byte[] BuildVita49DiscoveryPacket(SmartUnlinkRadioEntity radio)
    {
        // Build VITA-49 discovery packet matching real FlexRadio format
        // Based on captured packets from real FLEX-8400 radio

        // Build payload string with all required fields
        var nickname = radio.Name.Replace(' ', '_');
        var callsign = string.IsNullOrEmpty(radio.Callsign) ? "" : radio.Callsign;

        // Format matching real FlexRadio discovery packets
        var payload = $"discovery_protocol_version=3.1.0.2 " +
                      $"model={radio.Model} " +
                      $"serial={radio.SerialNumber} " +
                      $"version=3.4.35.141 " +
                      $"nickname={nickname} " +
                      $"callsign={callsign} " +
                      $"ip={radio.IpAddress} " +
                      $"port={DiscoveryPort} " +
                      $"status=Available " +
                      $"inuse_ip= " +
                      $"inuse_host= " +
                      $"max_licensed_version=v3 " +
                      $"radio_license_id=00-00-00-00-00-00 " +
                      $"fpc_mac= " +
                      $"wan_connected=0 " +
                      $"licensed_clients=2 " +
                      $"available_clients=2 " +
                      $"max_panadapters=4 " +
                      $"available_panadapters=4 " +
                      $"max_slices=4 " +
                      $"available_slices=4 ";

        var payloadBytes = Encoding.ASCII.GetBytes(payload);

        _logger.LogDebug("SmartUnlink payload: {Payload} ({Length} bytes)", payload, payloadBytes.Length);

        // Pad payload to 4-byte alignment (VITA-49 requirement)
        // Round up to next 4-byte boundary
        var paddedLength = (payloadBytes.Length + 3) & ~3;
        var paddedPayload = new byte[paddedLength];
        Array.Copy(payloadBytes, paddedPayload, payloadBytes.Length);

        // Calculate packet length in 32-bit words (header + stream_id + class_id_h + class_id_l + 3 timestamps + payload)
        var packetLengthWords = 7 + (paddedPayload.Length / 4);

        // Build VITA-49 header matching real FlexRadio format
        // Bits 31-28: Packet Type (0x3 = Extension Command - same as real radio)
        // Bit 27: Class ID present (1)
        // Bit 26: Trailer present (0)
        // Bit 25: Reserved (0)
        // Bit 24: Reserved (0)
        // Bits 23-22: TSI (0x1 = Other)
        // Bits 21-20: TSF (0x1 = Sample Count)
        // Bits 19-16: Packet Count (incrementing)
        // Bits 15-0: Packet Size in 32-bit words
        // Header = 0x38500000 | (packetCount << 16) | packet_length
        uint header = 0x38500000 | ((uint)_packetCount << 16) | (uint)(packetLengthWords & 0xFFFF);
        _packetCount++;

        // Stream ID for Discovery: 0x00000800
        uint streamId = 0x00000800;

        // Class ID for Discovery: 0x00001C2D534CFFFF
        // Split into high (OUI) and low (class code) 32-bit words
        uint classIdHigh = 0x00001C2D;  // FlexRadio OUI
        uint classIdLow = 0x534CFFFF;   // Discovery class code

        // Timestamps (set to 0)
        uint timestampInt = 0;
        uint timestampFracHigh = 0;
        uint timestampFracLow = 0;

        // Build the complete packet
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Write header fields in network byte order (big-endian)
        bw.Write(SwapEndian(header));
        bw.Write(SwapEndian(streamId));
        bw.Write(SwapEndian(classIdHigh));
        bw.Write(SwapEndian(classIdLow));
        bw.Write(SwapEndian(timestampInt));
        bw.Write(SwapEndian(timestampFracHigh));
        bw.Write(SwapEndian(timestampFracLow));

        // Write payload (already in correct byte order)
        bw.Write(paddedPayload);

        var packet = ms.ToArray();

        // Log first 32 bytes of packet for debugging
        _logger.LogDebug("SmartUnlink packet header (first 32 bytes): {Header}",
            BitConverter.ToString(packet, 0, Math.Min(32, packet.Length)));

        return packet;
    }

    private static uint SwapEndian(uint value)
    {
        return ((value & 0x000000FF) << 24) |
               ((value & 0x0000FF00) << 8) |
               ((value & 0x00FF0000) >> 8) |
               ((value & 0xFF000000) >> 24);
    }

    // CRUD Operations

    public async Task<SmartUnlinkRadioAddedEvent> AddRadioAsync(SmartUnlinkRadioDto dto)
    {
        var entity = new SmartUnlinkRadioEntity
        {
            Id = ObjectId.GenerateNewId().ToString(),
            Name = dto.Name,
            IpAddress = dto.IpAddress,
            Model = dto.Model,
            SerialNumber = dto.SerialNumber,
            Callsign = dto.Callsign,
            Enabled = dto.Enabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _mongoContext.SmartUnlinkRadios.InsertOneAsync(entity);

        _radios[entity.Id] = entity;

        _logger.LogInformation("Added SmartUnlink radio: {Name} ({Model}) at {Ip}",
            entity.Name, entity.Model, entity.IpAddress);

        var evt = new SmartUnlinkRadioAddedEvent(
            entity.Id,
            entity.Name,
            entity.IpAddress,
            entity.Model,
            entity.SerialNumber,
            entity.Callsign,
            entity.Enabled
        );

        await _hubContext.Clients.All.OnSmartUnlinkRadioAdded(evt);

        return evt;
    }

    public async Task<SmartUnlinkRadioUpdatedEvent?> UpdateRadioAsync(SmartUnlinkRadioDto dto)
    {
        if (string.IsNullOrEmpty(dto.Id))
            return null;

        var update = Builders<SmartUnlinkRadioEntity>.Update
            .Set(r => r.Name, dto.Name)
            .Set(r => r.IpAddress, dto.IpAddress)
            .Set(r => r.Model, dto.Model)
            .Set(r => r.SerialNumber, dto.SerialNumber)
            .Set(r => r.Callsign, dto.Callsign)
            .Set(r => r.Enabled, dto.Enabled)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await _mongoContext.SmartUnlinkRadios.UpdateOneAsync(
            r => r.Id == dto.Id,
            update
        );

        if (result.ModifiedCount == 0)
            return null;

        // Update in-memory cache
        if (_radios.TryGetValue(dto.Id, out var existing))
        {
            var updated = existing with
            {
                Name = dto.Name,
                IpAddress = dto.IpAddress,
                Model = dto.Model,
                SerialNumber = dto.SerialNumber,
                Callsign = dto.Callsign,
                Enabled = dto.Enabled,
                UpdatedAt = DateTime.UtcNow
            };
            _radios[dto.Id] = updated;
        }

        _logger.LogInformation("Updated SmartUnlink radio: {Name} ({Model}) at {Ip}",
            dto.Name, dto.Model, dto.IpAddress);

        var evt = new SmartUnlinkRadioUpdatedEvent(
            dto.Id,
            dto.Name,
            dto.IpAddress,
            dto.Model,
            dto.SerialNumber,
            dto.Callsign,
            dto.Enabled
        );

        await _hubContext.Clients.All.OnSmartUnlinkRadioUpdated(evt);

        return evt;
    }

    public async Task<bool> RemoveRadioAsync(string id)
    {
        var result = await _mongoContext.SmartUnlinkRadios.DeleteOneAsync(r => r.Id == id);

        if (result.DeletedCount == 0)
            return false;

        _radios.TryRemove(id, out _);
        _lastBroadcast.TryRemove(id, out _);

        _logger.LogInformation("Removed SmartUnlink radio: {Id}", id);

        await _hubContext.Clients.All.OnSmartUnlinkRadioRemoved(new SmartUnlinkRadioRemovedEvent(id));

        return true;
    }

    public async Task<bool> SetRadioEnabledAsync(string id, bool enabled)
    {
        var update = Builders<SmartUnlinkRadioEntity>.Update
            .Set(r => r.Enabled, enabled)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await _mongoContext.SmartUnlinkRadios.UpdateOneAsync(r => r.Id == id, update);

        if (result.ModifiedCount == 0)
            return false;

        if (_radios.TryGetValue(id, out var existing))
        {
            var updated = existing with { Enabled = enabled, UpdatedAt = DateTime.UtcNow };
            _radios[id] = updated;

            _logger.LogInformation("Set SmartUnlink radio {Name} enabled: {Enabled}", updated.Name, enabled);

            var evt = new SmartUnlinkRadioUpdatedEvent(
                updated.Id!,
                updated.Name,
                updated.IpAddress,
                updated.Model,
                updated.SerialNumber,
                updated.Callsign,
                updated.Enabled
            );

            await _hubContext.Clients.All.OnSmartUnlinkRadioUpdated(evt);
        }

        return true;
    }

    public SmartUnlinkStatusEvent GetAllRadios()
    {
        var radios = _radios.Values.Select(r => new SmartUnlinkRadioAddedEvent(
            r.Id!,
            r.Name,
            r.IpAddress,
            r.Model,
            r.SerialNumber,
            r.Callsign,
            r.Enabled
        )).ToList();

        return new SmartUnlinkStatusEvent(radios);
    }

    public SmartUnlinkBroadcastStatusEvent? GetBroadcastStatus(string radioId)
    {
        if (!_radios.TryGetValue(radioId, out var radio))
            return null;

        _lastBroadcast.TryGetValue(radioId, out var lastBroadcast);

        return new SmartUnlinkBroadcastStatusEvent(
            radioId,
            radio.Enabled,
            lastBroadcast == default ? null : lastBroadcast
        );
    }
}

/// <summary>
/// MongoDB entity for SmartUnlink radio
/// </summary>
public record SmartUnlinkRadioEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; init; }

    public string Name { get; init; } = "";
    public string IpAddress { get; init; } = "";
    public string Model { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public string? Callsign { get; init; }
    public bool Enabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
