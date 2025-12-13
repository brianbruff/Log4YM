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

    // FlexRadio discovery port
    private const int DiscoveryPort = 4992;
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
            var collection = _mongoContext.Database.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios");
            var radios = await collection.Find(_ => true).ToListAsync();

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
                        var packet = BuildDiscoveryPacket(radio);
                        var bytes = Encoding.ASCII.GetBytes(packet);

                        // Broadcast to 255.255.255.255
                        var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);
                        await udpClient.SendAsync(bytes, bytes.Length, endpoint);

                        _lastBroadcast[radio.Id!] = DateTime.UtcNow;

                        _logger.LogDebug("Broadcast discovery for {Name} ({Model})", radio.Name, radio.Model);
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

    private string BuildDiscoveryPacket(SmartUnlinkRadioEntity radio)
    {
        // Build VITA-49 style discovery packet
        // Format: discovery key=value key=value ...
        var sb = new StringBuilder("discovery");

        sb.Append(" protocol_version=3.0.0.2");
        sb.Append($" model={radio.Model}");
        sb.Append($" serial={radio.SerialNumber}");
        sb.Append(" version=3.4.35.141");
        sb.Append($" nickname={radio.Name.Replace(' ', '_')}");

        if (!string.IsNullOrEmpty(radio.Callsign))
        {
            sb.Append($" callsign={radio.Callsign}");
        }

        sb.Append($" ip={radio.IpAddress}");
        sb.Append($" port={DiscoveryPort}");
        sb.Append(" status=Available");
        sb.Append(" inuse_ip=");
        sb.Append(" inuse_host=");
        sb.Append(" max_licensed_version=v3");
        sb.Append(" radio_license_id=00-00-00-00-00-00-00-00");
        sb.Append(" requires_additional_license=0");
        sb.Append(" fpc_mac=");
        sb.Append(" wan_connected=1");
        sb.Append(" licensed_clients=2");
        sb.Append(" available_clients=2");
        sb.Append(" max_panadapters=8");
        sb.Append(" available_panadapters=8");
        sb.Append(" max_slices=8");
        sb.Append(" available_slices=8");

        return sb.ToString();
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

        var collection = _mongoContext.Database.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios");
        await collection.InsertOneAsync(entity);

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

        var collection = _mongoContext.Database.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios");

        var update = Builders<SmartUnlinkRadioEntity>.Update
            .Set(r => r.Name, dto.Name)
            .Set(r => r.IpAddress, dto.IpAddress)
            .Set(r => r.Model, dto.Model)
            .Set(r => r.SerialNumber, dto.SerialNumber)
            .Set(r => r.Callsign, dto.Callsign)
            .Set(r => r.Enabled, dto.Enabled)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await collection.UpdateOneAsync(
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
        var collection = _mongoContext.Database.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios");
        var result = await collection.DeleteOneAsync(r => r.Id == id);

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
        var collection = _mongoContext.Database.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios");

        var update = Builders<SmartUnlinkRadioEntity>.Update
            .Set(r => r.Enabled, enabled)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await collection.UpdateOneAsync(r => r.Id == id, update);

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
