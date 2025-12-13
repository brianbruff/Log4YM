using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Services;

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
}

public class LogHub : Hub<ILogHubClient>
{
    private readonly ILogger<LogHub> _logger;
    private readonly AntennaGeniusService _antennaGeniusService;
    private readonly PgxlService _pgxlService;
    private readonly FlexRadioService _flexRadioService;
    private readonly TciRadioService _tciRadioService;

    public LogHub(
        ILogger<LogHub> logger,
        AntennaGeniusService antennaGeniusService,
        PgxlService pgxlService,
        FlexRadioService flexRadioService,
        TciRadioService tciRadioService)
    {
        _logger = logger;
        _antennaGeniusService = antennaGeniusService;
        _pgxlService = pgxlService;
        _flexRadioService = flexRadioService;
        _tciRadioService = tciRadioService;
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
    }

    public async Task SelectSpot(SpotSelectedEvent evt)
    {
        _logger.LogDebug("Spot selected: {DxCall} on {Frequency}", evt.DxCall, evt.Frequency);
        await Clients.Others.OnSpotSelected(evt);
    }

    public async Task CommandRotator(RotatorCommandEvent evt)
    {
        _logger.LogDebug("Rotator command: {Azimuth} from {Source}", evt.TargetAzimuth, evt.Source);
        // This would be handled by the rotator service
        // For now, just broadcast it
        await Clients.All.OnRotatorPosition(new RotatorPositionEvent(
            evt.RotatorId,
            evt.TargetAzimuth,
            true,
            evt.TargetAzimuth
        ));
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

        // Try FlexRadio first, then TCI
        if (_flexRadioService.HasRadio(cmd.RadioId))
        {
            await _flexRadioService.ConnectAsync(cmd.RadioId);
        }
        else if (_tciRadioService.HasRadio(cmd.RadioId))
        {
            await _tciRadioService.ConnectAsync(cmd.RadioId);
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

        // Send current radio states
        foreach (var state in _flexRadioService.GetRadioStates())
        {
            await Clients.Caller.OnRadioStateChanged(state);
        }

        foreach (var state in _tciRadioService.GetRadioStates())
        {
            await Clients.Caller.OnRadioStateChanged(state);
        }
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
}
