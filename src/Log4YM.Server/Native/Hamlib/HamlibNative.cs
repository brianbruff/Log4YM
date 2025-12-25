using System.Reflection;
using System.Runtime.InteropServices;

namespace Log4YM.Server.Native.Hamlib;

/// <summary>
/// P/Invoke declarations for Hamlib library
/// Targets Hamlib 4.5+ API
/// </summary>
public static class HamlibNative
{
    private const string LibraryName = "hamlib";

    public static bool IsLoaded { get; private set; }
    public static string? LoadedLibraryPath { get; private set; }
    public static string? LoadError { get; private set; }

    static HamlibNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(HamlibNative).Assembly, ImportResolver);
    }

    private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibraryName)
            return IntPtr.Zero;

        // Try platform-specific paths in order
        foreach (var path in GetLibrarySearchPaths())
        {
            if (File.Exists(path))
            {
                try
                {
                    var handle = NativeLibrary.Load(path);
                    if (handle != IntPtr.Zero)
                    {
                        IsLoaded = true;
                        LoadedLibraryPath = path;
                        Console.WriteLine($"[Hamlib] Loaded native library from: {path}");
                        return handle;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Hamlib] Failed to load {path}: {ex.Message}");
                }
            }
        }

        // Fallback to system library search
        var systemLibName = GetSystemLibraryName();
        if (NativeLibrary.TryLoad(systemLibName, out var systemHandle))
        {
            IsLoaded = true;
            LoadedLibraryPath = $"system:{systemLibName}";
            Console.WriteLine($"[Hamlib] Loaded native library from system: {systemLibName}");
            return systemHandle;
        }

        LoadError = $"Could not find Hamlib native library. Searched paths: {string.Join(", ", GetLibrarySearchPaths())}";
        Console.WriteLine($"[Hamlib] {LoadError}");
        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetLibrarySearchPaths()
    {
        var baseDir = AppContext.BaseDirectory;
        var rid = GetRuntimeIdentifier();
        var libName = GetLibraryFileName();

        // 1. runtimes/{rid}/native/{libname} in app directory
        yield return Path.Combine(baseDir, "runtimes", rid, "native", libName);

        // 2. Direct lib name in base directory
        yield return Path.Combine(baseDir, libName);

        // 3. Platform-specific system paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Homebrew paths for macOS
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                yield return "/opt/homebrew/opt/hamlib/lib/libhamlib.4.dylib";
                yield return "/opt/homebrew/lib/libhamlib.4.dylib";
            }
            else
            {
                yield return "/usr/local/opt/hamlib/lib/libhamlib.4.dylib";
                yield return "/usr/local/lib/libhamlib.4.dylib";
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Common Linux paths
            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                yield return "/usr/lib/x86_64-linux-gnu/libhamlib.so.4";
                yield return "/usr/lib64/libhamlib.so.4";
            }
            else if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                yield return "/usr/lib/aarch64-linux-gnu/libhamlib.so.4";
            }
            yield return "/usr/local/lib/libhamlib.so.4";
            yield return "/usr/lib/libhamlib.so.4";
        }
    }

    private static string GetRuntimeIdentifier()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "win-x64" : "win-arm64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "linux-x64" : "linux-arm64";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? "osx-x64" : "osx-arm64";
        return "unknown";
    }

    private static string GetLibraryFileName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "hamlib-4.dll";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "libhamlib.so.4";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libhamlib.4.dylib";
        return "libhamlib.so";
    }

    private static string GetSystemLibraryName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "hamlib-4";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libhamlib.4.dylib";
        return "libhamlib.so.4";
    }

    // =========================================================================
    // Initialization Functions
    // =========================================================================

    /// <summary>
    /// Load all Hamlib backend drivers
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_load_all_backends();

    /// <summary>
    /// Initialize a rig handle for a given model
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rig_init(int rig_model);

    /// <summary>
    /// Open the rig connection
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_open(IntPtr rig);

    /// <summary>
    /// Close the rig connection
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_close(IntPtr rig);

    /// <summary>
    /// Cleanup/free rig handle
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_cleanup(IntPtr rig);

    /// <summary>
    /// Set debug level
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void rig_set_debug(int debug_level);

    // =========================================================================
    // Model Enumeration
    // =========================================================================

    /// <summary>
    /// Callback delegate for rig_list_foreach
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int RigListCallback(IntPtr caps, IntPtr data);

    /// <summary>
    /// Callback delegate for rig_list_foreach_model (Hamlib 4.2+)
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int RigListModelCallback(int rig_model, IntPtr data);

    /// <summary>
    /// Enumerate all available rig models (older API)
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_list_foreach(RigListCallback callback, IntPtr data);

    /// <summary>
    /// Enumerate all available rig models (Hamlib 4.2+ API)
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_list_foreach_model(RigListModelCallback callback, IntPtr data);

    /// <summary>
    /// Get capabilities structure for a rig model
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rig_get_caps(int rig_model);

    /// <summary>
    /// Get a specific capability string (Hamlib 4.2+)
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rig_get_caps_cptr(int rig_model, int caps_type);

    // =========================================================================
    // Frequency Functions
    // =========================================================================

    /// <summary>
    /// Get current frequency
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_freq(IntPtr rig, int vfo, out double freq);

    /// <summary>
    /// Set frequency
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_set_freq(IntPtr rig, int vfo, double freq);

    // =========================================================================
    // Mode Functions
    // =========================================================================

    /// <summary>
    /// Get current mode and passband width
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_mode(IntPtr rig, int vfo, out ulong mode, out int width);

    /// <summary>
    /// Set mode and passband width
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_set_mode(IntPtr rig, int vfo, ulong mode, int width);

    /// <summary>
    /// Parse mode string to mode ID
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern ulong rig_parse_mode([MarshalAs(UnmanagedType.LPStr)] string mode);

    /// <summary>
    /// Get mode string from mode ID
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rig_strrmode(ulong mode);

    // =========================================================================
    // VFO Functions
    // =========================================================================

    /// <summary>
    /// Get current VFO
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_vfo(IntPtr rig, out int vfo);

    /// <summary>
    /// Set VFO
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_set_vfo(IntPtr rig, int vfo);

    /// <summary>
    /// Get VFO string from VFO ID
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rig_strvfo(int vfo);

    // =========================================================================
    // PTT Functions
    // =========================================================================

    /// <summary>
    /// Get PTT state
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_ptt(IntPtr rig, int vfo, out int ptt);

    /// <summary>
    /// Set PTT state
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_set_ptt(IntPtr rig, int vfo, int ptt);

    // =========================================================================
    // Level Functions
    // =========================================================================

    /// <summary>
    /// Get level value (power, key speed, etc.)
    /// The value union is complex - we'll handle it via overloads
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rig_get_level")]
    public static extern int rig_get_level_int(IntPtr rig, int vfo, uint level, out int val);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rig_get_level")]
    public static extern int rig_get_level_float(IntPtr rig, int vfo, uint level, out float val);

    /// <summary>
    /// Set level value
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rig_set_level")]
    public static extern int rig_set_level_int(IntPtr rig, int vfo, uint level, int val);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "rig_set_level")]
    public static extern int rig_set_level_float(IntPtr rig, int vfo, uint level, float val);

    /// <summary>
    /// Check if rig has get capability for a level
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_has_get_level(IntPtr rig, uint level);

    /// <summary>
    /// Convert power level to milliwatts
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_power2mW(IntPtr rig, out uint mwpower, float power, double freq, ulong mode);

    // =========================================================================
    // RIT/XIT Functions
    // =========================================================================

    /// <summary>
    /// Get RIT offset
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_rit(IntPtr rig, int vfo, out int rit);

    /// <summary>
    /// Get XIT offset
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_xit(IntPtr rig, int vfo, out int xit);

    // =========================================================================
    // Function Get/Set
    // =========================================================================

    /// <summary>
    /// Get function status (RIT, XIT enable, etc.)
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_get_func(IntPtr rig, int vfo, uint func, out int status);

    /// <summary>
    /// Check if rig has get capability for a function
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_has_get_func(IntPtr rig, uint func);

    // =========================================================================
    // Configuration Functions
    // =========================================================================

    /// <summary>
    /// Lookup configuration token by name
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int rig_token_lookup(IntPtr rig, [MarshalAs(UnmanagedType.LPStr)] string name);

    /// <summary>
    /// Set configuration value
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int rig_set_conf(IntPtr rig, int token, [MarshalAs(UnmanagedType.LPStr)] string val);

    // =========================================================================
    // Morse Functions (for future use)
    // =========================================================================

    /// <summary>
    /// Send morse code
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int rig_send_morse(IntPtr rig, int vfo, [MarshalAs(UnmanagedType.LPStr)] string msg);

    /// <summary>
    /// Stop sending morse (Hamlib 4.0+)
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int rig_stop_morse(IntPtr rig, int vfo);

    // =========================================================================
    // Error Handling
    // =========================================================================

    /// <summary>
    /// Get error string for error code
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rigerror(int errnum);

    /// <summary>
    /// Get error string (Hamlib 4.5+)
    /// </summary>
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr rigerror2(int errnum);

    // =========================================================================
    // Helper to read native string
    // =========================================================================

    public static string? PtrToString(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        return Marshal.PtrToStringAnsi(ptr);
    }
}

// =========================================================================
// Hamlib Constants and Enums
// =========================================================================

/// <summary>
/// Hamlib return codes
/// </summary>
public static class RigError
{
    public const int RIG_OK = 0;
    public const int RIG_EINVAL = -1;
    public const int RIG_ECONF = -2;
    public const int RIG_ENOMEM = -3;
    public const int RIG_ENIMPL = -4;
    public const int RIG_ETIMEOUT = -5;
    public const int RIG_EIO = -6;
    public const int RIG_EINTERNAL = -7;
    public const int RIG_EPROTO = -8;
    public const int RIG_ERJCTED = -9;
    public const int RIG_ETRUNC = -10;
    public const int RIG_ENAVAIL = -11;
    public const int RIG_ENTARGET = -12;
    public const int RIG_BUSERROR = -13;
    public const int RIG_BUSBUSY = -14;
    public const int RIG_EARG = -15;
    public const int RIG_EVFO = -16;
    public const int RIG_EDOM = -17;

    /// <summary>
    /// Check if error code is a soft error (capability-related, not fatal)
    /// </summary>
    public static bool IsSoftError(int errcode)
    {
        return errcode == RIG_EINVAL || errcode == RIG_ENIMPL || errcode == RIG_ERJCTED
            || errcode == RIG_ETRUNC || errcode == RIG_ENAVAIL || errcode == RIG_ENTARGET
            || errcode == RIG_EVFO || errcode == RIG_EDOM;
    }
}

/// <summary>
/// Hamlib debug levels
/// </summary>
public static class RigDebug
{
    public const int RIG_DEBUG_NONE = 0;
    public const int RIG_DEBUG_BUG = 1;
    public const int RIG_DEBUG_ERR = 2;
    public const int RIG_DEBUG_WARN = 3;
    public const int RIG_DEBUG_VERBOSE = 4;
    public const int RIG_DEBUG_TRACE = 5;
    public const int RIG_DEBUG_CACHE = 6;
}

/// <summary>
/// Hamlib VFO constants
/// </summary>
public static class RigVfo
{
    public const int RIG_VFO_NONE = 0;
    public const int RIG_VFO_CURR = unchecked((int)0x20000000);
    public const int RIG_VFO_A = (1 << 0);
    public const int RIG_VFO_B = (1 << 1);
    public const int RIG_VFO_C = (1 << 2);
    public const int RIG_VFO_MAIN = (1 << 3);
    public const int RIG_VFO_SUB = (1 << 4);
}

/// <summary>
/// Hamlib mode constants (bitmask)
/// </summary>
public static class RigMode
{
    public const ulong RIG_MODE_NONE = 0;
    public const ulong RIG_MODE_AM = (1UL << 0);
    public const ulong RIG_MODE_CW = (1UL << 1);
    public const ulong RIG_MODE_USB = (1UL << 2);
    public const ulong RIG_MODE_LSB = (1UL << 3);
    public const ulong RIG_MODE_RTTY = (1UL << 4);
    public const ulong RIG_MODE_FM = (1UL << 5);
    public const ulong RIG_MODE_WFM = (1UL << 6);
    public const ulong RIG_MODE_CWR = (1UL << 7);
    public const ulong RIG_MODE_RTTYR = (1UL << 8);
    public const ulong RIG_MODE_AMS = (1UL << 9);
    public const ulong RIG_MODE_PKTLSB = (1UL << 10);
    public const ulong RIG_MODE_PKTUSB = (1UL << 11);
    public const ulong RIG_MODE_PKTFM = (1UL << 12);
    public const ulong RIG_MODE_ECSSUSB = (1UL << 13);
    public const ulong RIG_MODE_ECSSLSB = (1UL << 14);
    public const ulong RIG_MODE_FAX = (1UL << 15);
    public const ulong RIG_MODE_SAM = (1UL << 16);
    public const ulong RIG_MODE_SAL = (1UL << 17);
    public const ulong RIG_MODE_SAH = (1UL << 18);
    public const ulong RIG_MODE_DSB = (1UL << 19);
    public const ulong RIG_MODE_FMN = (1UL << 21);
    public const ulong RIG_MODE_PKTAM = (1UL << 22);

    public const int RIG_PASSBAND_NOCHANGE = -1;
}

/// <summary>
/// Hamlib PTT constants
/// </summary>
public static class RigPtt
{
    public const int RIG_PTT_OFF = 0;
    public const int RIG_PTT_ON = 1;
    public const int RIG_PTT_ON_MIC = 2;
    public const int RIG_PTT_ON_DATA = 3;
}

/// <summary>
/// Hamlib PTT type constants
/// </summary>
public static class RigPttType
{
    public const int RIG_PTT_NONE = 0;
    public const int RIG_PTT_RIG = 1;
    public const int RIG_PTT_SERIAL_DTR = 2;
    public const int RIG_PTT_SERIAL_RTS = 3;
    public const int RIG_PTT_PARALLEL = 4;
    public const int RIG_PTT_RIG_MICDATA = 5;
    public const int RIG_PTT_CM108 = 6;
    public const int RIG_PTT_GPIO = 7;
    public const int RIG_PTT_GPION = 8;
}

/// <summary>
/// Hamlib level constants
/// </summary>
public static class RigLevel
{
    public const uint RIG_LEVEL_NONE = 0;
    public const uint RIG_LEVEL_PREAMP = (1U << 0);
    public const uint RIG_LEVEL_ATT = (1U << 1);
    public const uint RIG_LEVEL_VOXDELAY = (1U << 2);
    public const uint RIG_LEVEL_AF = (1U << 3);
    public const uint RIG_LEVEL_RF = (1U << 4);
    public const uint RIG_LEVEL_SQL = (1U << 5);
    public const uint RIG_LEVEL_IF = (1U << 6);
    public const uint RIG_LEVEL_APF = (1U << 7);
    public const uint RIG_LEVEL_NR = (1U << 8);
    public const uint RIG_LEVEL_PBT_IN = (1U << 9);
    public const uint RIG_LEVEL_PBT_OUT = (1U << 10);
    public const uint RIG_LEVEL_CWPITCH = (1U << 11);
    public const uint RIG_LEVEL_RFPOWER = (1U << 12);
    public const uint RIG_LEVEL_MICGAIN = (1U << 13);
    public const uint RIG_LEVEL_KEYSPD = (1U << 14);
    public const uint RIG_LEVEL_NOTCHF = (1U << 15);
    public const uint RIG_LEVEL_COMP = (1U << 16);
    public const uint RIG_LEVEL_AGC = (1U << 17);
    public const uint RIG_LEVEL_BKINDL = (1U << 18);
    public const uint RIG_LEVEL_BALANCE = (1U << 19);
    public const uint RIG_LEVEL_METER = (1U << 20);
    public const uint RIG_LEVEL_VOXGAIN = (1U << 21);
    public const uint RIG_LEVEL_ANTIVOX = (1U << 22);
    public const uint RIG_LEVEL_SLOPE_LOW = (1U << 23);
    public const uint RIG_LEVEL_SLOPE_HIGH = (1U << 24);
    public const uint RIG_LEVEL_BKIN_DLYMS = (1U << 25);
    public const uint RIG_LEVEL_RAWSTR = (1U << 26);
    public const uint RIG_LEVEL_SWR = (1U << 27);
    public const uint RIG_LEVEL_ALC = (1U << 28);
    public const uint RIG_LEVEL_STRENGTH = (1U << 29);
    public const uint RIG_LEVEL_RFPOWER_METER = (1U << 30);
    public const uint RIG_LEVEL_COMP_METER = (1U << 31);
}

/// <summary>
/// Hamlib function constants (for RIT/XIT enable, etc.)
/// </summary>
public static class RigFunc
{
    public const uint RIG_FUNC_NONE = 0;
    public const uint RIG_FUNC_FAGC = (1U << 0);
    public const uint RIG_FUNC_NB = (1U << 1);
    public const uint RIG_FUNC_COMP = (1U << 2);
    public const uint RIG_FUNC_VOX = (1U << 3);
    public const uint RIG_FUNC_TONE = (1U << 4);
    public const uint RIG_FUNC_TSQL = (1U << 5);
    public const uint RIG_FUNC_SBKIN = (1U << 6);
    public const uint RIG_FUNC_FBKIN = (1U << 7);
    public const uint RIG_FUNC_ANF = (1U << 8);
    public const uint RIG_FUNC_NR = (1U << 9);
    public const uint RIG_FUNC_AIP = (1U << 10);
    public const uint RIG_FUNC_APF = (1U << 11);
    public const uint RIG_FUNC_MON = (1U << 12);
    public const uint RIG_FUNC_MN = (1U << 13);
    public const uint RIG_FUNC_RF = (1U << 14);
    public const uint RIG_FUNC_ARO = (1U << 15);
    public const uint RIG_FUNC_LOCK = (1U << 16);
    public const uint RIG_FUNC_MUTE = (1U << 17);
    public const uint RIG_FUNC_VSC = (1U << 18);
    public const uint RIG_FUNC_REV = (1U << 19);
    public const uint RIG_FUNC_SQL = (1U << 20);
    public const uint RIG_FUNC_ABM = (1U << 21);
    public const uint RIG_FUNC_BC = (1U << 22);
    public const uint RIG_FUNC_MBC = (1U << 23);
    public const uint RIG_FUNC_RIT = (1U << 24);
    public const uint RIG_FUNC_AFC = (1U << 25);
    public const uint RIG_FUNC_SATMODE = (1U << 26);
    public const uint RIG_FUNC_SCOPE = (1U << 27);
    public const uint RIG_FUNC_RESUME = (1U << 28);
    public const uint RIG_FUNC_TBURST = (1U << 29);
    public const uint RIG_FUNC_TUNER = (1U << 30);
    public const uint RIG_FUNC_XIT = (1U << 31);
}

/// <summary>
/// Hamlib rig_caps field access constants (for rig_get_caps_cptr)
/// </summary>
public static class RigCapsField
{
    public const int RIG_CAPS_MFG_NAME_CPTR = 1;
    public const int RIG_CAPS_MODEL_NAME_CPTR = 2;
    public const int RIG_CAPS_VERSION_CPTR = 3;
}

/// <summary>
/// Well-known rig model IDs
/// </summary>
public static class RigModel
{
    public const int RIG_MODEL_DUMMY = 1;
    public const int RIG_MODEL_NETRIGCTL = 2;
}

/// <summary>
/// Serial handshake/flow control
/// </summary>
public enum SerialHandshake
{
    None = 0,
    XonXoff = 1,
    Hardware = 2
}

/// <summary>
/// Serial parity
/// </summary>
public enum SerialParity
{
    None = 0,
    Odd = 1,
    Even = 2,
    Mark = 3,
    Space = 4
}
