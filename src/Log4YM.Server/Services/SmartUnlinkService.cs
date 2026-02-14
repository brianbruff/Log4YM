using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Log4YM.Contracts.Events;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public class SmartUnlinkService : BackgroundService
{
    private readonly ILogger<SmartUnlinkService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ISmartUnlinkRepository _repository;
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
        ISmartUnlinkRepository repository)
    {
        _logger = logger;
        _hubContext = hubContext;
        _repository = repository;
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
            var radios = await _repository.GetAllAsync();

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

    /// <summary>
    /// Gets all broadcast addresses for local network interfaces.
    /// This is important for VPN scenarios where 255.255.255.255 may not reach the correct network.
    /// </summary>
    private List<(string InterfaceName, IPAddress BroadcastAddress, IPAddress LocalAddress)> GetBroadcastAddresses()
    {
        var broadcasts = new List<(string InterfaceName, IPAddress BroadcastAddress, IPAddress LocalAddress)>();

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Skip loopback and non-operational interfaces
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    networkInterface.OperationalStatus != OperationalStatus.Up)
                    continue;

                var properties = networkInterface.GetIPProperties();

                foreach (var unicast in properties.UnicastAddresses)
                {
                    // Only process IPv4 addresses
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork)
                        continue;

                    // Skip loopback addresses
                    if (IPAddress.IsLoopback(unicast.Address))
                        continue;

                    // Calculate broadcast address: IP | (~mask)
                    var ipBytes = unicast.Address.GetAddressBytes();
                    var maskBytes = unicast.IPv4Mask.GetAddressBytes();
                    var broadcastBytes = new byte[4];

                    for (int i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(ipBytes[i] | (~maskBytes[i] & 0xFF));
                    }

                    var broadcastAddress = new IPAddress(broadcastBytes);
                    broadcasts.Add((networkInterface.Name, broadcastAddress, unicast.Address));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate network interfaces, falling back to default broadcast");
            // Fallback to default broadcast address
            broadcasts.Add(("default", IPAddress.Broadcast, IPAddress.Any));
        }

        // If no interfaces found, use default
        if (broadcasts.Count == 0)
        {
            broadcasts.Add(("default", IPAddress.Broadcast, IPAddress.Any));
        }

        return broadcasts;
    }

    private async Task BroadcastLoopAsync(CancellationToken ct)
    {
        using var udpClient = new UdpClient();
        udpClient.EnableBroadcast = true;

        // Log discovered network interfaces
        var broadcastAddresses = GetBroadcastAddresses();
        _logger.LogInformation("SmartUnlink broadcast loop started. Discovered {Count} network interface(s):", broadcastAddresses.Count);
        foreach (var (interfaceName, broadcastAddress, localAddress) in broadcastAddresses)
        {
            _logger.LogInformation("  {Interface}: {LocalIp} -> {BroadcastIp}", interfaceName, localAddress, broadcastAddress);
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(BroadcastIntervalMs, ct);

                // Refresh broadcast addresses periodically (interfaces may change)
                broadcastAddresses = GetBroadcastAddresses();

                foreach (var radio in _radios.Values.Where(r => r.Enabled))
                {
                    try
                    {
                        var packet = BuildVita49DiscoveryPacket(radio);

                        // Send to each network interface's broadcast address
                        foreach (var (interfaceName, broadcastAddress, _) in broadcastAddresses)
                        {
                            // Broadcast to both ports for maximum compatibility with all SmartSDR versions
                            var endpoint4992 = new IPEndPoint(broadcastAddress, DiscoveryPort);
                            var endpoint4991 = new IPEndPoint(broadcastAddress, StreamingPort);

                            await udpClient.SendAsync(packet, packet.Length, endpoint4992);
                            await udpClient.SendAsync(packet, packet.Length, endpoint4991);
                        }

                        _lastBroadcast[radio.Id!] = DateTime.UtcNow;

                        _logger.LogDebug("Broadcast VITA-49 discovery for {Name} ({Model}) - {Bytes} bytes to {InterfaceCount} interface(s) on ports {Port1}/{Port2}",
                            radio.Name, radio.Model, packet.Length, broadcastAddresses.Count, DiscoveryPort, StreamingPort);
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
                      $"version={radio.Version} " +
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
                      $"licensed_clients=4 " +
                      $"available_clients=4 " +
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
            Version = dto.Version,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.InsertAsync(entity);

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
            entity.Enabled,
            entity.Version
        );

        await _hubContext.Clients.All.OnSmartUnlinkRadioAdded(evt);

        return evt;
    }

    public async Task<SmartUnlinkRadioUpdatedEvent?> UpdateRadioAsync(SmartUnlinkRadioDto dto)
    {
        if (string.IsNullOrEmpty(dto.Id))
            return null;

        _radios.TryGetValue(dto.Id, out var existing);

        var updatedEntity = new SmartUnlinkRadioEntity
        {
            Id = dto.Id,
            Name = dto.Name,
            IpAddress = dto.IpAddress,
            Model = dto.Model,
            SerialNumber = dto.SerialNumber,
            Callsign = dto.Callsign,
            Enabled = dto.Enabled,
            Version = dto.Version,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow
        };

        var success = await _repository.UpdateAsync(updatedEntity);

        if (!success)
            return null;

        // Update in-memory cache
        _radios[dto.Id] = updatedEntity;

        _logger.LogInformation("Updated SmartUnlink radio: {Name} ({Model}) at {Ip}",
            dto.Name, dto.Model, dto.IpAddress);

        var evt = new SmartUnlinkRadioUpdatedEvent(
            dto.Id,
            dto.Name,
            dto.IpAddress,
            dto.Model,
            dto.SerialNumber,
            dto.Callsign,
            dto.Enabled,
            dto.Version
        );

        await _hubContext.Clients.All.OnSmartUnlinkRadioUpdated(evt);

        return evt;
    }

    public async Task<bool> RemoveRadioAsync(string id)
    {
        var success = await _repository.DeleteAsync(id);

        if (!success)
            return false;

        _radios.TryRemove(id, out _);
        _lastBroadcast.TryRemove(id, out _);

        _logger.LogInformation("Removed SmartUnlink radio: {Id}", id);

        await _hubContext.Clients.All.OnSmartUnlinkRadioRemoved(new SmartUnlinkRadioRemovedEvent(id));

        return true;
    }

    public async Task<bool> SetRadioEnabledAsync(string id, bool enabled)
    {
        var success = await _repository.SetEnabledAsync(id, enabled);

        if (!success)
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
                updated.Enabled,
                updated.Version
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
            r.Enabled,
            r.Version
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
    public string Version { get; init; } = "4.1.3.39644";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
