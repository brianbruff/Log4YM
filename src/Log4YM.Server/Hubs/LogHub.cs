using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Native.Hamlib;

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

    // CW Keyer events
    Task OnCwKeyerStatus(CwKeyerStatusEvent evt);

    // Hamlib configuration events
    Task OnHamlibRigList(HamlibRigListEvent evt);
    Task OnHamlibRigCaps(HamlibRigCapsEvent evt);
    Task OnHamlibSerialPorts(HamlibSerialPortsEvent evt);
    Task OnHamlibConfigLoaded(HamlibConfigLoadedEvent evt);
    Task OnHamlibStatus(HamlibStatusEvent evt);

    // SmartUnlink events
    Task OnSmartUnlinkRadioAdded(SmartUnlinkRadioAddedEvent evt);
    Task OnSmartUnlinkRadioUpdated(SmartUnlinkRadioUpdatedEvent evt);
    Task OnSmartUnlinkRadioRemoved(SmartUnlinkRadioRemovedEvent evt);
    Task OnSmartUnlinkStatus(SmartUnlinkStatusEvent evt);

    // QRZ Sync events
    Task OnQrzSyncProgress(QrzSyncProgressEvent evt);

    // DX Cluster events
    Task OnClusterStatusChanged(ClusterStatusChangedEvent evt);

    // RBN events
    Task OnRbnSpot(RbnSpot spot);
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
    private readonly CwKeyerService _cwKeyerService;
    private readonly ICallsignImageRepository _imageRepository;
    private readonly IDbContext _dbContext;

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
        ISettingsRepository settingsRepository,
        CwKeyerService cwKeyerService,
        ICallsignImageRepository imageRepository,
        IDbContext dbContext)
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
        _cwKeyerService = cwKeyerService;
        _imageRepository = imageRepository;
        _dbContext = dbContext;
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
                    // Normalize station coordinates in case they were stored in microdegree format
                    var stationLat = NormalizeCoordinate(settings.Station.Latitude.Value, isLatitude: true);
                    var stationLon = NormalizeCoordinate(settings.Station.Longitude.Value, isLatitude: false);

                    if (stationLat.HasValue && stationLon.HasValue && stationLat != 0 && stationLon != 0)
                    {
                        bearing = CalculateBearing(stationLat.Value, stationLon.Value, info.Latitude.Value, info.Longitude.Value);
                        distance = CalculateDistance(stationLat.Value, stationLon.Value, info.Latitude.Value, info.Longitude.Value);

                        // Log if coordinates were normalized (helps diagnose issues)
                        if (stationLat != settings.Station.Latitude || stationLon != settings.Station.Longitude)
                        {
                            _logger.LogWarning("Station coordinates were in microdegree format: ({OrigLat}, {OrigLon}) -> ({NormLat}, {NormLon})",
                                settings.Station.Latitude, settings.Station.Longitude, stationLat, stationLon);
                        }
                    }
                }

                var lookedUpEvent = new CallsignLookedUpEvent(
                    Callsign: info.Callsign,
                    Name: BuildFullName(info.FirstName, info.Name),
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

    /// <summary>
    /// Normalize coordinates that may be in microdegree format (degrees * 1,000,000).
    /// </summary>
    private static double? NormalizeCoordinate(double value, bool isLatitude)
    {
        var maxValid = isLatitude ? 90.0 : 180.0;

        // Check if value is already in valid range
        if (Math.Abs(value) <= maxValid)
        {
            return value;
        }

        // Check if value looks like microdegrees (within valid range when divided by 1,000,000)
        var normalized = value / 1_000_000.0;
        if (Math.Abs(normalized) <= maxValid)
        {
            return normalized;
        }

        // Value is invalid even after normalization
        return null;
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(firstName);
        var hasLast = !string.IsNullOrWhiteSpace(lastName);

        if (hasFirst && hasLast)
            return $"{firstName} {lastName}";
        if (hasFirst)
            return firstName;
        if (hasLast)
            return lastName;
        return null;
    }

    private async Task SaveCallsignMapImageAsync(QrzCallsignInfo info)
    {
        if (!_dbContext.IsConnected) return;

        await _imageRepository.UpsertAsync(new CallsignMapImage
        {
            Callsign = info.Callsign,
            ImageUrl = info.ImageUrl,
            Latitude = info.Latitude!.Value,
            Longitude = info.Longitude!.Value,
            Name = BuildFullName(info.FirstName, info.Name),
            Country = info.Country,
            Grid = info.Grid,
            SavedAt = DateTime.UtcNow
        });
        _logger.LogDebug("Saved callsign map image for {Callsign}", info.Callsign);
    }

    /// <summary>
    /// Persist a callsign map image to MongoDB. Called by the frontend after a QSO is logged,
    /// so only actually worked callsigns are saved to the map overlay.
    /// </summary>
    public async Task PersistCallsignMapImage(CallsignMapImage image)
    {
        if (!_dbContext.IsConnected) return;
        if (string.IsNullOrEmpty(image.Callsign)) return;

        await _imageRepository.UpsertAsync(image);
        _logger.LogDebug("Persisted callsign map image for {Callsign} after QSO logged", image.Callsign);
    }

    public async Task SelectSpot(SpotSelectedEvent evt)
    {
        _logger.LogInformation("Spot selected: {DxCall} on {FrequencyMHz} MHz ({Mode})", evt.DxCall, evt.Frequency / 1000.0, evt.Mode ?? "unknown");

        // Broadcast to ALL clients (including caller) so the log entry gets populated
        await Clients.All.OnSpotSelected(evt);

        // Convert spot frequency from kHz to Hz
        var frequencyHz = (long)(evt.Frequency * 1000);

        // Try to tune connected radio (TCI first, then Hamlib)
        var tciRadios = _tciRadioService.GetRadioStates().ToList();
        if (tciRadios.Any())
        {
            var radioId = tciRadios.First().RadioId;
            var tuned = await _tciRadioService.SetFrequencyAsync(radioId, frequencyHz);
            if (tuned)
            {
                _logger.LogInformation("Tuned TCI radio {RadioId} to {FrequencyMHz} MHz", radioId, evt.Frequency / 1000.0);
            }
        }
        else if (_hamlibService.IsConnected)
        {
            // Tune Hamlib radio
            var tuned = await _hamlibService.SetFrequencyAsync(frequencyHz);
            if (tuned)
            {
                _logger.LogInformation("Tuned Hamlib radio to {FrequencyMHz} MHz", evt.Frequency / 1000.0);

                // Also set mode if provided
                if (!string.IsNullOrEmpty(evt.Mode))
                {
                    var modeSet = await _hamlibService.SetModeAsync(evt.Mode);
                    if (modeSet)
                    {
                        _logger.LogInformation("Set Hamlib radio mode to {Mode}", evt.Mode);
                    }
                }
            }
        }
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

        // Try FlexRadio first, then TCI (Hamlib is handled separately via config)
        if (_flexRadioService.HasRadio(cmd.RadioId))
        {
            await _flexRadioService.ConnectAsync(cmd.RadioId);
        }
        else if (_tciRadioService.HasRadio(cmd.RadioId))
        {
            await _tciRadioService.ConnectAsync(cmd.RadioId);
        }
        else if (cmd.RadioId.StartsWith("tci-"))
        {
            // Saved TCI rig not in live discovery — parse host:port from ID and connect directly
            var hostPort = cmd.RadioId["tci-".Length..];
            var parts = hostPort.Split(':');
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : 50001;

            // Load saved name from settings
            var settings = await _settingsRepository.GetAsync();
            var name = settings?.Radio?.Tci?.Name;

            await _tciRadioService.ConnectDirectAsync(host, port, !string.IsNullOrEmpty(name) ? name : null);
        }
        else if (cmd.RadioId == _hamlibService.RadioId)
        {
            _logger.LogDebug("Hamlib radio {RadioId} is already connected", cmd.RadioId);
        }
        else if (cmd.RadioId.StartsWith("hamlib-") && !_hamlibService.IsConnected)
        {
            // Saved Hamlib rig that's disconnected — load config and reconnect
            var config = await _hamlibService.LoadConfigAsync();
            if (config != null)
            {
                _logger.LogInformation("Reconnecting to saved Hamlib rig: {ModelName}", config.ModelName);
                await _hamlibService.ConnectAsync(config);
            }
            else
            {
                _logger.LogWarning("No saved Hamlib config found for {RadioId}", cmd.RadioId);
            }
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
        else if (cmd.RadioId == _hamlibService.RadioId)
        {
            await _hamlibService.DisconnectAsync();
        }
    }

    // ===== Hamlib Configuration Methods =====

    /// <summary>
    /// Get list of all available Hamlib rig models
    /// </summary>
    public async Task GetHamlibRigList()
    {
        _logger.LogInformation("Client requested Hamlib rig list");

        // Try to get rigs regardless of initialization state - this triggers lazy loading
        List<HamlibRigModelInfo> rigs;
        try
        {
            rigs = _hamlibService.GetAvailableRigs()
                .Select(r => new HamlibRigModelInfo(r.ModelId, r.Manufacturer, r.Model, r.Version, r.DisplayName))
                .ToList();

            _logger.LogInformation("Returning {Count} Hamlib rig models (lib loaded: {Loaded}, path: {Path})",
                rigs.Count,
                Native.Hamlib.HamlibNative.IsLoaded,
                Native.Hamlib.HamlibNative.LoadedLibraryPath ?? "none");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Hamlib rig list");
            rigs = new List<HamlibRigModelInfo>();
        }

        // If no rigs found, check for errors
        if (rigs.Count == 0)
        {
            var error = Native.Hamlib.HamlibRigList.InitError;
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogWarning("Hamlib initialization error: {Error}", error);
            }
        }

        await Clients.Caller.OnHamlibRigList(new HamlibRigListEvent(rigs));
    }

    /// <summary>
    /// Get capabilities for a specific Hamlib rig model
    /// </summary>
    public async Task GetHamlibRigCaps(int modelId)
    {
        _logger.LogDebug("Client requested Hamlib rig caps for model {ModelId}", modelId);

        var caps = _hamlibService.GetRigCapabilities(modelId);
        var capsDto = new HamlibRigCapabilities(
            caps.CanGetFreq,
            caps.CanGetMode,
            caps.CanGetVfo,
            caps.CanGetPtt,
            caps.CanGetPower,
            caps.CanGetRit,
            caps.CanGetXit,
            caps.CanGetKeySpeed,
            caps.CanSendMorse,
            caps.DefaultDataBits,
            caps.DefaultStopBits,
            caps.IsNetworkOnly,
            caps.SupportsSerial,
            caps.SupportsNetwork
        );

        await Clients.Caller.OnHamlibRigCaps(new HamlibRigCapsEvent(modelId, capsDto));
    }

    /// <summary>
    /// Get list of available serial ports
    /// </summary>
    public async Task GetHamlibSerialPorts()
    {
        _logger.LogDebug("Client requested serial ports list");

        var ports = _hamlibService.GetSerialPorts();
        await Clients.Caller.OnHamlibSerialPorts(new HamlibSerialPortsEvent(ports));
    }

    /// <summary>
    /// Get saved Hamlib configuration
    /// </summary>
    public async Task GetHamlibConfig()
    {
        _logger.LogDebug("Client requested Hamlib config");

        var config = await _hamlibService.LoadConfigAsync();
        HamlibRigConfigDto? configDto = null;

        if (config != null)
        {
            configDto = new HamlibRigConfigDto(
                config.ModelId,
                config.ModelName,
                (Contracts.Events.HamlibConnectionType)(int)config.ConnectionType,
                config.SerialPort,
                config.BaudRate,
                (Contracts.Events.HamlibDataBits)(int)config.DataBits,
                (Contracts.Events.HamlibStopBits)(int)config.StopBits,
                (Contracts.Events.HamlibFlowControl)(int)config.FlowControl,
                (Contracts.Events.HamlibParity)(int)config.Parity,
                config.Hostname,
                config.NetworkPort,
                (Contracts.Events.HamlibPttType)(int)config.PttType,
                config.PttPort,
                config.GetFrequency,
                config.GetMode,
                config.GetVfo,
                config.GetPtt,
                config.GetPower,
                config.GetRit,
                config.GetXit,
                config.GetKeySpeed,
                config.PollIntervalMs
            );
        }

        await Clients.Caller.OnHamlibConfigLoaded(new HamlibConfigLoadedEvent(configDto));
    }

    /// <summary>
    /// Get Hamlib initialization status
    /// </summary>
    public async Task GetHamlibStatus()
    {
        _logger.LogDebug("Client requested Hamlib status");

        await Clients.Caller.OnHamlibStatus(new HamlibStatusEvent(
            _hamlibService.IsInitialized,
            _hamlibService.IsConnected,
            _hamlibService.RadioId,
            null
        ));
    }

    /// <summary>
    /// Connect to a Hamlib rig with full configuration
    /// </summary>
    public async Task ConnectHamlibRig(HamlibRigConfigDto configDto)
    {
        _logger.LogInformation("Connecting to Hamlib rig: {ModelName}", configDto.ModelName);

        var config = new HamlibRigConfig
        {
            ModelId = configDto.ModelId,
            ModelName = configDto.ModelName,
            ConnectionType = (Native.Hamlib.HamlibConnectionType)(int)configDto.ConnectionType,
            SerialPort = configDto.SerialPort,
            BaudRate = configDto.BaudRate,
            DataBits = (Native.Hamlib.HamlibDataBits)(int)configDto.DataBits,
            StopBits = (Native.Hamlib.HamlibStopBits)(int)configDto.StopBits,
            FlowControl = (Native.Hamlib.HamlibFlowControl)(int)configDto.FlowControl,
            Parity = (Native.Hamlib.HamlibParity)(int)configDto.Parity,
            Hostname = configDto.Hostname,
            NetworkPort = configDto.NetworkPort,
            PttType = (Native.Hamlib.HamlibPttType)(int)configDto.PttType,
            PttPort = configDto.PttPort,
            GetFrequency = configDto.GetFrequency,
            GetMode = configDto.GetMode,
            GetVfo = configDto.GetVfo,
            GetPtt = configDto.GetPtt,
            GetPower = configDto.GetPower,
            GetRit = configDto.GetRit,
            GetXit = configDto.GetXit,
            GetKeySpeed = configDto.GetKeySpeed,
            PollIntervalMs = configDto.PollIntervalMs
        };

        try
        {
            await _hamlibService.ConnectAsync(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect Hamlib rig");
            await Clients.Caller.OnHamlibStatus(new HamlibStatusEvent(
                _hamlibService.IsInitialized,
                false,
                null,
                ex.Message
            ));
        }
    }

    /// <summary>
    /// Save Hamlib rig configuration without connecting.
    /// The rig will appear in the saved list but won't be connected.
    /// </summary>
    public async Task SaveHamlibConfig(HamlibRigConfigDto configDto)
    {
        _logger.LogInformation("Saving Hamlib config (no connect): {ModelName}", configDto.ModelName);

        var config = new HamlibRigConfig
        {
            ModelId = configDto.ModelId,
            ModelName = configDto.ModelName,
            ConnectionType = (Native.Hamlib.HamlibConnectionType)(int)configDto.ConnectionType,
            SerialPort = configDto.SerialPort,
            BaudRate = configDto.BaudRate,
            DataBits = (Native.Hamlib.HamlibDataBits)(int)configDto.DataBits,
            StopBits = (Native.Hamlib.HamlibStopBits)(int)configDto.StopBits,
            FlowControl = (Native.Hamlib.HamlibFlowControl)(int)configDto.FlowControl,
            Parity = (Native.Hamlib.HamlibParity)(int)configDto.Parity,
            Hostname = configDto.Hostname,
            NetworkPort = configDto.NetworkPort,
            PttType = (Native.Hamlib.HamlibPttType)(int)configDto.PttType,
            PttPort = configDto.PttPort,
            GetFrequency = configDto.GetFrequency,
            GetMode = configDto.GetMode,
            GetVfo = configDto.GetVfo,
            GetPtt = configDto.GetPtt,
            GetPower = configDto.GetPower,
            GetRit = configDto.GetRit,
            GetXit = configDto.GetXit,
            GetKeySpeed = configDto.GetKeySpeed,
            PollIntervalMs = configDto.PollIntervalMs
        };

        await _hamlibService.SaveConfigOnlyAsync(config);
        await RequestRadioStatus();
    }

    /// <summary>
    /// Disconnect from the Hamlib rig
    /// </summary>
    public async Task DisconnectHamlibRig()
    {
        _logger.LogInformation("Disconnecting from Hamlib rig");
        await _hamlibService.DisconnectAsync();
    }

    /// <summary>
    /// Delete saved Hamlib configuration
    /// </summary>
    public async Task DeleteHamlibConfig()
    {
        _logger.LogInformation("Deleting saved Hamlib configuration");
        await _hamlibService.DeleteConfigAsync();
        
        // Request updated radio status to reflect the removal
        await RequestRadioStatus();
    }

    /// <summary>
    /// Delete saved TCI configuration
    /// </summary>
    public async Task DeleteTciConfig()
    {
        _logger.LogInformation("Deleting saved TCI configuration");

        // Load settings to determine which TCI radio to remove
        var settings = await _settingsRepository.GetAsync();
        var tciSettings = settings?.Radio?.Tci;

        // Remove TCI radio from discovered radios if it exists
        if (tciSettings != null && !string.IsNullOrEmpty(tciSettings.Host))
        {
            var radioId = $"tci-{tciSettings.Host}:{tciSettings.Port}";
            await _tciRadioService.RemoveRadioAsync(radioId);
        }

        // Clear TCI settings to defaults
        settings ??= new Log4YM.Contracts.Models.UserSettings();
        settings.Radio.Tci = new Log4YM.Contracts.Models.TciSettings();
        settings.Radio.ActiveRigType = null;
        settings.Radio.AutoReconnect = false;

        await _settingsRepository.UpsertAsync(settings);

        // Request updated radio status to reflect the removal
        await RequestRadioStatus();
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

        foreach (var radio in await _tciRadioService.GetDiscoveredRadiosAsync())
        {
            await Clients.Caller.OnRadioDiscovered(radio);
        }

        foreach (var radio in await _hamlibService.GetDiscoveredRadiosAsync())
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

        // Send current connection states so UI reflects actual connection status
        foreach (var connState in _tciRadioService.GetConnectionStates())
        {
            await Clients.Caller.OnRadioConnectionStateChanged(connState);
        }
    }

    // CW Keyer methods

    public async Task SendCwKey(SendCwKeyCommand cmd)
    {
        _logger.LogInformation("Sending CW message for radio {RadioId}: {Message}", cmd.RadioId, cmd.Message);
        await _cwKeyerService.SendCwAsync(cmd.RadioId, cmd.Message, cmd.SpeedWpm);
    }

    public async Task StopCwKey(StopCwKeyCommand cmd)
    {
        _logger.LogInformation("Stopping CW keying for radio {RadioId}", cmd.RadioId);
        await _cwKeyerService.StopCwAsync(cmd.RadioId);
    }

    public async Task SetCwSpeed(SetCwSpeedCommand cmd)
    {
        _logger.LogInformation("Setting CW speed for radio {RadioId}: {Wpm} WPM", cmd.RadioId, cmd.SpeedWpm);
        await _cwKeyerService.SetSpeedAsync(cmd.RadioId, cmd.SpeedWpm);
    }

    public async Task RequestCwKeyerStatus(string radioId)
    {
        _logger.LogDebug("Client requested CW keyer status for radio {RadioId}", radioId);
        var status = _cwKeyerService.GetStatus(radioId);
        if (status != null)
        {
            await Clients.Caller.OnCwKeyerStatus(status);
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

    public static async Task BroadcastClusterStatusChanged(this IHubContext<LogHub, ILogHubClient> hub, ClusterStatusChangedEvent evt)
    {
        await hub.Clients.All.OnClusterStatusChanged(evt);
    }
}
