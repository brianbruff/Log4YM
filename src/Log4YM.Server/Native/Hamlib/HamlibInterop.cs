using System.Runtime.InteropServices;

namespace Log4YM.Server.Native.Hamlib;

/// <summary>
/// High-level wrapper for Hamlib providing safe, managed access
/// </summary>
public sealed class HamlibRig : IDisposable
{
    private readonly ILogger? _logger;
    private IntPtr _rig;
    private readonly object _lock = new();
    private bool _isOpen;
    private bool _disposed;

    public int ModelId { get; }
    public bool IsOpen => _isOpen;

    private HamlibRig(IntPtr rig, int modelId, ILogger? logger)
    {
        _rig = rig;
        ModelId = modelId;
        _logger = logger;
    }

    /// <summary>
    /// Initialize the Hamlib library (call once at startup)
    /// </summary>
    public static void Initialize()
    {
        HamlibNative.rig_load_all_backends();
        HamlibNative.rig_set_debug(RigDebug.RIG_DEBUG_ERR);
    }

    /// <summary>
    /// Create a new rig instance for the specified model
    /// </summary>
    public static HamlibRig? Create(int modelId, ILogger? logger = null)
    {
        var rig = HamlibNative.rig_init(modelId);
        if (rig == IntPtr.Zero)
        {
            logger?.LogError("Failed to initialize rig model {ModelId}", modelId);
            return null;
        }
        return new HamlibRig(rig, modelId, logger);
    }

    /// <summary>
    /// Configure serial port settings before opening
    /// </summary>
    public void ConfigureSerial(
        string portPath,
        int baudRate = 9600,
        int dataBits = 8,
        int stopBits = 1,
        SerialHandshake handshake = SerialHandshake.None,
        SerialParity parity = SerialParity.None)
    {
        ThrowIfDisposed();

        // Normalize Windows COM port paths
        // COM ports >= 10 or with certain drivers need \\.\COMx format
        var normalizedPath = NormalizeSerialPortPath(portPath);
        _logger?.LogInformation("Configuring serial port: {OriginalPath} -> {NormalizedPath}",
            portPath, normalizedPath);

        lock (_lock)
        {
            // Set pathname
            SetConf("rig_pathname", normalizedPath);

            // Set serial parameters via conf tokens where available
            SetConf("serial_speed", baudRate.ToString());
            SetConf("data_bits", dataBits.ToString());
            SetConf("stop_bits", stopBits.ToString());

            var flowStr = handshake switch
            {
                SerialHandshake.Hardware => "Hardware",
                SerialHandshake.XonXoff => "XONXOFF",
                _ => "None"
            };
            SetConf("serial_handshake", flowStr);

            var parityStr = parity switch
            {
                SerialParity.Even => "Even",
                SerialParity.Odd => "Odd",
                SerialParity.Mark => "Mark",
                SerialParity.Space => "Space",
                _ => "None"
            };
            SetConf("serial_parity", parityStr);
        }
    }

    /// <summary>
    /// Configure network settings before opening
    /// </summary>
    public void ConfigureNetwork(string hostname, int port = 4532)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            var pathStr = $"{hostname}:{port}";
            SetConf("rig_pathname", pathStr);
        }
    }

    /// <summary>
    /// Configure PTT type
    /// </summary>
    public void ConfigurePtt(string pttType, string? pttPort = null)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            SetConf("ptt_type", pttType);
            SetConf("ptt_share", "1"); // Enable PTT sharing

            if (!string.IsNullOrEmpty(pttPort))
            {
                SetConf("ptt_pathname", pttPort);
            }
        }
    }

    /// <summary>
    /// Set a configuration value
    /// </summary>
    private void SetConf(string name, string value)
    {
        var token = HamlibNative.rig_token_lookup(_rig, name);
        if (token > 0)
        {
            var result = HamlibNative.rig_set_conf(_rig, token, value);
            if (result != RigError.RIG_OK)
            {
                _logger?.LogDebug("Failed to set {Name}={Value}: {Error}", name, value, GetErrorString(result));
            }
        }
        else
        {
            _logger?.LogDebug("Config token not found: {Name}", name);
        }
    }

    /// <summary>
    /// Open connection to the rig
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when rig_open fails with error details</exception>
    public bool Open()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (_isOpen) return true;

            var result = HamlibNative.rig_open(_rig);
            if (result != RigError.RIG_OK)
            {
                var errorMsg = GetErrorString(result);
                _logger?.LogError("Failed to open rig: {Error} (code {Code})", errorMsg, result);
                throw new InvalidOperationException($"Hamlib rig_open failed: {errorMsg} (error code {result})");
            }

            _isOpen = true;
            _logger?.LogInformation("Rig opened successfully");
            return true;
        }
    }

    /// <summary>
    /// Close the rig connection
    /// </summary>
    public void Close()
    {
        lock (_lock)
        {
            if (!_isOpen || _rig == IntPtr.Zero) return;

            HamlibNative.rig_close(_rig);
            _isOpen = false;
            _logger?.LogInformation("Rig closed");
        }
    }

    /// <summary>
    /// Get current frequency in Hz
    /// </summary>
    public double? GetFrequency()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            var result = HamlibNative.rig_get_freq(_rig, RigVfo.RIG_VFO_CURR, out var freq);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get frequency failed: {Error}", GetErrorString(result));
                return null;
            }
            return freq;
        }
    }

    /// <summary>
    /// Set frequency in Hz
    /// </summary>
    public bool SetFrequency(double freqHz)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return false;

            var result = HamlibNative.rig_set_freq(_rig, RigVfo.RIG_VFO_CURR, freqHz);
            if (result != RigError.RIG_OK)
            {
                _logger?.LogWarning("Set frequency failed: {Error}", GetErrorString(result));
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Get current mode and passband width
    /// </summary>
    public (string Mode, int Passband)? GetMode()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            var result = HamlibNative.rig_get_mode(_rig, RigVfo.RIG_VFO_CURR, out var mode, out var width);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get mode failed: {Error}", GetErrorString(result));
                return null;
            }

            var modeStr = HamlibNative.PtrToString(HamlibNative.rig_strrmode(mode)) ?? "UNKNOWN";
            return (modeStr, width);
        }
    }

    /// <summary>
    /// Set mode by name
    /// </summary>
    public bool SetMode(string mode, int passband = RigMode.RIG_PASSBAND_NOCHANGE)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return false;

            var modeId = HamlibNative.rig_parse_mode(mode);
            if (modeId == RigMode.RIG_MODE_NONE)
            {
                _logger?.LogWarning("Unknown mode: {Mode}", mode);
                return false;
            }

            var result = HamlibNative.rig_set_mode(_rig, RigVfo.RIG_VFO_CURR, modeId, passband);
            if (result != RigError.RIG_OK)
            {
                _logger?.LogWarning("Set mode failed: {Error}", GetErrorString(result));
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Get current VFO
    /// </summary>
    public string? GetVfo()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            var result = HamlibNative.rig_get_vfo(_rig, out var vfo);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get VFO failed: {Error}", GetErrorString(result));
                return null;
            }

            return HamlibNative.PtrToString(HamlibNative.rig_strvfo(vfo));
        }
    }

    /// <summary>
    /// Get PTT state
    /// </summary>
    public bool? GetPtt()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            var result = HamlibNative.rig_get_ptt(_rig, RigVfo.RIG_VFO_CURR, out var ptt);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get PTT failed: {Error}", GetErrorString(result));
                return null;
            }

            return ptt != RigPtt.RIG_PTT_OFF;
        }
    }

    /// <summary>
    /// Set PTT state
    /// </summary>
    public bool SetPtt(bool tx)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return false;

            var pttState = tx ? RigPtt.RIG_PTT_ON : RigPtt.RIG_PTT_OFF;
            var result = HamlibNative.rig_set_ptt(_rig, RigVfo.RIG_VFO_CURR, pttState);
            if (result != RigError.RIG_OK)
            {
                _logger?.LogWarning("Set PTT failed: {Error}", GetErrorString(result));
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Get RF power in watts (if supported)
    /// </summary>
    public double? GetPower(double currentFreq, ulong currentMode)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            // Check if power reading is supported
            if (HamlibNative.rig_has_get_level(_rig, RigLevel.RIG_LEVEL_RFPOWER) == 0)
                return null;

            var result = HamlibNative.rig_get_level_float(_rig, RigVfo.RIG_VFO_CURR, RigLevel.RIG_LEVEL_RFPOWER, out var powerLevel);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get power level failed: {Error}", GetErrorString(result));
                return null;
            }

            // Convert to milliwatts
            result = HamlibNative.rig_power2mW(_rig, out var mwPower, powerLevel, currentFreq, currentMode);
            if (result != RigError.RIG_OK)
            {
                return null;
            }

            return mwPower / 1000.0; // Convert to watts
        }
    }

    /// <summary>
    /// Get RIT offset in Hz (if supported)
    /// </summary>
    public int? GetRit()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            // Check if RIT function is enabled
            if (HamlibNative.rig_has_get_func(_rig, RigFunc.RIG_FUNC_RIT) == 0)
                return null;

            var result = HamlibNative.rig_get_func(_rig, RigVfo.RIG_VFO_CURR, RigFunc.RIG_FUNC_RIT, out var ritEnabled);
            if (result != RigError.RIG_OK || ritEnabled == 0)
                return 0;

            result = HamlibNative.rig_get_rit(_rig, RigVfo.RIG_VFO_CURR, out var rit);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get RIT failed: {Error}", GetErrorString(result));
                return null;
            }

            return rit;
        }
    }

    /// <summary>
    /// Get XIT offset in Hz (if supported)
    /// </summary>
    public int? GetXit()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            // Check if XIT function is enabled
            if (HamlibNative.rig_has_get_func(_rig, RigFunc.RIG_FUNC_XIT) == 0)
                return null;

            var result = HamlibNative.rig_get_func(_rig, RigVfo.RIG_VFO_CURR, RigFunc.RIG_FUNC_XIT, out var xitEnabled);
            if (result != RigError.RIG_OK || xitEnabled == 0)
                return 0;

            result = HamlibNative.rig_get_xit(_rig, RigVfo.RIG_VFO_CURR, out var xit);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get XIT failed: {Error}", GetErrorString(result));
                return null;
            }

            return xit;
        }
    }

    /// <summary>
    /// Get CW key speed in WPM (if supported)
    /// </summary>
    public int? GetKeySpeed()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen) return null;

            if (HamlibNative.rig_has_get_level(_rig, RigLevel.RIG_LEVEL_KEYSPD) == 0)
                return null;

            var result = HamlibNative.rig_get_level_int(_rig, RigVfo.RIG_VFO_CURR, RigLevel.RIG_LEVEL_KEYSPD, out var wpm);
            if (result != RigError.RIG_OK)
            {
                if (!RigError.IsSoftError(result))
                    _logger?.LogDebug("Get key speed failed: {Error}", GetErrorString(result));
                return null;
            }

            return wpm;
        }
    }

    /// <summary>
    /// Set CW key speed in WPM
    /// </summary>
    public bool SetKeySpeed(int wpm)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            if (!_isOpen || wpm < 0) return false;

            var result = HamlibNative.rig_set_level_int(_rig, RigVfo.RIG_VFO_CURR, RigLevel.RIG_LEVEL_KEYSPD, wpm);
            if (result != RigError.RIG_OK)
            {
                _logger?.LogWarning("Set key speed failed: {Error}", GetErrorString(result));
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Normalize serial port path for the current platform
    /// Windows COM ports need special handling for Hamlib
    /// </summary>
    private static string NormalizeSerialPortPath(string portPath)
    {
        if (string.IsNullOrEmpty(portPath))
            return portPath;

        // On Windows, normalize COM port paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // If it's already in \\.\COMx format, leave it alone
            if (portPath.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
                return portPath;

            // If it's COMx format, convert to \\.\COMx for compatibility
            // This is required for COM ports >= 10, and works for all COM ports
            if (portPath.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            {
                return @"\\.\" + portPath.ToUpperInvariant();
            }
        }

        return portPath;
    }

    private static string GetErrorString(int errorCode)
    {
        // Try rigerror2 first (Hamlib 4.5+), fall back to rigerror
        var ptr = HamlibNative.rigerror2(errorCode);
        if (ptr == IntPtr.Zero)
            ptr = HamlibNative.rigerror(errorCode);
        return HamlibNative.PtrToString(ptr) ?? $"Error {errorCode}";
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HamlibRig));
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            Close();

            if (_rig != IntPtr.Zero)
            {
                HamlibNative.rig_cleanup(_rig);
                _rig = IntPtr.Zero;
            }
        }
    }
}

/// <summary>
/// Static helper for listing available rig models
/// </summary>
public static class HamlibRigList
{
    private static List<RigModelInfo>? _cachedModels;
    private static readonly object _cacheLock = new();
    private static string? _initError;

    /// <summary>
    /// Check if initialization has been attempted
    /// </summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// Get any initialization error message
    /// </summary>
    public static string? InitError => _initError ?? HamlibNative.LoadError;

    /// <summary>
    /// Get list of all available rig models
    /// </summary>
    public static List<RigModelInfo> GetModels()
    {
        lock (_cacheLock)
        {
            if (_cachedModels != null)
                return _cachedModels;

            try
            {
                HamlibRig.Initialize();
                IsInitialized = true;
                Console.WriteLine("[Hamlib] Library initialized, enumerating rig models...");
            }
            catch (Exception ex)
            {
                _initError = $"Failed to initialize Hamlib: {ex.Message}";
                Console.WriteLine($"[Hamlib] {_initError}");
                _cachedModels = new List<RigModelInfo>();
                return _cachedModels;
            }

            var models = new List<RigModelInfo>();

            // Use the newer API if available (Hamlib 4.2+)
            try
            {
                Console.WriteLine("[Hamlib] Using rig_list_foreach_model API...");
                HamlibNative.rig_list_foreach_model((modelId, data) =>
                {
                    try
                    {
                        var mfgPtr = HamlibNative.rig_get_caps_cptr(modelId, RigCapsField.RIG_CAPS_MFG_NAME_CPTR);
                        var modelPtr = HamlibNative.rig_get_caps_cptr(modelId, RigCapsField.RIG_CAPS_MODEL_NAME_CPTR);
                        var versionPtr = HamlibNative.rig_get_caps_cptr(modelId, RigCapsField.RIG_CAPS_VERSION_CPTR);

                        var mfg = HamlibNative.PtrToString(mfgPtr)?.Trim() ?? "Unknown";
                        var model = HamlibNative.PtrToString(modelPtr)?.Trim() ?? "Unknown";
                        var version = HamlibNative.PtrToString(versionPtr)?.Trim() ?? "";

                        models.Add(new RigModelInfo(modelId, mfg, model, version));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Hamlib] Error reading model {modelId}: {ex.Message}");
                    }
                    return 1; // Continue enumeration (1 = continue, 0 = stop)
                }, IntPtr.Zero);
                Console.WriteLine($"[Hamlib] Enumerated {models.Count} rig models using new API");
            }
            catch (EntryPointNotFoundException)
            {
                Console.WriteLine("[Hamlib] rig_list_foreach_model not found, falling back to older API...");
                // Fall back to older API
                try
                {
                    HamlibNative.rig_list_foreach((capsPtr, data) =>
                    {
                        if (capsPtr == IntPtr.Zero) return 1;

                        try
                        {
                            // Read from rig_caps structure
                            // This is less reliable but works with older Hamlib
                            var modelId = Marshal.ReadInt32(capsPtr, 0); // rig_model is first field

                            // Skip complex structure parsing for older API
                            // Just use the model ID and fetch caps separately
                            var caps = HamlibNative.rig_get_caps(modelId);
                            if (caps != IntPtr.Zero)
                            {
                                // Basic info extraction would require struct marshaling
                                models.Add(new RigModelInfo(modelId, "Unknown", $"Model {modelId}", ""));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[Hamlib] Error in rig_list_foreach callback: {ex.Message}");
                        }
                        return 1;
                    }, IntPtr.Zero);
                    Console.WriteLine($"[Hamlib] Enumerated {models.Count} rig models using legacy API");
                }
                catch (Exception ex)
                {
                    _initError = $"Failed to enumerate rigs: {ex.Message}";
                    Console.WriteLine($"[Hamlib] {_initError}");
                }
            }
            catch (DllNotFoundException ex)
            {
                _initError = $"Hamlib native library not found: {ex.Message}";
                Console.WriteLine($"[Hamlib] {_initError}");
            }
            catch (Exception ex)
            {
                _initError = $"Error enumerating rigs: {ex.Message}";
                Console.WriteLine($"[Hamlib] {_initError}");
            }

            _cachedModels = models.OrderBy(m => m.Manufacturer).ThenBy(m => m.Model).ToList();
            Console.WriteLine($"[Hamlib] Returning {_cachedModels.Count} rig models (sorted)");
            return _cachedModels;
        }
    }

    /// <summary>
    /// Clear cached model list (if Hamlib backends change)
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _cachedModels = null;
            IsInitialized = false;
            _initError = null;
        }
    }
}

/// <summary>
/// Information about a rig model
/// </summary>
public record RigModelInfo(int ModelId, string Manufacturer, string Model, string Version)
{
    public string DisplayName => string.IsNullOrEmpty(Version)
        ? $"{Manufacturer} {Model}"
        : $"{Manufacturer} {Model} ({Version})";
}

/// <summary>
/// Rig capabilities for a specific model
/// </summary>
public record RigCapabilities
{
    public bool CanGetFreq { get; init; }
    public bool CanGetMode { get; init; }
    public bool CanGetVfo { get; init; }
    public bool CanGetPtt { get; init; }
    public bool CanGetPower { get; init; }
    public bool CanGetRit { get; init; }
    public bool CanGetXit { get; init; }
    public bool CanGetKeySpeed { get; init; }
    public bool CanSendMorse { get; init; }
    public int DefaultDataBits { get; init; }
    public int DefaultStopBits { get; init; }
    public bool IsNetworkOnly { get; init; }

    /// <summary>
    /// Get capabilities for a specific rig model
    /// Note: This requires creating a temporary rig instance
    /// </summary>
    public static RigCapabilities GetForModel(int modelId)
    {
        var capsPtr = HamlibNative.rig_get_caps(modelId);
        if (capsPtr == IntPtr.Zero)
        {
            return new RigCapabilities();
        }

        // The rig_caps structure is complex - we need to create a rig instance
        // to properly check capabilities
        var isNetworkOnly = modelId == RigModel.RIG_MODEL_NETRIGCTL;

        // For network rigs, assume broad capability (actual caps known after connect)
        if (isNetworkOnly)
        {
            return new RigCapabilities
            {
                CanGetFreq = true,
                CanGetMode = true,
                CanGetVfo = true,
                CanGetPtt = true,
                CanGetPower = true,
                CanGetRit = true,
                CanGetXit = true,
                CanGetKeySpeed = true,
                CanSendMorse = true,
                DefaultDataBits = 8,
                DefaultStopBits = 1,
                IsNetworkOnly = true
            };
        }

        // For physical rigs, we need to read from the caps structure
        // This requires struct marshaling which is complex for rig_caps
        // For now, return defaults that allow user to try all features
        return new RigCapabilities
        {
            CanGetFreq = true,
            CanGetMode = true,
            CanGetVfo = true,
            CanGetPtt = true,
            CanGetPower = true,
            CanGetRit = true,
            CanGetXit = true,
            CanGetKeySpeed = true,
            CanSendMorse = false, // Be conservative on morse
            DefaultDataBits = 8,
            DefaultStopBits = 1,
            IsNetworkOnly = false
        };
    }
}
