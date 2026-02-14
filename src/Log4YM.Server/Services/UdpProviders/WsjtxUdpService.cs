using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services.UdpProviders;

/// <summary>
/// WSJT-X/JTDX/MSHV UDP Protocol Service
/// Supports receiving status, decode, and QSO logged messages from digital mode applications
/// Compatible with WSJT-X protocol specification (magic: 0xadbccbda)
/// </summary>
public class WsjtxUdpService : BackgroundService
{
    private readonly ILogger<WsjtxUdpService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly EventBus _eventBus;
    private readonly SettingsService _settingsService;

    private UdpClient? _udpClient;
    private UdpProviderConfig? _config;
    private CancellationTokenSource? _listenerCts;

    private const uint MagicNumber = 0xadbccbda;
    private const int SchemaVersion = 3; // Using schema version 3 (Qt 5.4)

    // Message type constants
    private const uint MessageTypeHeartbeat = 0;
    private const uint MessageTypeStatus = 1;
    private const uint MessageTypeDecode = 2;
    private const uint MessageTypeClear = 3;
    private const uint MessageTypeReply = 4;
    private const uint MessageTypeQsoLogged = 5;
    private const uint MessageTypeClose = 6;
    private const uint MessageTypeReplay = 7;
    private const uint MessageTypeHaltTx = 8;
    private const uint MessageTypeFreeText = 9;
    private const uint MessageTypeWsprDecode = 10;
    private const uint MessageTypeLocation = 11;
    private const uint MessageTypeLoggedAdif = 12;
    private const uint MessageTypeHighlightCallsign = 13;

    public WsjtxUdpService(
        ILogger<WsjtxUdpService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        EventBus eventBus,
        SettingsService settingsService)
    {
        _logger = logger;
        _hubContext = hubContext;
        _eventBus = eventBus;
        _settingsService = settingsService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WSJT-X UDP service starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await LoadConfigurationAsync();

                if (_config?.Enabled == true)
                {
                    await StartListenerAsync(stoppingToken);
                }
                else
                {
                    await StopListenerAsync();
                }

                // Check for configuration changes every 5 seconds
                await Task.Delay(5000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WSJT-X UDP service");
                await Task.Delay(5000, stoppingToken);
            }
        }

        await StopListenerAsync();
        _logger.LogInformation("WSJT-X UDP service stopped");
    }

    private async Task LoadConfigurationAsync()
    {
        var settings = await _settingsService.GetSettingsAsync();
        var newConfig = settings.UdpProviders.Providers.FirstOrDefault(p => p.Id == "wsjtx");

        // Check if configuration changed
        if (newConfig != null && !ConfigEquals(_config, newConfig))
        {
            _logger.LogInformation("WSJT-X configuration changed, reloading...");
            _config = newConfig;

            // Restart listener if already running
            if (_udpClient != null)
            {
                await StopListenerAsync();
            }
        }
    }

    private bool ConfigEquals(UdpProviderConfig? a, UdpProviderConfig? b)
    {
        if (a == null || b == null) return a == b;
        return a.Enabled == b.Enabled &&
               a.Port == b.Port &&
               a.MulticastEnabled == b.MulticastEnabled &&
               a.MulticastAddress == b.MulticastAddress;
    }

    private async Task StartListenerAsync(CancellationToken ct)
    {
        if (_udpClient != null || _config == null) return;

        try
        {
            _listenerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            if (_config.MulticastEnabled && !string.IsNullOrEmpty(_config.MulticastAddress))
            {
                // Multicast mode
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
                var multicastAddress = IPAddress.Parse(_config.MulticastAddress);
                _udpClient.JoinMulticastGroup(multicastAddress, _config.MulticastTtl);

                _logger.LogInformation(
                    "WSJT-X listening on multicast {Address}:{Port} (TTL: {Ttl})",
                    _config.MulticastAddress, _config.Port, _config.MulticastTtl);
            }
            else
            {
                // Unicast mode
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
                _logger.LogInformation("WSJT-X listening on UDP port {Port}", _config.Port);
            }

            await PublishStatusEvent(true, null);

            // Start listener task
            _ = Task.Run(async () => await RunListenerAsync(_listenerCts.Token), _listenerCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start WSJT-X UDP listener");
            await PublishStatusEvent(false, ex.Message);
            await StopListenerAsync();
        }
    }

    private async Task StopListenerAsync()
    {
        if (_udpClient == null) return;

        try
        {
            _listenerCts?.Cancel();
            _udpClient?.Close();
            _udpClient?.Dispose();
            _udpClient = null;
            _listenerCts?.Dispose();
            _listenerCts = null;

            await PublishStatusEvent(false, null);
            _logger.LogInformation("WSJT-X UDP listener stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping WSJT-X UDP listener");
        }
    }

    private async Task RunListenerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udpClient != null)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(ct);
                await ProcessDatagramAsync(result.Buffer, result.RemoteEndPoint);

                // Forward datagram if enabled
                if (_config?.ForwardingEnabled == true && _config.ForwardingAddresses.Any())
                {
                    await ForwardDatagramAsync(result.Buffer);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving WSJT-X UDP datagram");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessDatagramAsync(byte[] data, IPEndPoint sender)
    {
        if (data.Length < 12) return; // Minimum header size

        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream);

        // Read header
        var magic = ReadUInt32BigEndian(reader);
        if (magic != MagicNumber)
        {
            _logger.LogDebug("Invalid WSJT-X magic number: 0x{Magic:X8}", magic);
            return;
        }

        var schema = ReadUInt32BigEndian(reader);
        var messageType = ReadUInt32BigEndian(reader);

        _logger.LogDebug("WSJT-X message: type={Type}, schema={Schema}", messageType, schema);

        try
        {
            switch (messageType)
            {
                case MessageTypeStatus:
                    await HandleStatusMessage(reader);
                    break;

                case MessageTypeDecode:
                    await HandleDecodeMessage(reader);
                    break;

                case MessageTypeQsoLogged:
                    await HandleQsoLoggedMessage(reader);
                    break;

                case MessageTypeLoggedAdif:
                    await HandleLoggedAdifMessage(reader);
                    break;

                default:
                    _logger.LogDebug("Unhandled WSJT-X message type: {Type}", messageType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing WSJT-X message type {Type}", messageType);
        }
    }

    private async Task HandleStatusMessage(BinaryReader reader)
    {
        var id = ReadQString(reader);
        var dialFrequency = ReadUInt64BigEndian(reader);
        var mode = ReadQString(reader);
        var dxCall = ReadQString(reader);
        var report = ReadQString(reader);
        var txMode = ReadQString(reader);
        var txEnabled = reader.ReadBoolean();
        var transmitting = reader.ReadBoolean();
        var decoding = reader.ReadBoolean();
        var rxDf = ReadUInt32BigEndian(reader);
        var txDf = ReadUInt32BigEndian(reader);
        var deCall = ReadQString(reader);
        var deGrid = ReadQString(reader);
        var dxGrid = ReadQString(reader);
        var txWatchdog = reader.ReadBoolean();
        var subMode = ReadQString(reader);
        var fastMode = reader.ReadBoolean();
        var specialOpMode = reader.ReadByte();
        var freqTolerance = ReadUInt32BigEndian(reader);
        var trPeriod = ReadUInt32BigEndian(reader);
        var confName = ReadQString(reader);
        var txMessage = ReadQString(reader);

        var evt = new WsjtxStatusReceivedEvent(
            Id: id,
            DialFrequency: (long)dialFrequency,
            Mode: mode,
            DxCall: dxCall,
            DxGrid: dxGrid,
            DeCall: deCall,
            DeGrid: deGrid,
            TxEnabled: txEnabled,
            Transmitting: transmitting,
            Decoding: decoding,
            RxDf: (int)rxDf,
            TxDf: (int)txDf,
            SubMode: subMode,
            FreqTolerance: (int)freqTolerance,
            TrPeriod: trPeriod,
            Report: report,
            TxMessage: txMessage
        );

        _eventBus.Publish(evt);
        await _hubContext.Clients.All.OnWsjtxStatusReceived(evt);
    }

    private async Task HandleDecodeMessage(BinaryReader reader)
    {
        var id = ReadQString(reader);
        var isNew = reader.ReadBoolean();
        var time = ReadQTime(reader);
        var snr = ReadInt32BigEndian(reader);
        var deltaTime = ReadDoubleBigEndian(reader);
        var deltaFrequency = ReadUInt32BigEndian(reader);
        var mode = ReadQString(reader);
        var message = ReadQString(reader);
        var lowConfidence = reader.ReadBoolean();
        var offAir = reader.ReadBoolean();

        var evt = new WsjtxDecodeReceivedEvent(
            Id: id,
            IsNew: isNew,
            Time: time,
            Snr: snr,
            DeltaTime: deltaTime,
            DeltaFrequency: (int)deltaFrequency,
            Mode: mode,
            Message: message,
            LowConfidence: lowConfidence,
            OffAir: offAir
        );

        _eventBus.Publish(evt);
        await _hubContext.Clients.All.OnWsjtxDecodeReceived(evt);
    }

    private async Task HandleQsoLoggedMessage(BinaryReader reader)
    {
        var id = ReadQString(reader);
        var timeOff = ReadQDateTime(reader);
        var dxCall = ReadQString(reader);
        var dxGrid = ReadQString(reader);
        var txFrequency = ReadUInt64BigEndian(reader);
        var mode = ReadQString(reader);
        var reportSent = ReadQString(reader);
        var reportReceived = ReadQString(reader);
        var txPower = ReadQString(reader);
        var comments = ReadQString(reader);
        var name = ReadQString(reader);
        var timeOn = ReadQDateTime(reader);
        var operatorCall = ReadQString(reader);
        var myCall = ReadQString(reader);
        var myGrid = ReadQString(reader);
        var exchangeSent = ReadQString(reader);
        var exchangeReceived = ReadQString(reader);
        var propMode = ReadQString(reader);

        var evt = new WsjtxQsoLoggedEvent(
            Id: id,
            DxCall: dxCall,
            DxGrid: dxGrid,
            TxFrequency: (long)txFrequency,
            Mode: mode,
            ReportSent: reportSent,
            ReportReceived: reportReceived,
            TxPower: txPower,
            TimeOn: timeOn,
            TimeOff: timeOff,
            MyCall: myCall,
            MyGrid: myGrid,
            Comments: string.IsNullOrEmpty(comments) ? null : comments,
            ExchangeSent: string.IsNullOrEmpty(exchangeSent) ? null : exchangeSent,
            ExchangeReceived: string.IsNullOrEmpty(exchangeReceived) ? null : exchangeReceived
        );

        _eventBus.Publish(evt);
        await _hubContext.Clients.All.OnWsjtxQsoLogged(evt);
    }

    private async Task HandleLoggedAdifMessage(BinaryReader reader)
    {
        var id = ReadQString(reader);
        var adifText = ReadQString(reader);

        _logger.LogInformation("WSJT-X ADIF QSO logged from {Id}: {Length} bytes", id, adifText.Length);

        // TODO: Parse ADIF and emit QSO event
        // For now, just log it
    }

    private async Task ForwardDatagramAsync(byte[] data)
    {
        if (_config?.ForwardingAddresses == null) return;

        foreach (var addressStr in _config.ForwardingAddresses)
        {
            try
            {
                var parts = addressStr.Split(':', 2);
                if (parts.Length != 2) continue;

                var host = parts[0];
                var port = int.Parse(parts[1]);

                using var forwardClient = new UdpClient();
                await forwardClient.SendAsync(data, data.Length, host, port);

                _logger.LogDebug("Forwarded WSJT-X datagram to {Address}", addressStr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error forwarding WSJT-X datagram to {Address}", addressStr);
            }
        }
    }

    private async Task PublishStatusEvent(bool isRunning, string? errorMessage)
    {
        var evt = new UdpProviderStatusChangedEvent(
            ProviderId: "wsjtx",
            ProviderName: "WSJT-X / JTDX / MSHV",
            IsRunning: isRunning,
            IsListening: isRunning,
            ListeningPort: _config?.Port,
            IsMulticastEnabled: _config?.MulticastEnabled,
            ErrorMessage: errorMessage
        );

        _eventBus.Publish(evt);
        await _hubContext.Clients.All.OnUdpProviderStatusChanged(evt);
    }

    // Binary reading helpers (WSJT-X uses big-endian)

    private static uint ReadUInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static ulong ReadUInt64BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static int ReadInt32BigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static double ReadDoubleBigEndian(BinaryReader reader)
    {
        var bytes = reader.ReadBytes(8);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToDouble(bytes, 0);
    }

    private static string ReadQString(BinaryReader reader)
    {
        var length = ReadUInt32BigEndian(reader);
        if (length == 0xFFFFFFFF) return string.Empty; // Null string in Qt
        if (length == 0) return string.Empty;

        var bytes = reader.ReadBytes((int)length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static DateTime ReadQDateTime(BinaryReader reader)
    {
        var julianDay = ReadUInt64BigEndian(reader);
        var msecsSinceMidnight = ReadUInt32BigEndian(reader);
        var timeSpec = reader.ReadByte();

        // Qt Julian Day starts from November 24, 4714 BCE
        // Convert to .NET DateTime
        var baseDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var days = (long)julianDay - 2440588; // Convert Qt Julian Day to Unix epoch days
        var dt = baseDate.AddDays(days).AddMilliseconds(msecsSinceMidnight);

        return dt;
    }

    private static DateTime ReadQTime(BinaryReader reader)
    {
        var msecsSinceMidnight = ReadUInt32BigEndian(reader);
        var today = DateTime.UtcNow.Date;
        return today.AddMilliseconds(msecsSinceMidnight);
    }

    public override void Dispose()
    {
        StopListenerAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
