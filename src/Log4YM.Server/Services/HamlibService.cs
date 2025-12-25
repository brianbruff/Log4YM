using System.IO.Ports;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
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

    private const string CollectionName = "settings";
    private const string ConfigDocId = "hamlib_config";

    public HamlibService(
        ILogger<HamlibService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IConfiguration configuration)
    {
        _logger = logger;
        _hubContext = hubContext;

        // Get MongoDB connection
        var connectionString = configuration.GetConnectionString("MongoDB");
        if (!string.IsNullOrEmpty(connectionString))
        {
            try
            {
                var client = new MongoClient(connectionString);
                _database = client.GetDatabase("log4ym");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to MongoDB for Hamlib config storage");
            }
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

        // Try to load and auto-connect saved config
        var savedConfig = await LoadConfigAsync();
        if (savedConfig != null)
        {
            _logger.LogInformation("Found saved Hamlib config for model {ModelId}, attempting auto-connect", savedConfig.ModelId);
            try
            {
                await ConnectAsync(savedConfig);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-connect to saved Hamlib rig failed");
            }
        }

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
        if (!_initialized) return new RigCapabilities();
        return RigCapabilities.GetForModel(modelId);
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
                    _rig.ConfigurePtt(config.GetPttTypeString(), config.PttPort);
                }
            }
            else // Network
            {
                if (string.IsNullOrEmpty(config.Hostname))
                {
                    throw new InvalidOperationException("Hostname not specified");
                }

                _rig.ConfigureNetwork(config.Hostname, config.NetworkPort);
            }

            // Open connection
            if (!_rig.Open())
            {
                throw new InvalidOperationException("Failed to open rig connection");
            }

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
        _config = null;

        // Reset state
        _currentFrequencyHz = 0;
        _currentMode = "";
        _isTransmitting = false;

        _logger.LogInformation("Disconnected from Hamlib rig");

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Disconnected));
        await _hubContext.BroadcastRadioRemoved(new RadioRemovedEvent(radioId));
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
            _logger.LogDebug("Saved Hamlib config to MongoDB");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save Hamlib config to MongoDB");
        }
    }

    /// <summary>
    /// Get discovered radios (just the current connection if any)
    /// </summary>
    public IEnumerable<RadioDiscoveredEvent> GetDiscoveredRadios()
    {
        if (_radioId == null || _config == null) yield break;

        yield return new RadioDiscoveredEvent(
            _radioId,
            RadioType.Hamlib,
            _config.ModelName,
            _config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? _config.Hostname ?? "" : _config.SerialPort ?? "",
            _config.ConnectionType == Native.Hamlib.HamlibConnectionType.Network ? _config.NetworkPort : 0,
            null,
            null
        );
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

            // Broadcast state if changed
            if (stateChanged)
            {
                await BroadcastStateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error polling Hamlib rig state");

            // Connection may be lost
            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_radioId, RadioConnectionState.Error, ex.Message));
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
