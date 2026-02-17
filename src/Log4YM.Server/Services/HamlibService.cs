using System.IO.Ports;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;
using Log4YM.Server.Native.Hamlib;

namespace Log4YM.Server.Services;

/// <summary>
/// Service for direct Hamlib rig control via native library
/// Supports all Hamlib-compatible rigs with full configuration options
/// </summary>
public class HamlibService : BackgroundService
{
    private readonly ILogger<HamlibService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;

    private HamlibRig? _rig;
    private HamlibRigConfig? _config;
    private string? _radioId;
    private bool _initialized;

    // State tracking
    private long _currentFrequencyHz;
    private string _currentMode = "";
    private int _currentPassband;
    private bool _isTransmitting;
    private string? _currentVfo;
    private double? _currentPower;
    private int? _currentRit;
    private int? _currentXit;
    private int? _currentKeySpeed;
    private int _consecutiveErrors;
    private const int MaxConsecutiveErrors = 3;

    public HamlibService(
        ILogger<HamlibService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hamlib service starting...");

        // Initialize Hamlib library
        try
        {
            HamlibRig.Initialize();
            _initialized = true;
            _logger.LogInformation("Hamlib library initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Hamlib library - native library may not be available");
            return;
        }

        // Self-heal: fix any radio_configs docs with null _id from a previous bug
        await FixNullIdsAsync();

        // Migrate old hamlib_config from settings collection to radio_configs
        await MigrateOldHamlibConfigAsync();

        // Try to load and auto-connect saved config if autoReconnect is enabled
        await TryAutoConnectAsync();

        // Poll connected rig periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_rig?.IsOpen == true && _config != null)
            {
                await PollRigStateAsync();
            }
            await Task.Delay(_config?.PollIntervalMs ?? 250, stoppingToken);
        }
    }

    private async Task TryAutoConnectAsync()
    {
        try
        {
            // Check unified autoReconnect flag
            using var scope = _scopeFactory.CreateScope();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepository.GetAsync();
            var radioSettings = settings?.Radio;

            // Only auto-connect if autoReconnect is enabled AND activeRigType is hamlib
            _logger.LogInformation("Hamlib auto-connect check: autoReconnect={AutoReconnect}, activeRigType={ActiveRigType}",
                radioSettings?.AutoReconnect, radioSettings?.ActiveRigType);

            if (radioSettings is not { AutoReconnect: true, ActiveRigType: "hamlib" })
            {
                _logger.LogInformation("Hamlib auto-reconnect not enabled - skipping");
                return;
            }

            // Load saved Hamlib config
            var savedConfig = await LoadConfigAsync();
            if (savedConfig == null)
            {
                _logger.LogInformation("No saved Hamlib config found - cannot auto-reconnect");
                return;
            }

            _logger.LogInformation("Auto-reconnecting to Hamlib rig {ModelName} (model {ModelId})",
                savedConfig.ModelName, savedConfig.ModelId);
            await ConnectAsync(savedConfig);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-connect to Hamlib rig");
        }
    }

    /// <summary>
    /// Self-heal: fix radio_configs docs with _id: null from a previous serialization bug
    /// </summary>
    private async Task FixNullIdsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRadioConfigRepository>();
            await repo.FixNullIdsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fix null IDs in radio_configs");
        }
    }

    /// <summary>
    /// One-time migration: move old hamlib_config from settings collection to radio_configs
    /// </summary>
    private async Task MigrateOldHamlibConfigAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRadioConfigRepository>();
            var migrated = await repo.MigrateOldHamlibConfigAsync();
            if (migrated)
            {
                _logger.LogInformation("Migrated old Hamlib config from settings collection to radio_configs");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to migrate old Hamlib config");
        }
    }

    /// <summary>
    /// Check if Hamlib library is available
    /// </summary>
    public bool IsInitialized => _initialized;

    /// <summary>
    /// Check if a rig is connected
    /// </summary>
    public bool IsConnected => _rig?.IsOpen ?? false;

    /// <summary>
    /// Get the current radio ID
    /// </summary>
    public string? RadioId => _radioId;

    /// <summary>
    /// Get list of all available rig models
    /// Note: This triggers lazy initialization of Hamlib library
    /// </summary>
    public List<RigModelInfo> GetAvailableRigs()
    {
        // HamlibRigList.GetModels() handles its own initialization
        return HamlibRigList.GetModels();
    }

    /// <summary>
    /// Get capabilities for a specific rig model
    /// </summary>
    public RigCapabilities GetRigCapabilities(int modelId)
    {
        if (!_initialized) return new RigCapabilities { SupportsSerial = true, SupportsNetwork = false };

        // Look up model info to pass manufacturer/model names for better detection
        var modelInfo = GetAvailableRigs().FirstOrDefault(r => r.ModelId == modelId);
        return RigCapabilities.GetForModel(modelId, modelInfo?.Manufacturer, modelInfo?.Model);
    }

    /// <summary>
    /// Get list of available serial ports
    /// </summary>
    public List<string> GetSerialPorts()
    {
        try
        {
            return SerialPort.GetPortNames().OrderBy(p => p).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate serial ports");
            return new List<string>();
        }
    }

    /// <summary>
    /// Connect to a rig with the specified configuration
    /// </summary>
    public async Task ConnectAsync(HamlibRigConfig config)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Hamlib library not initialized");
        }

        // Disconnect existing connection if any
        if (_rig != null)
        {
            await DisconnectAsync();
        }

        _config = config;
        _radioId = $"hamlib-{config.ModelId}";

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(_radioId, RadioConnectionState.Connecting));

        try
        {
            // Create rig instance
            _logger.LogInformation("Creating Hamlib rig instance for model {ModelId} ({ModelName})",
                config.ModelId, config.ModelName);

            _rig = HamlibRig.Create(config.ModelId, _logger);
            if (_rig == null)
            {
                throw new InvalidOperationException($"Failed to initialize rig model {config.ModelId}");
            }

            // Configure based on connection type
            if (config.ConnectionType == Native.Hamlib.HamlibConnectionType.Serial)
            {
                if (string.IsNullOrEmpty(config.SerialPort))
                {
                    throw new InvalidOperationException("Serial port not specified");
                }

                _logger.LogInformation("Configuring serial: Port={Port}, Baud={Baud}, DataBits={DataBits}, StopBits={StopBits}, Flow={Flow}, Parity={Parity}",
                    config.SerialPort, config.BaudRate, config.DataBits, config.StopBits, config.FlowControl, config.Parity);

                _rig.ConfigureSerial(
                    config.SerialPort,
                    config.BaudRate,
                    (int)config.DataBits,
                    (int)config.StopBits,
                    config.GetSerialHandshake(),
                    config.GetSerialParity());

                // Configure PTT
                if (config.PttType != Native.Hamlib.HamlibPttType.None)
                {
                    _logger.LogInformation("Configuring PTT: Type={PttType}, Port={PttPort}",
                        config.GetPttTypeString(), config.PttPort ?? "(same as rig)");
                    _rig.ConfigurePtt(config.GetPttTypeString(), config.PttPort);
                }
            }
            else // Network
            {
                if (string.IsNullOrEmpty(config.Hostname))
                {
                    throw new InvalidOperationException("Hostname not specified");
                }

                _logger.LogInformation("Configuring network: Host={Host}, Port={Port}",
                    config.Hostname, config.NetworkPort);
                _rig.ConfigureNetwork(config.Hostname, config.NetworkPort);
            }

            // Open connection
            _logger.LogInformation("Opening rig connection...");
            _rig.Open(); // Now throws on failure with detailed error

            _logger.LogInformation("Connected to Hamlib rig: {ModelName}", config.ModelName);

            // Save config to MongoDB
            await SaveConfigAsync(config);

            // Broadcast discovery and connection state
            await _hubContext.BroadcastRadioDiscovered(new RadioDiscoveredEvent(
                _radioId,
                RadioType.Hamlib,
                config.ModelName,
                config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? config.Hostname ?? "" : config.SerialPort ?? "",
                config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? config.NetworkPort : 0,
                null,
                null
            ));

            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_radioId, RadioConnectionState.Connected));

            // Get initial state
            await PollRigStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Hamlib rig");

            _rig?.Dispose();
            _rig = null;

            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_radioId!, RadioConnectionState.Error, ex.Message));

            throw;
        }
    }

    /// <summary>
    /// Disconnect from the current rig
    /// </summary>
    public async Task DisconnectAsync()
    {
        if (_rig == null || _radioId == null) return;

        var radioId = _radioId;

        _rig.Close();
        _rig.Dispose();
        _rig = null;
        _radioId = null;
        // Keep _config alive so GetDiscoveredRadiosAsync() still returns the saved rig

        // Reset state
        _currentFrequencyHz = 0;
        _currentMode = "";
        _isTransmitting = false;

        _logger.LogInformation("Disconnected from Hamlib rig");

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Disconnected));
    }

    /// <summary>
    /// Set the rig frequency in Hz
    /// </summary>
    public async Task<bool> SetFrequencyAsync(long frequencyHz)
    {
        if (_rig == null || !_rig.IsOpen)
        {
            _logger.LogWarning("Cannot set frequency: Hamlib rig not connected");
            return false;
        }

        var result = _rig.SetFrequency(frequencyHz);
        if (result)
        {
            _currentFrequencyHz = frequencyHz;
            _logger.LogDebug("Hamlib frequency set to {FrequencyHz} Hz", frequencyHz);
            // Broadcast state change immediately so UI updates
            await BroadcastStateAsync();
        }
        return result;
    }

    /// <summary>
    /// Set the rig mode
    /// </summary>
    public async Task<bool> SetModeAsync(string mode)
    {
        if (_rig == null || !_rig.IsOpen)
        {
            _logger.LogWarning("Cannot set mode: Hamlib rig not connected");
            return false;
        }

        var result = _rig.SetMode(mode);
        if (result)
        {
            _currentMode = mode;
            _logger.LogDebug("Hamlib mode set to {Mode}", mode);
            // Broadcast state change immediately so UI updates
            await BroadcastStateAsync();
        }
        return result;
    }

    /// <summary>
    /// Send CW message via Hamlib
    /// </summary>
    public async Task<bool> SendCwAsync(string radioId, string message, int speedWpm)
    {
        // TODO: Implement Hamlib CW keying using rig_send_morse
        // For now, return false to indicate not implemented
        _logger.LogWarning("Hamlib CW keying not yet implemented");
        return await Task.FromResult(false);
    }

    /// <summary>
    /// Set CW speed via Hamlib
    /// </summary>
    public async Task<bool> SetCwSpeedAsync(string radioId, int speedWpm)
    {
        // TODO: Implement Hamlib CW speed setting
        // For now, return false to indicate not implemented
        _logger.LogWarning("Hamlib CW speed setting not yet implemented");
        return await Task.FromResult(false);
    }

    /// <summary>
    /// Get current saved configuration from the radio_configs collection
    /// </summary>
    public async Task<HamlibRigConfig?> LoadConfigAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRadioConfigRepository>();
            var configs = await repo.GetByTypeAsync("hamlib");
            var entity = configs.FirstOrDefault();
            return entity != null ? MapToHamlibConfig(entity) : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Hamlib config from database");
            return null;
        }
    }

    /// <summary>
    /// Save configuration to the radio_configs collection
    /// </summary>
    private async Task SaveConfigAsync(HamlibRigConfig config)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRadioConfigRepository>();
            var entity = MapFromHamlibConfig(config);
            await repo.UpsertByRadioIdAsync(entity);
            _logger.LogInformation("Saved Hamlib config: {ModelName} ({ModelId})", config.ModelName, config.ModelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Hamlib config to database");
        }
    }

    /// <summary>
    /// Save configuration without connecting to the rig.
    /// The rig will appear in GetDiscoveredRadiosAsync() but won't be connected.
    /// </summary>
    public async Task SaveConfigOnlyAsync(HamlibRigConfig config)
    {
        _config = config;
        await SaveConfigAsync(config);
    }

    /// <summary>
    /// Delete saved configuration from the radio_configs collection
    /// </summary>
    public async Task DeleteConfigAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRadioConfigRepository>();
            var configs = await repo.GetByTypeAsync("hamlib");
            foreach (var config in configs)
            {
                await repo.DeleteByRadioIdAsync(config.RadioId);
            }
            _config = null;
            _logger.LogInformation("Deleted Hamlib config from database");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Hamlib config from database");
        }
    }

    /// <summary>
    /// Get discovered radios — reads all hamlib configs from the repo.
    /// Falls back to in-memory config if DB is unreachable.
    /// </summary>
    public async Task<IEnumerable<RadioDiscoveredEvent>> GetDiscoveredRadiosAsync()
    {
        var radios = new List<RadioDiscoveredEvent>();

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRadioConfigRepository>();
            var configs = await repo.GetByTypeAsync("hamlib");

            foreach (var entity in configs)
            {
                var isNetwork = entity.ConnectionType == "Network";
                radios.Add(new RadioDiscoveredEvent(
                    entity.RadioId,
                    RadioType.Hamlib,
                    entity.DisplayName,
                    isNetwork ? entity.Hostname ?? "" : entity.SerialPort ?? "",
                    isNetwork ? entity.NetworkPort ?? 0 : 0,
                    null,
                    null
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Hamlib configs from database, falling back to in-memory");

            // In-memory fallback
            if (_config != null)
            {
                var radioId = _radioId ?? $"hamlib-{_config.ModelId}";
                radios.Add(new RadioDiscoveredEvent(
                    radioId,
                    RadioType.Hamlib,
                    _config.ModelName,
                    _config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? _config.Hostname ?? "" : _config.SerialPort ?? "",
                    _config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? _config.NetworkPort : 0,
                    null,
                    null
                ));
            }
        }

        return radios;
    }

    /// <summary>
    /// Map a HamlibRigConfig to a RadioConfigEntity for storage
    /// </summary>
    public static RadioConfigEntity MapFromHamlibConfig(HamlibRigConfig config)
    {
        return new RadioConfigEntity
        {
            RadioId = $"hamlib-{config.ModelId}",
            RadioType = "hamlib",
            DisplayName = config.ModelName,
            HamlibModelId = config.ModelId,
            HamlibModelName = config.ModelName,
            ConnectionType = config.ConnectionType.ToString(),
            SerialPort = config.SerialPort,
            BaudRate = config.BaudRate,
            DataBits = (int)config.DataBits,
            StopBits = (int)config.StopBits,
            FlowControl = config.FlowControl.ToString(),
            Parity = config.Parity.ToString(),
            Hostname = config.Hostname,
            NetworkPort = config.NetworkPort,
            PttType = config.PttType.ToString(),
            PttPort = config.PttPort,
            GetFrequency = config.GetFrequency,
            GetMode = config.GetMode,
            GetVfo = config.GetVfo,
            GetPtt = config.GetPtt,
            GetPower = config.GetPower,
            GetRit = config.GetRit,
            GetXit = config.GetXit,
            GetKeySpeed = config.GetKeySpeed,
            PollIntervalMs = config.PollIntervalMs,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Map a RadioConfigEntity back to a HamlibRigConfig
    /// </summary>
    public static HamlibRigConfig MapToHamlibConfig(RadioConfigEntity entity)
    {
        return new HamlibRigConfig
        {
            ModelId = entity.HamlibModelId ?? 0,
            ModelName = entity.HamlibModelName ?? entity.DisplayName,
            ConnectionType = Enum.TryParse<Native.Hamlib.HamlibConnectionType>(entity.ConnectionType, out var ct) ? ct : Native.Hamlib.HamlibConnectionType.Serial,
            SerialPort = entity.SerialPort,
            BaudRate = entity.BaudRate ?? 9600,
            DataBits = entity.DataBits.HasValue ? (Native.Hamlib.HamlibDataBits)entity.DataBits.Value : Native.Hamlib.HamlibDataBits.Eight,
            StopBits = entity.StopBits.HasValue ? (Native.Hamlib.HamlibStopBits)entity.StopBits.Value : Native.Hamlib.HamlibStopBits.One,
            FlowControl = Enum.TryParse<Native.Hamlib.HamlibFlowControl>(entity.FlowControl, out var fc) ? fc : Native.Hamlib.HamlibFlowControl.None,
            Parity = Enum.TryParse<Native.Hamlib.HamlibParity>(entity.Parity, out var par) ? par : Native.Hamlib.HamlibParity.None,
            Hostname = entity.Hostname,
            NetworkPort = entity.NetworkPort ?? 4532,
            PttType = Enum.TryParse<Native.Hamlib.HamlibPttType>(entity.PttType, out var ptt) ? ptt : Native.Hamlib.HamlibPttType.Rig,
            PttPort = entity.PttPort,
            GetFrequency = entity.GetFrequency ?? true,
            GetMode = entity.GetMode ?? true,
            GetVfo = entity.GetVfo ?? true,
            GetPtt = entity.GetPtt ?? true,
            GetPower = entity.GetPower ?? false,
            GetRit = entity.GetRit ?? false,
            GetXit = entity.GetXit ?? false,
            GetKeySpeed = entity.GetKeySpeed ?? false,
            PollIntervalMs = entity.PollIntervalMs ?? 250,
        };
    }

    /// <summary>
    /// Get current radio states
    /// </summary>
    public IEnumerable<RadioStateChangedEvent> GetRadioStates()
    {
        if (_radioId == null || _rig?.IsOpen != true) yield break;

        yield return new RadioStateChangedEvent(
            _radioId,
            _currentFrequencyHz,
            _currentMode,
            _isTransmitting,
            BandHelper.GetBand(_currentFrequencyHz),
            null
        );
    }

    /// <summary>
    /// Poll rig for current state
    /// </summary>
    private async Task PollRigStateAsync()
    {
        if (_rig == null || !_rig.IsOpen || _config == null || _radioId == null) return;

        try
        {
            var stateChanged = false;

            // Get frequency
            if (_config.GetFrequency)
            {
                var freq = _rig.GetFrequency();
                if (freq.HasValue)
                {
                    var freqHz = (long)freq.Value;
                    if (freqHz != _currentFrequencyHz)
                    {
                        _currentFrequencyHz = freqHz;
                        stateChanged = true;
                    }
                }
            }

            // Get mode
            if (_config.GetMode)
            {
                var modeInfo = _rig.GetMode();
                if (modeInfo.HasValue)
                {
                    var (mode, passband) = modeInfo.Value;
                    if (mode != _currentMode || passband != _currentPassband)
                    {
                        _currentMode = mode;
                        _currentPassband = passband;
                        stateChanged = true;
                    }
                }
            }

            // Get VFO
            if (_config.GetVfo)
            {
                var vfo = _rig.GetVfo();
                if (vfo != _currentVfo)
                {
                    _currentVfo = vfo;
                    // VFO changes don't trigger broadcast, but track internally
                }
            }

            // Get PTT
            if (_config.GetPtt)
            {
                var ptt = _rig.GetPtt();
                if (ptt.HasValue && ptt.Value != _isTransmitting)
                {
                    _isTransmitting = ptt.Value;
                    stateChanged = true;
                }
            }

            // Get Power (not broadcast, but could be used internally)
            if (_config.GetPower && _currentFrequencyHz > 0)
            {
                var mode = HamlibNative.rig_parse_mode(_currentMode);
                _currentPower = _rig.GetPower(_currentFrequencyHz, mode);
            }

            // Get RIT
            if (_config.GetRit)
            {
                _currentRit = _rig.GetRit();
            }

            // Get XIT
            if (_config.GetXit)
            {
                _currentXit = _rig.GetXit();
            }

            // Get Key Speed
            if (_config.GetKeySpeed)
            {
                _currentKeySpeed = _rig.GetKeySpeed();
            }

            // Reset error counter on successful poll
            _consecutiveErrors = 0;

            // Broadcast state if changed
            if (stateChanged)
            {
                await BroadcastStateAsync();
            }
        }
        catch (Exception ex)
        {
            _consecutiveErrors++;
            _logger.LogWarning(ex, "Error polling Hamlib rig state (consecutive errors: {Count}/{Max})",
                _consecutiveErrors, MaxConsecutiveErrors);

            if (_consecutiveErrors >= MaxConsecutiveErrors)
            {
                // Radio likely powered off or connection lost - transition to Disconnected
                _logger.LogWarning("Hamlib rig exceeded error threshold, treating as disconnected");

                var radioId = _radioId!;
                _rig?.Close();
                _rig?.Dispose();
                _rig = null;
                _radioId = null;
                // Keep _config alive so the rig stays in discovered list

                _currentFrequencyHz = 0;
                _currentMode = "";
                _isTransmitting = false;
                _consecutiveErrors = 0;

                await _hubContext.BroadcastRadioConnectionStateChanged(
                    new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Disconnected));
            }
            else
            {
                // Broadcast error state but keep trying
                await _hubContext.BroadcastRadioConnectionStateChanged(
                    new RadioConnectionStateChangedEvent(_radioId!, RadioConnectionState.Error, ex.Message));
            }
        }
    }

    /// <summary>
    /// Broadcast current state to all clients
    /// </summary>
    private async Task BroadcastStateAsync()
    {
        if (_radioId == null) return;

        var state = new RadioStateChangedEvent(
            _radioId,
            _currentFrequencyHz,
            _currentMode,
            _isTransmitting,
            BandHelper.GetBand(_currentFrequencyHz),
            null
        );

        _logger.LogDebug("Hamlib state: {Freq} Hz, {Mode}, TX={Tx}",
            _currentFrequencyHz, _currentMode, _isTransmitting);

        await _hubContext.BroadcastRadioStateChanged(state);
    }
}
