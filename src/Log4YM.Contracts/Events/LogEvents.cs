namespace Log4YM.Contracts.Events;

/// <summary>
/// Emitted when user focuses on a callsign (typing, clicking spot, etc.)
/// </summary>
public record CallsignFocusedEvent(
    string Callsign,
    string Source,
    string? Grid = null,
    double? Frequency = null,
    string? Mode = null
);

/// <summary>
/// Emitted after successful QRZ/callbook lookup
/// </summary>
public record CallsignLookedUpEvent(
    string Callsign,
    string? Name,
    string? Grid,
    double? Latitude,
    double? Longitude,
    string? Country,
    int? Dxcc,
    int? CqZone,
    int? ItuZone,
    string? State,
    string? ImageUrl,
    double? Bearing = null,
    double? Distance = null
);

/// <summary>
/// Emitted when a QSO is logged
/// </summary>
public record QsoLoggedEvent(
    string Id,
    string Callsign,
    DateTime QsoDate,
    string TimeOn,
    string Band,
    string Mode,
    double? Frequency,
    string? RstSent,
    string? RstRcvd,
    string? Grid
);

/// <summary>
/// Current station location
/// </summary>
public record StationLocationEvent(
    string Callsign,
    string Grid,
    double Latitude,
    double Longitude
);

/// <summary>
/// New DX spot received
/// </summary>
public record SpotReceivedEvent(
    string Id,
    string DxCall,
    string Spotter,
    double Frequency,
    string? Mode,
    string? Comment,
    DateTime Timestamp,
    string Source,
    string? Country,
    int? Dxcc,
    string? Grid
);

/// <summary>
/// User clicked on a spot
/// </summary>
public record SpotSelectedEvent(
    string DxCall,
    double Frequency,
    string? Mode,
    string? Grid
);

/// <summary>
/// Current rotator position
/// </summary>
public record RotatorPositionEvent(
    string RotatorId,
    double CurrentAzimuth,
    bool IsMoving,
    double? TargetAzimuth = null
);

/// <summary>
/// Request to move rotator
/// </summary>
public record RotatorCommandEvent(
    string RotatorId,
    double TargetAzimuth,
    string Source
);

/// <summary>
/// Current rig frequency/mode
/// </summary>
public record RigStatusEvent(
    string RigId,
    double Frequency,
    string Mode,
    bool IsTransmitting
);

// ===== Antenna Genius Events =====

/// <summary>
/// Antenna Genius device discovered on network
/// </summary>
public record AntennaGeniusDiscoveredEvent(
    string IpAddress,
    int Port,
    string Version,
    string Serial,
    string Name,
    int RadioPorts,
    int AntennaPorts,
    string Mode,
    int Uptime
);

/// <summary>
/// Antenna Genius device disconnected
/// </summary>
public record AntennaGeniusDisconnectedEvent(
    string Serial
);

/// <summary>
/// Full status update from Antenna Genius
/// </summary>
public record AntennaGeniusStatusEvent(
    string DeviceSerial,
    string DeviceName,
    string IpAddress,
    string Version,
    bool IsConnected,
    List<AntennaGeniusAntennaInfo> Antennas,
    List<AntennaGeniusBandInfo> Bands,
    AntennaGeniusPortStatus PortA,
    AntennaGeniusPortStatus PortB
);

/// <summary>
/// Antenna info for events (without BSON attributes)
/// </summary>
public record AntennaGeniusAntennaInfo(
    int Id,
    string Name,
    ushort TxBandMask,
    ushort RxBandMask,
    ushort InbandMask
);

/// <summary>
/// Band info for events
/// </summary>
public record AntennaGeniusBandInfo(
    int Id,
    string Name,
    double FreqStart,
    double FreqStop
);

/// <summary>
/// Port status for events
/// </summary>
public record AntennaGeniusPortStatus(
    int PortId,
    bool Auto,
    string Source,
    int Band,
    int RxAntenna,
    int TxAntenna,
    bool IsTransmitting,
    bool IsInhibited
);

/// <summary>
/// Port status changed (antenna selection, band change, etc.)
/// </summary>
public record AntennaGeniusPortChangedEvent(
    string DeviceSerial,
    int PortId,
    bool Auto,
    string Source,
    int Band,
    int RxAntenna,
    int TxAntenna,
    bool IsTransmitting,
    bool IsInhibited
);

/// <summary>
/// Request to select antenna for a port (client to server)
/// </summary>
public record SelectAntennaCommand(
    string DeviceSerial,
    int PortId,
    int AntennaId
);

// ===== PGXL Amplifier Events =====

/// <summary>
/// PGXL amplifier discovered on network
/// </summary>
public record PgxlDiscoveredEvent(
    string IpAddress,
    int Port,
    string Serial,
    string Model
);

/// <summary>
/// PGXL amplifier disconnected
/// </summary>
public record PgxlDisconnectedEvent(
    string Serial
);

/// <summary>
/// Full status update from PGXL amplifier
/// </summary>
public record PgxlStatusEvent(
    string Serial,
    string IpAddress,
    bool IsConnected,
    bool IsOperating,
    bool IsTransmitting,
    string Band,
    string BiasA,
    string BiasB,
    PgxlMeters Meters,
    PgxlSetup Setup
);

/// <summary>
/// PGXL meter readings
/// </summary>
public record PgxlMeters(
    double ForwardPowerDbm,
    double ForwardPowerWatts,
    double ReturnLossDb,
    double SwrRatio,
    double DrivePowerDbm,
    double PaCurrent,
    double TemperatureC
);

/// <summary>
/// PGXL setup/configuration
/// </summary>
public record PgxlSetup(
    string BandSource,
    int SelectedAntenna,
    bool AttenuatorEnabled,
    int BiasOffset,
    int PttDelay,
    int KeyDelay,
    bool HighSwr,
    bool OverTemp,
    bool OverCurrent
);

/// <summary>
/// Request to set PGXL operate mode (client to server)
/// </summary>
public record SetPgxlOperateCommand(
    string Serial
);

/// <summary>
/// Request to set PGXL standby mode (client to server)
/// </summary>
public record SetPgxlStandbyCommand(
    string Serial
);

/// <summary>
/// Request to disable FlexRadio pairing for a PGXL slice (client to server)
/// </summary>
public record DisablePgxlFlexRadioPairingCommand(
    string Serial,
    string Slice
);

// ===== Radio CAT Control Events =====

/// <summary>
/// Type of radio/protocol
/// </summary>
public enum RadioType
{
    FlexRadio,
    Tci,
    Hamlib
}

/// <summary>
/// Radio connection state
/// </summary>
public enum RadioConnectionState
{
    Disconnected,
    Discovering,
    Connecting,
    Connected,
    Monitoring,
    Error
}

/// <summary>
/// Radio discovered on network
/// </summary>
public record RadioDiscoveredEvent(
    string Id,
    RadioType Type,
    string Model,
    string IpAddress,
    int Port,
    string? Nickname,
    List<string>? Slices  // For FlexRadio - available slices
);

/// <summary>
/// Radio no longer available
/// </summary>
public record RadioRemovedEvent(
    string Id
);

/// <summary>
/// Radio connection state changed
/// </summary>
public record RadioConnectionStateChangedEvent(
    string RadioId,
    RadioConnectionState State,
    string? ErrorMessage = null
);

/// <summary>
/// Radio frequency/mode/TX state update
/// </summary>
public record RadioStateChangedEvent(
    string RadioId,
    long FrequencyHz,
    string Mode,
    bool IsTransmitting,
    string Band,
    string? SliceOrInstance
);

/// <summary>
/// Available slices updated (FlexRadio)
/// </summary>
public record RadioSlicesUpdatedEvent(
    string RadioId,
    List<RadioSliceInfo> Slices
);

/// <summary>
/// Slice information for FlexRadio
/// </summary>
public record RadioSliceInfo(
    string Id,
    string Letter,      // A, B, C, D
    long FrequencyHz,
    string Mode,
    bool IsActive
);

/// <summary>
/// Command to start radio discovery
/// </summary>
public record StartRadioDiscoveryCommand(
    RadioType Type
);

/// <summary>
/// Command to stop radio discovery
/// </summary>
public record StopRadioDiscoveryCommand(
    RadioType Type
);

/// <summary>
/// Command to connect to a radio
/// </summary>
public record ConnectRadioCommand(
    string RadioId
);

/// <summary>
/// Command to disconnect from a radio
/// </summary>
public record DisconnectRadioCommand(
    string RadioId
);

/// <summary>
/// Command to select a slice to monitor (FlexRadio)
/// </summary>
public record SelectRadioSliceCommand(
    string RadioId,
    string SliceId
);

/// <summary>
/// Command to select an instance to monitor (TCI)
/// </summary>
public record SelectRadioInstanceCommand(
    string RadioId,
    int Instance
);

// ===== CW Keyer Commands and Events =====

/// <summary>
/// Command to send CW/Morse code text
/// </summary>
public record SendCwKeyCommand(
    string RadioId,
    string Message,
    int? SpeedWpm = null
);

/// <summary>
/// Command to stop CW keying immediately
/// </summary>
public record StopCwKeyCommand(
    string RadioId
);

/// <summary>
/// Command to set CW keyer speed
/// </summary>
public record SetCwSpeedCommand(
    string RadioId,
    int SpeedWpm
);

/// <summary>
/// CW keyer status event
/// </summary>
public record CwKeyerStatusEvent(
    string RadioId,
    bool IsKeying,
    int SpeedWpm,
    string? CurrentMessage = null
);

// ===== Hamlib Configuration Events =====

/// <summary>
/// Hamlib connection type
/// </summary>
public enum HamlibConnectionType
{
    Serial,
    Network
}

/// <summary>
/// Hamlib data bits options
/// </summary>
public enum HamlibDataBits
{
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8
}

/// <summary>
/// Hamlib stop bits options
/// </summary>
public enum HamlibStopBits
{
    One = 1,
    Two = 2
}

/// <summary>
/// Hamlib flow control options
/// </summary>
public enum HamlibFlowControl
{
    None,
    Hardware,
    Software
}

/// <summary>
/// Hamlib parity options
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
/// Hamlib PTT type
/// </summary>
public enum HamlibPttType
{
    None,
    Rig,
    Dtr,
    Rts
}

/// <summary>
/// Information about a Hamlib rig model
/// </summary>
public record HamlibRigModelInfo(
    int ModelId,
    string Manufacturer,
    string Model,
    string Version,
    string DisplayName
);

/// <summary>
/// Hamlib rig capabilities
/// </summary>
public record HamlibRigCapabilities(
    bool CanGetFreq,
    bool CanGetMode,
    bool CanGetVfo,
    bool CanGetPtt,
    bool CanGetPower,
    bool CanGetRit,
    bool CanGetXit,
    bool CanGetKeySpeed,
    bool CanSendMorse,
    int DefaultDataBits,
    int DefaultStopBits,
    bool IsNetworkOnly,
    bool SupportsSerial,
    bool SupportsNetwork
);

/// <summary>
/// Hamlib rig configuration
/// </summary>
public record HamlibRigConfigDto(
    int ModelId,
    string ModelName,
    HamlibConnectionType ConnectionType,
    string? SerialPort,
    int BaudRate,
    HamlibDataBits DataBits,
    HamlibStopBits StopBits,
    HamlibFlowControl FlowControl,
    HamlibParity Parity,
    string? Hostname,
    int NetworkPort,
    HamlibPttType PttType,
    string? PttPort,
    bool GetFrequency,
    bool GetMode,
    bool GetVfo,
    bool GetPtt,
    bool GetPower,
    bool GetRit,
    bool GetXit,
    bool GetKeySpeed,
    int PollIntervalMs
);

/// <summary>
/// Hamlib rig list response
/// </summary>
public record HamlibRigListEvent(
    List<HamlibRigModelInfo> Rigs
);

/// <summary>
/// Hamlib rig capabilities response
/// </summary>
public record HamlibRigCapsEvent(
    int ModelId,
    HamlibRigCapabilities Capabilities
);

/// <summary>
/// Available serial ports
/// </summary>
public record HamlibSerialPortsEvent(
    List<string> Ports
);

/// <summary>
/// Hamlib configuration loaded
/// </summary>
public record HamlibConfigLoadedEvent(
    HamlibRigConfigDto? Config
);

/// <summary>
/// Hamlib library initialization status
/// </summary>
public record HamlibStatusEvent(
    bool IsInitialized,
    bool IsConnected,
    string? RadioId,
    string? ErrorMessage
);

// ===== SmartUnlink Events =====

/// <summary>
/// Known FlexRadio models for SmartUnlink
/// </summary>
public static class FlexRadioModels
{
    public static readonly string[] All = new[]
    {
        "FLEX-5100",   // Aurora series
        "FLEX-5200",   // Aurora series
        "FLEX-6400",   // Signature series
        "FLEX-6400M",  // Signature series with ATU
        "FLEX-6600",   // Signature series
        "FLEX-6600M",  // Signature series with ATU
        "FLEX-6700",   // Signature series
        "FLEX-8400",   // Maestro series
        "FLEX-8600",   // Maestro series
        "FlexRadio",   // Generic placeholder
    };
}

/// <summary>
/// SmartUnlink radio configuration
/// </summary>
public record SmartUnlinkRadio(
    string Id,
    string Name,
    string IpAddress,
    string Model,
    string SerialNumber,
    string? Callsign,
    bool Enabled,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>
/// SmartUnlink radio configuration DTO for API
/// </summary>
public record SmartUnlinkRadioDto(
    string? Id,
    string Name,
    string IpAddress,
    string Model,
    string SerialNumber,
    string? Callsign,
    bool Enabled,
    string Version = "4.1.3.39644"
);

/// <summary>
/// SmartUnlink radio added event
/// </summary>
public record SmartUnlinkRadioAddedEvent(
    string Id,
    string Name,
    string IpAddress,
    string Model,
    string SerialNumber,
    string? Callsign,
    bool Enabled,
    string Version = "4.1.3.39644"
);

/// <summary>
/// SmartUnlink radio updated event
/// </summary>
public record SmartUnlinkRadioUpdatedEvent(
    string Id,
    string Name,
    string IpAddress,
    string Model,
    string SerialNumber,
    string? Callsign,
    bool Enabled,
    string Version = "4.1.3.39644"
);

/// <summary>
/// SmartUnlink radio removed event
/// </summary>
public record SmartUnlinkRadioRemovedEvent(
    string Id
);

/// <summary>
/// SmartUnlink broadcast status event
/// </summary>
public record SmartUnlinkBroadcastStatusEvent(
    string RadioId,
    bool IsBroadcasting,
    DateTime? LastBroadcast
);

/// <summary>
/// Full SmartUnlink status for initial load
/// </summary>
public record SmartUnlinkStatusEvent(
    List<SmartUnlinkRadioAddedEvent> Radios
);

// ===== DX Cluster Events =====

/// <summary>
/// DX Cluster connection status changed
/// </summary>
public record ClusterStatusChangedEvent(
    string ClusterId,
    string Name,
    string Status,  // "connected" | "connecting" | "disconnected" | "error"
    string? ErrorMessage = null
);

// ===== QRZ Sync Events =====

/// <summary>
/// QRZ sync progress update
/// </summary>
public record QrzSyncProgressEvent(
    int Total,
    int Completed,
    int Successful,
    int Failed,
    bool IsComplete,
    string? CurrentCallsign,
    string? Message
);

// ===== Tuner Genius Events =====

/// <summary>
/// Tuner Genius device discovered on network
/// </summary>
public record TunerGeniusDiscoveredEvent(
    string IpAddress,
    int Port,
    string Version,
    string Serial,
    string Name,
    string Model,
    int Uptime
);

/// <summary>
/// Tuner Genius device disconnected
/// </summary>
public record TunerGeniusDisconnectedEvent(
    string Serial
);

/// <summary>
/// Full status update from Tuner Genius XL.
/// The TGXL is a single tuner with one L/C network serving up to two radios.
/// Swr, Power, L/C positions and bypass/operate state are tuner-level (not per-radio).
/// FreqAMhz / FreqBMhz are the frequencies reported by the two radio inputs.
/// </summary>
public record TunerGeniusStatusEvent(
    string DeviceSerial,
    string DeviceName,
    string IpAddress,
    string Version,
    string Model,
    bool IsConnected,
    // Tuner state
    bool IsOperating,          // true = Operate, false = Standby
    bool IsBypassed,           // true = tuner bypassed (out of circuit)
    bool IsTuning,             // true = tune cycle in progress
    int ActiveRadio,           // 1 or 2
    // Metering
    double ForwardPowerWatts,
    double Swr,                // e.g. 1.5
    // Matching network positions (0-255)
    int L,
    int C1,
    int C2,
    // Per-radio frequency inputs
    double FreqAMhz,
    double FreqBMhz,
    // Legacy port wrappers (PortA = Radio 1, PortB = Radio 2)
    TunerGeniusPortStatus PortA,
    TunerGeniusPortStatus? PortB
);

/// <summary>
/// Per-radio input status (frequency and band).
/// L/C/SWR/power are tuner-level — see TunerGeniusStatusEvent.
/// </summary>
public record TunerGeniusPortStatus(
    int PortId,
    bool Auto,
    string Band,
    double FrequencyMhz,
    int Swr,               // SWR * 10 (e.g. 15 = 1.5:1) — kept for UI compat
    bool IsTuning,
    bool IsTransmitting,
    int? SelectedAntenna,
    string TuneResult      // "OK", "HighSWR", "Timeout", "Error"
);

/// <summary>
/// Fired on every status poll — carries full tuner + radio state.
/// </summary>
public record TunerGeniusPortChangedEvent(
    string DeviceSerial,
    int PortId,
    bool Auto,
    string Band,
    double FrequencyMhz,
    int Swr,
    bool IsTuning,
    bool IsTransmitting,
    int? SelectedAntenna,
    string TuneResult,
    // Tuner-level additions
    bool IsBypassed,
    bool IsOperating,
    double ForwardPowerWatts,
    double SwrDecimal,     // SWR as double e.g. 1.5
    int L,
    int C1,
    int C2,
    int ActiveRadio
);

/// <summary>
/// Command to initiate auto-tune (client to server)
/// </summary>
public record TuneTunerGeniusCommand(
    string DeviceSerial,
    int PortId
);

/// <summary>
/// Command to toggle bypass state (client to server)
/// </summary>
public record BypassTunerGeniusCommand(
    string DeviceSerial,
    int PortId,
    bool Bypass
);

/// <summary>
/// Command to set Operate / Standby mode (client to server)
/// </summary>
public record OperateTunerGeniusCommand(
    string DeviceSerial,
    bool Operate
);

/// <summary>
/// Command to activate a radio channel (client to server)
/// </summary>
public record ActivateChannelTunerGeniusCommand(
    string DeviceSerial,
    int Channel   // 1 or 2
);
