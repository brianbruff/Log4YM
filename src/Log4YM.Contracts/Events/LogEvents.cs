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
    int? Dxcc
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

// ===== Radio CAT Control Events =====

/// <summary>
/// Type of radio/protocol
/// </summary>
public enum RadioType
{
    FlexRadio,
    Tci
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
