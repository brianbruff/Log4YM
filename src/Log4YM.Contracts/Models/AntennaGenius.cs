namespace Log4YM.Contracts.Models;

/// <summary>
/// Information about an Antenna Genius device discovered on the network
/// </summary>
public record AntennaGeniusDevice(
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
/// Antenna configuration from the device
/// </summary>
public record AntennaInfo(
    int Id,
    string Name,
    ushort TxBandMask,
    ushort RxBandMask,
    ushort InbandMask
);

/// <summary>
/// Band configuration from the device
/// </summary>
public record BandInfo(
    int Id,
    string Name,
    double FreqStart,
    double FreqStop
);

/// <summary>
/// Radio port status (A=1, B=2)
/// </summary>
public record PortStatus(
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
/// Complete status of an Antenna Genius device
/// </summary>
public record AntennaGeniusStatus(
    string DeviceSerial,
    string DeviceName,
    string IpAddress,
    string Version,
    bool IsConnected,
    List<AntennaInfo> Antennas,
    List<BandInfo> Bands,
    PortStatus PortA,
    PortStatus PortB
);
