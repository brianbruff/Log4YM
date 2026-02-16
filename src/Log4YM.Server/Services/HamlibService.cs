using System.IO.Ports;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;
using Log4YM.Server.Native.Hamlib;
using MongoDB.Driver;

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
    private readonly IMongoDatabase? _database;

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

    private const string CollectionName = "settings";
    private const string ConfigDocId = "hamlib_config";

    public HamlibService(
        ILogger<HamlibService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceScopeFactory scopeFactory,
        IUserConfigService userConfigService)
    {
        _logger = logger;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;

        // Only connect to MongoDB when the provider is explicitly set to MongoDb.
        // Previously this only checked whether a connection string existed, which
        // meant a stale Atlas URI left over in config.json after switching to
        // Local provider would still trigger a MongoClient construction — blocking
        // the constructor on DNS SRV resolution for an unreachable cluster.
        try
        {
            var config = userConfigService.GetConfigAsync().GetAwaiter().GetResult();
            if (config.Provider == DatabaseProvider.MongoDb
                && !string.IsNullOrEmpty(config.MongoDbConnectionString))
            {
                var client = new MongoClient(config.MongoDbConnectionString);
                var databaseName = config.MongoDbDatabaseName ?? "Log4YM";
                _database = client.GetDatabase(databaseName);
                _logger.LogInformation("HamlibService connected to MongoDB database: {DatabaseName}", databaseName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to MongoDB for Hamlib config storage");
        }
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
    /// Get current saved configuration
    /// </summary>
    public async Task<HamlibRigConfig?> LoadConfigAsync()
    {
        if (_database == null) return null;

        try
        {
            var collection = _database.GetCollection<HamlibRigConfig>(CollectionName);
            return await collection.Find(c => c.Id == ConfigDocId).FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Hamlib config from MongoDB");
            return null;
        }
    }

    /// <summary>
    /// Save configuration to MongoDB
    /// </summary>
    private async Task SaveConfigAsync(HamlibRigConfig config)
    {
        if (_database == null) return;

        try
        {
            var collection = _database.GetCollection<HamlibRigConfig>(CollectionName);
            await collection.ReplaceOneAsync(
                c => c.Id == ConfigDocId,
                config with { Id = ConfigDocId },
                new ReplaceOptions { IsUpsert = true });
            _logger.LogInformation("Saved Hamlib config to MongoDB: {ModelName} ({ModelId})", config.ModelName, config.ModelId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Hamlib config to MongoDB");
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
    /// Delete saved configuration from MongoDB
    /// </summary>
    public async Task DeleteConfigAsync()
    {
        if (_database == null) return;

        try
        {
            var collection = _database.GetCollection<HamlibRigConfig>(CollectionName);
            await collection.DeleteOneAsync(c => c.Id == ConfigDocId);
            _logger.LogInformation("Deleted Hamlib config from MongoDB");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete Hamlib config from MongoDB");
        }
    }

    /// <summary>
    /// Get discovered radios (includes saved configuration even if not connected)
    /// </summary>
    public async Task<IEnumerable<RadioDiscoveredEvent>> GetDiscoveredRadiosAsync()
    {
        var radios = new List<RadioDiscoveredEvent>();

        // If we're currently connected, return the active connection
        if (_radioId != null && _config != null)
        {
            radios.Add(new RadioDiscoveredEvent(
                _radioId,
                RadioType.Hamlib,
                _config.ModelName,
                _config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? _config.Hostname ?? "" : _config.SerialPort ?? "",
                _config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? _config.NetworkPort : 0,
                null,
                null
            ));
        }
        // If not connected but we have an in-memory config (disconnect keeps it alive), use that
        else if (_config != null)
        {
            var radioId = $"hamlib-{_config.ModelId}";
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
        // Fall back to saved config from database
        else
        {
            var savedConfig = await LoadConfigAsync();
            if (savedConfig != null)
            {
                var radioId = $"hamlib-{savedConfig.ModelId}";
                radios.Add(new RadioDiscoveredEvent(
                    radioId,
                    RadioType.Hamlib,
                    savedConfig.ModelName,
                    savedConfig.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? savedConfig.Hostname ?? "" : savedConfig.SerialPort ?? "",
                    savedConfig.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? savedConfig.NetworkPort : 0,
                    null,
                    null
                ));
            }
        }

        return radios;
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
