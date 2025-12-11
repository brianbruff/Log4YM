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
}

public class LogHub : Hub<ILogHubClient>
{
    private readonly ILogger<LogHub> _logger;
    private readonly AntennaGeniusService _antennaGeniusService;

    public LogHub(ILogger<LogHub> logger, AntennaGeniusService antennaGeniusService)
    {
        _logger = logger;
        _antennaGeniusService = antennaGeniusService;
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
}
