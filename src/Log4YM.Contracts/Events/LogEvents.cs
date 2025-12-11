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
    string? ImageUrl
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
    double Azimuth,
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
