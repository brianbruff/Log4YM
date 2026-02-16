namespace Log4YM.Server.Native.Hamlib;

/// <summary>
/// Connection type for Hamlib rig
/// </summary>
public enum HamlibConnectionType
{
    Serial,
    Network
}

/// <summary>
/// Serial data bits options
/// </summary>
public enum HamlibDataBits
{
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8
}

/// <summary>
/// Serial stop bits options
/// </summary>
public enum HamlibStopBits
{
    One = 1,
    Two = 2
}

/// <summary>
/// Serial flow control options
/// </summary>
public enum HamlibFlowControl
{
    None,
    Hardware,
    Software
}

/// <summary>
/// Serial parity options
/// </summary>
public enum HamlibParity
{
    None,
    Even,
    Odd,
    Mark,
    Space
}

/// <summary>
/// PTT control type
/// </summary>
public enum HamlibPttType
{
    None,
    Rig,  // CAT control
    Dtr,
    Rts
}

/// <summary>
/// Configuration for Hamlib rig connection
/// Stored in MongoDB as single config document
/// </summary>
public record HamlibRigConfig
{
    /// <summary>
    /// Hamlib rig model ID
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// Display name for the rig model
    /// </summary>
    public string ModelName { get; init; } = "";

    /// <summary>
    /// Connection type: Serial or Network
    /// </summary>
    public HamlibConnectionType ConnectionType { get; init; } = HamlibConnectionType.Serial;

    // =========================================================================
    // Serial Settings
    // =========================================================================

    /// <summary>
    /// Serial port path (e.g., /dev/ttyUSB0, COM1)
    /// </summary>
    public string? SerialPort { get; init; }

    /// <summary>
    /// Serial baud rate
    /// </summary>
    public int BaudRate { get; init; } = 9600;

    /// <summary>
    /// Serial data bits
    /// </summary>
    public HamlibDataBits DataBits { get; init; } = HamlibDataBits.Eight;

    /// <summary>
    /// Serial stop bits
    /// </summary>
    public HamlibStopBits StopBits { get; init; } = HamlibStopBits.One;

    /// <summary>
    /// Serial flow control
    /// </summary>
    public HamlibFlowControl FlowControl { get; init; } = HamlibFlowControl.None;

    /// <summary>
    /// Serial parity
    /// </summary>
    public HamlibParity Parity { get; init; } = HamlibParity.None;

    // =========================================================================
    // Network Settings
    // =========================================================================

    /// <summary>
    /// Network hostname or IP address
    /// </summary>
    public string? Hostname { get; init; }

    /// <summary>
    /// Network port (default 4532 for rigctld)
    /// </summary>
    public int NetworkPort { get; init; } = 4532;

    // =========================================================================
    // PTT Configuration
    // =========================================================================

    /// <summary>
    /// PTT control method
    /// </summary>
    public HamlibPttType PttType { get; init; } = HamlibPttType.Rig;

    /// <summary>
    /// Alternate port for DTR/RTS PTT control
    /// </summary>
    public string? PttPort { get; init; }

    // =========================================================================
    // Feature Toggles
    // =========================================================================

    /// <summary>
    /// Enable reading frequency from rig
    /// </summary>
    public bool GetFrequency { get; init; } = true;

    /// <summary>
    /// Enable reading mode from rig
    /// </summary>
    public bool GetMode { get; init; } = true;

    /// <summary>
    /// Enable reading VFO from rig
    /// </summary>
    public bool GetVfo { get; init; } = true;

    /// <summary>
    /// Enable reading PTT state from rig
    /// </summary>
    public bool GetPtt { get; init; } = true;

    /// <summary>
    /// Enable reading power from rig
    /// </summary>
    public bool GetPower { get; init; } = false;

    /// <summary>
    /// Enable reading RIT offset from rig
    /// </summary>
    public bool GetRit { get; init; } = false;

    /// <summary>
    /// Enable reading XIT offset from rig
    /// </summary>
    public bool GetXit { get; init; } = false;

    /// <summary>
    /// Enable reading CW key speed from rig
    /// </summary>
    public bool GetKeySpeed { get; init; } = false;

    // =========================================================================
    // Polling Configuration
    // =========================================================================

    /// <summary>
    /// Polling interval in milliseconds
    /// </summary>
    public int PollIntervalMs { get; init; } = 250;

    /// <summary>
    /// Convert PTT type to Hamlib string
    /// </summary>
    public string GetPttTypeString() => PttType switch
    {
        HamlibPttType.Rig => "RIG",
        HamlibPttType.Dtr => "DTR",
        HamlibPttType.Rts => "RTS",
        _ => "None"
    };

    /// <summary>
    /// Convert flow control to SerialHandshake enum
    /// </summary>
    public SerialHandshake GetSerialHandshake() => FlowControl switch
    {
        HamlibFlowControl.Hardware => SerialHandshake.Hardware,
        HamlibFlowControl.Software => SerialHandshake.XonXoff,
        _ => SerialHandshake.None
    };

    /// <summary>
    /// Convert parity to SerialParity enum
    /// </summary>
    public SerialParity GetSerialParity() => Parity switch
    {
        HamlibParity.Even => Native.Hamlib.SerialParity.Even,
        HamlibParity.Odd => Native.Hamlib.SerialParity.Odd,
        HamlibParity.Mark => Native.Hamlib.SerialParity.Mark,
        HamlibParity.Space => Native.Hamlib.SerialParity.Space,
        _ => Native.Hamlib.SerialParity.None
    };
}

/// <summary>
/// Available baud rates for UI
/// </summary>
public static class HamlibBaudRates
{
    public static readonly int[] Values = { 1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200 };
}
