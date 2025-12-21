using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Services;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Hubs;

public interface ILogHubClient
{
    Task OnCallsignFocused(CallsignFocusedEvent evt);
    Task OnCallsignLookedUp(CallsignLookedUpEvent evt);
    Task OnQsoLogged(QsoLoggedEvent evt);
    Task OnSpotReceived(SpotReceivedEvent evt);
    Task OnSpotSelected(SpotSelectedEvent evt);
    Task OnRotatorPosition(RotatorPositionEvent evt);
    Task OnRigStatus(RigStatusEvent evt);
    Task OnStationLocation(StationLocationEvent evt);

    // Antenna Genius events
    Task OnAntennaGeniusDiscovered(AntennaGeniusDiscoveredEvent evt);
    Task OnAntennaGeniusDisconnected(AntennaGeniusDisconnectedEvent evt);
    Task OnAntennaGeniusStatus(AntennaGeniusStatusEvent evt);
    Task OnAntennaGeniusPortChanged(AntennaGeniusPortChangedEvent evt);

    // PGXL Amplifier events
    Task OnPgxlDiscovered(PgxlDiscoveredEvent evt);
    Task OnPgxlDisconnected(PgxlDisconnectedEvent evt);
    Task OnPgxlStatus(PgxlStatusEvent evt);

    // Radio CAT Control events
    Task OnRadioDiscovered(RadioDiscoveredEvent evt);
    Task OnRadioRemoved(RadioRemovedEvent evt);
    Task OnRadioConnectionStateChanged(RadioConnectionStateChangedEvent evt);
    Task OnRadioStateChanged(RadioStateChangedEvent evt);
    Task OnRadioSlicesUpdated(RadioSlicesUpdatedEvent evt);

    // SmartUnlink events
    Task OnSmartUnlinkRadioAdded(SmartUnlinkRadioAddedEvent evt);
    Task OnSmartUnlinkRadioUpdated(SmartUnlinkRadioUpdatedEvent evt);
    Task OnSmartUnlinkRadioRemoved(SmartUnlinkRadioRemovedEvent evt);
    Task OnSmartUnlinkStatus(SmartUnlinkStatusEvent evt);

    // QRZ Sync events
    Task OnQrzSyncProgress(QrzSyncProgressEvent evt);
}

public class LogHub : Hub<ILogHubClient>
{
    private readonly ILogger<LogHub> _logger;
    private readonly AntennaGeniusService _antennaGeniusService;
    private readonly PgxlService _pgxlService;
    private readonly FlexRadioService _flexRadioService;
    private readonly TciRadioService _tciRadioService;
    private readonly HamlibService _hamlibService;
    private readonly SmartUnlinkService _smartUnlinkService;
    private readonly RotatorService _rotatorService;
    private readonly IQrzService _qrzService;
    private readonly ISettingsRepository _settingsRepository;

    public LogHub(
        ILogger<LogHub> logger,
        AntennaGeniusService antennaGeniusService,
        PgxlService pgxlService,
        FlexRadioService flexRadioService,
        TciRadioService tciRadioService,
        HamlibService hamlibService,
        SmartUnlinkService smartUnlinkService,
        RotatorService rotatorService,
        IQrzService qrzService,
        ISettingsRepository settingsRepository)
    {
        _logger = logger;
        _antennaGeniusService = antennaGeniusService;
        _pgxlService = pgxlService;
        _flexRadioService = flexRadioService;
        _tciRadioService = tciRadioService;
        _hamlibService = hamlibService;
        _smartUnlinkService = smartUnlinkService;
        _rotatorService = rotatorService;
        _qrzService = qrzService;
        _settingsRepository = settingsRepository;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    // Client-to-server methods (called by frontend)

    public async Task FocusCallsign(CallsignFocusedEvent evt)
    {
        _logger.LogDebug("Callsign focused: {Callsign} from {Source}", evt.Callsign, evt.Source);
        await Clients.Others.OnCallsignFocused(evt);

        // Perform QRZ lookup
        try
        {
            var info = await _qrzService.LookupCallsignAsync(evt.Callsign);
            if (info != null)
            {
                // Calculate bearing from station location
                double? bearing = null;
                double? distance = null;

                var settings = await _settingsRepository.GetAsync();
                if (settings?.Station != null && info.Latitude.HasValue && info.Longitude.HasValue
                    && settings.Station.Latitude.HasValue && settings.Station.Longitude.HasValue)
                {
                    var stationLat = settings.Station.Latitude.Value;
                    var stationLon = settings.Station.Longitude.Value;
                    if (stationLat != 0 && stationLon != 0)
                    {
                        bearing = CalculateBearing(stationLat, stationLon, info.Latitude.Value, info.Longitude.Value);
                        distance = CalculateDistance(stationLat, stationLon, info.Latitude.Value, info.Longitude.Value);
                    }
                }

                var lookedUpEvent = new CallsignLookedUpEvent(
                    Callsign: info.Callsign,
                    Name: info.Name ?? info.FirstName,
                    Grid: info.Grid,
                    Latitude: info.Latitude,
                    Longitude: info.Longitude,
                    Country: info.Country,
                    Dxcc: info.Dxcc,
                    CqZone: info.CqZone,
                    ItuZone: info.ItuZone,
                    State: info.State,
                    ImageUrl: info.ImageUrl,
                    Bearing: bearing,
                    Distance: distance
                );

                _logger.LogDebug("Callsign looked up: {Callsign} -> {Name}, {Country}, Bearing: {Bearing}°",
                    info.Callsign, info.Name, info.Country, bearing?.ToString("F0") ?? "N/A");

                await Clients.All.OnCallsignLookedUp(lookedUpEvent);
            }
            else
            {
                // Send empty lookup result to clear loading state
                await Clients.Caller.OnCallsignLookedUp(new CallsignLookedUpEvent(
                    Callsign: evt.Callsign, Name: null, Grid: null, Latitude: null, Longitude: null,
                    Country: null, Dxcc: null, CqZone: null, ItuZone: null, State: null, ImageUrl: null));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to lookup callsign {Callsign}", evt.Callsign);
            // Send empty result to clear loading state
            await Clients.Caller.OnCallsignLookedUp(new CallsignLookedUpEvent(
                Callsign: evt.Callsign, Name: null, Grid: null, Latitude: null, Longitude: null,
                Country: null, Dxcc: null, CqZone: null, ItuZone: null, State: null, ImageUrl: null));
        }
    }

    private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = ToRadians(lon2 - lon1);
        var lat1Rad = ToRadians(lat1);
        var lat2Rad = ToRadians(lat2);

        var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x);
        return (ToDegrees(bearing) + 360) % 360;
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;

    public async Task SelectSpot(SpotSelectedEvent evt)
    {
        _logger.LogDebug("Spot selected: {DxCall} on {Frequency}", evt.DxCall, evt.Frequency);
        await Clients.Others.OnSpotSelected(evt);
    }

    public async Task CommandRotator(RotatorCommandEvent evt)
    {
        _logger.LogInformation("Rotator command: {Azimuth}° from {Source}", evt.TargetAzimuth, evt.Source);
        await _rotatorService.SetPositionAsync(evt.TargetAzimuth);
    }

    public async Task StopRotator()
    {
        _logger.LogInformation("Rotator stop command");
        await _rotatorService.StopAsync();
    }

    public async Task RequestRotatorStatus()
    {
        _logger.LogDebug("Client requested rotator status");
        var status = _rotatorService.GetCurrentStatus();
        await Clients.Caller.OnRotatorPosition(status);
    }

    // Antenna Genius methods

    public async Task SelectAntenna(SelectAntennaCommand cmd)
    {
        _logger.LogInformation("Selecting antenna {AntennaId} for port {PortId} on device {Serial}",
            cmd.AntennaId, cmd.PortId, cmd.DeviceSerial);

        await _antennaGeniusService.SelectAntennaAsync(cmd.DeviceSerial, cmd.PortId, cmd.AntennaId);
    }

    public async Task RequestAntennaGeniusStatus()
    {
        _logger.LogDebug("Client requested Antenna Genius status");

        foreach (var status in _antennaGeniusService.GetAllDeviceStatuses())
        {
            await Clients.Caller.OnAntennaGeniusStatus(status);
        }
    }

    // PGXL Amplifier methods

    public async Task SetPgxlOperate(SetPgxlOperateCommand cmd)
    {
        _logger.LogInformation("Setting PGXL {Serial} to OPERATE", cmd.Serial);
        await _pgxlService.SetOperateAsync(cmd.Serial);
    }

    public async Task SetPgxlStandby(SetPgxlStandbyCommand cmd)
    {
        _logger.LogInformation("Setting PGXL {Serial} to STANDBY", cmd.Serial);
        await _pgxlService.SetStandbyAsync(cmd.Serial);
    }

    public async Task RequestPgxlStatus()
    {
        _logger.LogDebug("Client requested PGXL status");

        foreach (var status in _pgxlService.GetAllStatuses())
        {
            await Clients.Caller.OnPgxlStatus(status);
        }
    }

    public async Task DisablePgxlFlexRadioPairing(DisablePgxlFlexRadioPairingCommand cmd)
    {
        _logger.LogInformation("Disabling FlexRadio pairing for PGXL {Serial} slice {Slice}", cmd.Serial, cmd.Slice);
        await _pgxlService.DisableFlexRadioPairingAsync(cmd.Serial, cmd.Slice);
    }

    // Radio CAT Control methods

    public async Task StartRadioDiscovery(StartRadioDiscoveryCommand cmd)
    {
        _logger.LogInformation("Starting radio discovery for {Type}", cmd.Type);

        if (cmd.Type == RadioType.FlexRadio)
        {
            await _flexRadioService.StartDiscoveryAsync();
        }
        else if (cmd.Type == RadioType.Tci)
        {
            await _tciRadioService.StartDiscoveryAsync();
        }
    }

    public async Task StopRadioDiscovery(StopRadioDiscoveryCommand cmd)
    {
        _logger.LogInformation("Stopping radio discovery for {Type}", cmd.Type);

        if (cmd.Type == RadioType.FlexRadio)
        {
            await _flexRadioService.StopDiscoveryAsync();
        }
        else if (cmd.Type == RadioType.Tci)
        {
            await _tciRadioService.StopDiscoveryAsync();
        }
    }

    public async Task ConnectRadio(ConnectRadioCommand cmd)
    {
        _logger.LogInformation("Connecting to radio {RadioId}", cmd.RadioId);

        // Try FlexRadio first, then TCI, then Hamlib
        if (_flexRadioService.HasRadio(cmd.RadioId))
        {
            await _flexRadioService.ConnectAsync(cmd.RadioId);
        }
        else if (_tciRadioService.HasRadio(cmd.RadioId))
        {
            await _tciRadioService.ConnectAsync(cmd.RadioId);
        }
        else if (_hamlibService.HasRadio(cmd.RadioId))
        {
            // Hamlib radios are already connected when added
            _logger.LogDebug("Hamlib radio {RadioId} is already connected", cmd.RadioId);
        }
        else
        {
            _logger.LogWarning("Radio {RadioId} not found", cmd.RadioId);
        }
    }

    public async Task DisconnectRadio(DisconnectRadioCommand cmd)
    {
        _logger.LogInformation("Disconnecting from radio {RadioId}", cmd.RadioId);

        if (_flexRadioService.HasRadio(cmd.RadioId))
        {
            await _flexRadioService.DisconnectAsync(cmd.RadioId);
        }
        else if (_tciRadioService.HasRadio(cmd.RadioId))
        {
            await _tciRadioService.DisconnectAsync(cmd.RadioId);
        }
        else if (_hamlibService.HasRadio(cmd.RadioId))
        {
            await _hamlibService.DisconnectAsync(cmd.RadioId);
        }
    }

    /// <summary>
    /// Connect to a rigctld instance (Hamlib daemon)
    /// </summary>
    public async Task ConnectHamlib(string host, int port = 4532, string? name = null)
    {
        _logger.LogInformation("Connecting to rigctld at {Host}:{Port}", host, port);
        await _hamlibService.ConnectAsync(host, port, name);
    }

    /// <summary>
    /// Disconnect from a rigctld instance
    /// </summary>
    public async Task DisconnectHamlib(string radioId)
    {
        _logger.LogInformation("Disconnecting from rigctld {RadioId}", radioId);
        await _hamlibService.DisconnectAsync(radioId);
    }

    /// <summary>
    /// Connect directly to a TCI server without discovery
    /// </summary>
    public async Task ConnectTci(string host, int port = 50001, string? name = null)
    {
        _logger.LogInformation("Connecting to TCI at {Host}:{Port}", host, port);
        await _tciRadioService.ConnectDirectAsync(host, port, name);
    }

    /// <summary>
    /// Disconnect from a TCI server
    /// </summary>
    public async Task DisconnectTci(string radioId)
    {
        _logger.LogInformation("Disconnecting from TCI {RadioId}", radioId);
        await _tciRadioService.DisconnectAsync(radioId);
    }

    public async Task SelectRadioSlice(SelectRadioSliceCommand cmd)
    {
        _logger.LogInformation("Selecting slice {SliceId} on radio {RadioId}", cmd.SliceId, cmd.RadioId);
        await _flexRadioService.SelectSliceAsync(cmd.RadioId, cmd.SliceId);
    }

    public async Task SelectRadioInstance(SelectRadioInstanceCommand cmd)
    {
        _logger.LogInformation("Selecting instance {Instance} on radio {RadioId}", cmd.Instance, cmd.RadioId);
        await _tciRadioService.SelectInstanceAsync(cmd.RadioId, cmd.Instance);
    }

    public async Task RequestRadioStatus()
    {
        _logger.LogDebug("Client requested radio status");

        // Send all discovered radios
        foreach (var radio in _flexRadioService.GetDiscoveredRadios())
        {
            await Clients.Caller.OnRadioDiscovered(radio);
        }

        foreach (var radio in _tciRadioService.GetDiscoveredRadios())
        {
            await Clients.Caller.OnRadioDiscovered(radio);
        }

        foreach (var radio in _hamlibService.GetDiscoveredRadios())
        {
            await Clients.Caller.OnRadioDiscovered(radio);
        }

        // Send current radio states
        foreach (var state in _flexRadioService.GetRadioStates())
        {
            await Clients.Caller.OnRadioStateChanged(state);
        }

        foreach (var state in _tciRadioService.GetRadioStates())
        {
            await Clients.Caller.OnRadioStateChanged(state);
        }

        foreach (var state in _hamlibService.GetRadioStates())
        {
            await Clients.Caller.OnRadioStateChanged(state);
        }
    }

    // SmartUnlink methods

    public async Task AddSmartUnlinkRadio(SmartUnlinkRadioDto dto)
    {
        _logger.LogInformation("Adding SmartUnlink radio: {Name} ({Model}) at {Ip}",
            dto.Name, dto.Model, dto.IpAddress);

        await _smartUnlinkService.AddRadioAsync(dto);
    }

    public async Task UpdateSmartUnlinkRadio(SmartUnlinkRadioDto dto)
    {
        _logger.LogInformation("Updating SmartUnlink radio: {Id} - {Name}", dto.Id, dto.Name);

        await _smartUnlinkService.UpdateRadioAsync(dto);
    }

    public async Task RemoveSmartUnlinkRadio(string id)
    {
        _logger.LogInformation("Removing SmartUnlink radio: {Id}", id);

        await _smartUnlinkService.RemoveRadioAsync(id);
    }

    public async Task SetSmartUnlinkRadioEnabled(string id, bool enabled)
    {
        _logger.LogInformation("Setting SmartUnlink radio {Id} enabled: {Enabled}", id, enabled);

        await _smartUnlinkService.SetRadioEnabledAsync(id, enabled);
    }

    public async Task RequestSmartUnlinkStatus()
    {
        _logger.LogDebug("Client requested SmartUnlink status");

        var status = _smartUnlinkService.GetAllRadios();
        await Clients.Caller.OnSmartUnlinkStatus(status);
    }
}

// Extension method for broadcasting events from services
public static class LogHubExtensions
{
    public static async Task BroadcastSpot(this IHubContext<LogHub, ILogHubClient> hub, SpotReceivedEvent evt)
    {
        await hub.Clients.All.OnSpotReceived(evt);
    }

    public static async Task BroadcastQso(this IHubContext<LogHub, ILogHubClient> hub, QsoLoggedEvent evt)
    {
        await hub.Clients.All.OnQsoLogged(evt);
    }

    public static async Task BroadcastCallsignLookup(this IHubContext<LogHub, ILogHubClient> hub, CallsignLookedUpEvent evt)
    {
        await hub.Clients.All.OnCallsignLookedUp(evt);
    }

    public static async Task BroadcastRotatorPosition(this IHubContext<LogHub, ILogHubClient> hub, RotatorPositionEvent evt)
    {
        await hub.Clients.All.OnRotatorPosition(evt);
    }

    // Radio CAT Control extensions
    public static async Task BroadcastRadioDiscovered(this IHubContext<LogHub, ILogHubClient> hub, RadioDiscoveredEvent evt)
    {
        await hub.Clients.All.OnRadioDiscovered(evt);
    }

    public static async Task BroadcastRadioRemoved(this IHubContext<LogHub, ILogHubClient> hub, RadioRemovedEvent evt)
    {
        await hub.Clients.All.OnRadioRemoved(evt);
    }

    public static async Task BroadcastRadioConnectionStateChanged(this IHubContext<LogHub, ILogHubClient> hub, RadioConnectionStateChangedEvent evt)
    {
        await hub.Clients.All.OnRadioConnectionStateChanged(evt);
    }

    public static async Task BroadcastRadioStateChanged(this IHubContext<LogHub, ILogHubClient> hub, RadioStateChangedEvent evt)
    {
        await hub.Clients.All.OnRadioStateChanged(evt);
    }

    public static async Task BroadcastRadioSlicesUpdated(this IHubContext<LogHub, ILogHubClient> hub, RadioSlicesUpdatedEvent evt)
    {
        await hub.Clients.All.OnRadioSlicesUpdated(evt);
    }

    public static async Task BroadcastQrzSyncProgress(this IHubContext<LogHub, ILogHubClient> hub, QrzSyncProgressEvent evt)
    {
        await hub.Clients.All.OnQrzSyncProgress(evt);
    }
}
