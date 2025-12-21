using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public class PgxlService : BackgroundService
{
    private readonly ILogger<PgxlService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ConcurrentDictionary<string, PgxlConnection> _connections = new();

    // PGXL uses port 9008 for both UDP discovery and TCP control
    // (similar to Antenna Genius on port 9007)
    private const int DiscoveryPort = 9008;
    private const int ControlPort = 9008;
    private const int KeepAliveIntervalMs = 30000;

    public PgxlService(
        ILogger<PgxlService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PGXL Amplifier service starting...");

        try
        {
            // Start UDP discovery listener
            var discoveryTask = RunDiscoveryListenerAsync(stoppingToken);

            // Start keep-alive task
            var keepAliveTask = RunKeepAliveAsync(stoppingToken);

            await Task.WhenAll(discoveryTask, keepAliveTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("PGXL service stopping...");
        }
    }

    private async Task RunDiscoveryListenerAsync(CancellationToken ct)
    {
        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        _logger.LogInformation("Listening for PGXL discovery on UDP port {Port}", DiscoveryPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(ct);
                var message = Encoding.ASCII.GetString(result.Buffer);

                // Look for PGXL discovery packets (similar format to Antenna Genius)
                // Expected: "PGXL ip=x.x.x.x port=9008 v=x.x.x serial=xxx name=xxx ..."
                if (message.StartsWith("PGXL ") || message.Contains("PowerGenius") || message.Contains("model=PowerGeniusXL"))
                {
                    var device = ParseDiscoveryMessage(message, result.RemoteEndPoint);
                    if (device != null)
                    {
                        await HandleDeviceDiscoveredAsync(device, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in PGXL discovery listener");
                await Task.Delay(1000, ct);
            }
        }
    }

    private PgxlDiscoveredEvent? ParseDiscoveryMessage(string message, IPEndPoint remoteEndPoint)
    {
        try
        {
            _logger.LogInformation("PGXL discovery packet received: {Message}", message);

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Parse key=value pairs from discovery message
            var matches = Regex.Matches(message, @"(\w+)=([^\s]+)");
            foreach (Match match in matches)
            {
                values[match.Groups[1].Value] = match.Groups[2].Value;
            }

            // Extract IP - use from message if available, otherwise from remote endpoint
            var ip = values.GetValueOrDefault("ip", remoteEndPoint.Address.ToString());
            var port = int.Parse(values.GetValueOrDefault("port", DiscoveryPort.ToString()));
            var serial = values.GetValueOrDefault("serial_num")
                ?? values.GetValueOrDefault("serial", "")
                ?? "";
            var model = values.GetValueOrDefault("model", "PowerGeniusXL");
            var name = values.GetValueOrDefault("name", "");

            if (string.IsNullOrEmpty(serial))
            {
                // Use IP-based identifier if no serial
                serial = $"pgxl-{ip.Replace(".", "-")}";
            }

            // Use ControlPort for TCP, not the discovery port
            return new PgxlDiscoveredEvent(ip, ControlPort, serial, model);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse PGXL discovery message: {Message}", message);
            return null;
        }
    }

    private async Task HandleDeviceDiscoveredAsync(PgxlDiscoveredEvent device, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(device.Serial))
            return;

        if (!_connections.TryGetValue(device.Serial, out var connection))
        {
            _logger.LogInformation("Discovered PGXL: {Model} ({Serial}) at {Ip}:{Port}",
                device.Model, device.Serial, device.IpAddress, device.Port);

            connection = new PgxlConnection(device, _logger, _hubContext);
            if (_connections.TryAdd(device.Serial, connection))
            {
                // Broadcast discovery event
                await _hubContext.Clients.All.OnPgxlDiscovered(device);

                // Start TCP connection
                _ = connection.ConnectAsync(ct);
            }
        }
        else
        {
            // Update device info if already known
            connection.UpdateDiscovery(device);
        }
    }


    private async Task RunKeepAliveAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(KeepAliveIntervalMs, ct);

                foreach (var connection in _connections.Values)
                {
                    if (connection.IsConnected)
                    {
                        _ = connection.SendPingAsync();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task SetOperateAsync(string serial)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            await connection.SetOperateAsync();
        }
        else
        {
            _logger.LogWarning("PGXL with serial {Serial} not found", serial);
        }
    }

    public async Task SetStandbyAsync(string serial)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            await connection.SetStandbyAsync();
        }
        else
        {
            _logger.LogWarning("PGXL with serial {Serial} not found", serial);
        }
    }

    public PgxlStatusEvent? GetStatus(string serial)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            return connection.GetStatus();
        }
        return null;
    }

    public IEnumerable<PgxlStatusEvent> GetAllStatuses()
    {
        return _connections.Values
            .Select(c => c.GetStatus())
            .Where(s => s != null)!;
    }

    /// <summary>
    /// Read the FlexRadio pairing configuration for a slice (A or B)
    /// </summary>
    public async Task<string?> ReadFlexRadioConfigAsync(string serial, string slice)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            return await connection.ReadFlexRadioConfigAsync(slice);
        }
        return null;
    }

    /// <summary>
    /// Disable FlexRadio pairing for a slice, allowing other band sources to work
    /// </summary>
    public async Task DisableFlexRadioPairingAsync(string serial, string slice)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            await connection.DisableFlexRadioPairingAsync(slice);
        }
    }
}

internal class PgxlConnection
{
    private readonly ILogger _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private PgxlDiscoveredEvent _device;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private int _sequenceNumber = 0;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pendingCommands = new();

    // Current state
    public string Serial => _device.Serial;
    public string IpAddress => _device.IpAddress;
    public bool IsConnected => _tcpClient?.Connected ?? false;
    public bool IsOperating { get; private set; }
    public bool IsTransmitting { get; private set; }
    public string Band { get; private set; } = "Unknown";
    public string BiasA { get; private set; } = "";
    public string BiasB { get; private set; } = "";
    public string FirmwareVersion { get; private set; } = "";

    // Meters
    private double _forwardPowerDbm;
    private double _returnLossDb;
    private double _drivePowerDbm;
    private double _paCurrent;
    private double _temperatureC;

    // Setup
    private string _bandSource = "ACC";
    private int _selectedAntenna = 1;
    private bool _attenuatorEnabled;
    private int _biasOffset;
    private int _pttDelay;
    private int _keyDelay;
    private bool _highSwr;
    private bool _overTemp;
    private bool _overCurrent;
    private string _nickname = "";

    private const string Terminator = "\r\n";

    public PgxlConnection(PgxlDiscoveredEvent device, ILogger logger, IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _device = device;
        _logger = logger;
        _hubContext = hubContext;
    }

    public void UpdateDiscovery(PgxlDiscoveredEvent device)
    {
        _device = device;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to PGXL {Serial} at {Ip}:{Port}...",
                    _device.Serial, _device.IpAddress, _device.Port);

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_device.IpAddress, _device.Port, ct);
                _stream = _tcpClient.GetStream();
                _reader = new StreamReader(_stream, Encoding.ASCII);

                _logger.LogInformation("Connected to PGXL at {Ip}:{Port}", _device.IpAddress, _device.Port);

                // Read version prologue
                await ReadPrologueAsync(ct);

                // Start receive loop in background
                var receiveTask = Task.Run(() => ReceiveLoopAsync(ct), ct);

                // Larger delay for receive loop to start
                await Task.Delay(500, ct);

                // Test: Try simple status command first
                _logger.LogInformation("PGXL sending test status command...");

                // Initialize - read setup to get configuration and serial
                await InitializeAsync(ct);

                // Start status polling loop
                await StatusPollingLoopAsync(ct);

                // Wait for receive task (runs until disconnect)
                await receiveTask;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PGXL connection error to {Serial}", _device.Serial);
                await _hubContext.Clients.All.OnPgxlDisconnected(new PgxlDisconnectedEvent(_device.Serial));
            }

            // Cleanup and retry
            Disconnect();

            if (!ct.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnecting to PGXL {Serial} in 5 seconds...", _device.Serial);
                await Task.Delay(5000, ct);
            }
        }
    }

    private async Task ReadPrologueAsync(CancellationToken ct)
    {
        // Read version line: V<a.b.c> [AUTH] or V<a.b.c> PGXL
        var prologue = await _reader!.ReadLineAsync(ct);
        _logger.LogInformation("PGXL prologue: '{Prologue}'", prologue);

        if (prologue != null && prologue.StartsWith("V"))
        {
            var parts = prologue.Split(' ');
            FirmwareVersion = parts[0].Substring(1);
            _logger.LogInformation("PGXL {Serial} firmware version: {Version}", _device.Serial, FirmwareVersion);
        }
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        // Start with a simple ping to verify communication
        _logger.LogInformation("PGXL sending ping to verify communication...");
        var pingResponse = await SendCommandAsync("ping", ct);
        _logger.LogInformation("PGXL ping response: '{Response}'", pingResponse);

        // Read setup to get configuration
        var setupResponse = await SendCommandAsync("setup read", ct);
        _logger.LogInformation("PGXL setup response: '{Response}'", setupResponse);
        ParseSetupResponse(setupResponse);

        // Get initial status
        var statusResponse = await SendCommandAsync("status", ct);
        _logger.LogInformation("PGXL status response: '{Response}'", statusResponse);
        ParseStatusResponse(statusResponse);

        // Read FlexRadio pairing configuration for both slices
        var flexConfigA = await SendCommandAsync("flexradio read=A", ct);
        _logger.LogInformation("PGXL FlexRadio config A: '{Response}'", flexConfigA);
        var flexConfigB = await SendCommandAsync("flexradio read=B", ct);
        _logger.LogInformation("PGXL FlexRadio config B: '{Response}'", flexConfigB);

        _logger.LogInformation("PGXL {Serial} initialized: Operating={Operating}, Band={Band}, Nickname={Nickname}",
            _device.Serial, IsOperating, Band, _nickname);
    }

    private async Task StatusPollingLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(500, ct);

                // Poll status
                var statusResponse = await SendCommandAsync("status", ct);
                ParseStatusResponse(statusResponse);

                // Broadcast status update
                await BroadcastStatusAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling PGXL status");
                break;
            }
        }
    }

    private async Task<string> SendCommandAsync(string command, CancellationToken ct = default)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            var seq = Interlocked.Increment(ref _sequenceNumber) % 256;
            if (seq == 0) seq = 1;

            var tcs = new TaskCompletionSource<string>();
            _pendingCommands[seq] = tcs;

            var commandLine = $"C{seq}|{command}{Terminator}";
            var bytes = Encoding.ASCII.GetBytes(commandLine);

            await _stream!.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);

            _logger.LogInformation("PGXL sent: {Command}", commandLine.TrimEnd());

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(5000);

            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("PGXL command timeout: {Command}", command);
                return "";
            }
            finally
            {
                _pendingCommands.TryRemove(seq, out _);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        _logger.LogInformation("PGXL receive loop starting for {Serial}", _device.Serial);

        while (!ct.IsCancellationRequested && _tcpClient?.Connected == true)
        {
            try
            {
                var line = await _reader!.ReadLineAsync(ct);
                if (line == null)
                {
                    _logger.LogWarning("PGXL connection closed");
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    ProcessLine(line);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PGXL receive error");
                break;
            }
        }
        _logger.LogInformation("PGXL receive loop ended for {Serial}", _device.Serial);
    }

    private void ProcessLine(string line)
    {
        _logger.LogInformation("PGXL recv: {Line}", line);

        if (line.StartsWith("R"))
        {
            // Response: R<seq>|<hex_response>|<message>
            var parts = line.Split('|', 3);
            if (parts.Length >= 2)
            {
                var seqStr = parts[0].Substring(1);
                if (int.TryParse(seqStr, out var seq))
                {
                    var message = parts.Length > 2 ? parts[2] : "";

                    if (_pendingCommands.TryGetValue(seq, out var tcs))
                    {
                        tcs.TrySetResult(message);
                    }
                }
            }
        }
        else if (line.StartsWith("S0|") || line.StartsWith("S|"))
        {
            // Async status message: S0|<message> or S|<message>
            var message = line.Contains('|') ? line.Substring(line.IndexOf('|') + 1) : line;
            ProcessAsyncStatus(message);
        }
    }

    private void ProcessAsyncStatus(string message)
    {
        _logger.LogDebug("PGXL async status: {Message}", message);

        // Parse state from async message to detect operating state
        var stateMatch = System.Text.RegularExpressions.Regex.Match(message, @"state=(\w+)");
        if (stateMatch.Success)
        {
            var state = stateMatch.Groups[1].Value;
            var wasOperating = IsOperating;

            // IDLE or TRANSMIT means we're in operate mode
            // STANDBY means we're in standby mode
            IsOperating = !state.Equals("STANDBY", StringComparison.OrdinalIgnoreCase)
                       && !state.Equals("FAULT", StringComparison.OrdinalIgnoreCase);
            IsTransmitting = state.StartsWith("TRANSMIT", StringComparison.OrdinalIgnoreCase);

            if (wasOperating != IsOperating)
            {
                _logger.LogInformation("PGXL state={State}, IsOperating changed to {IsOperating}", state, IsOperating);
            }

            if (state.Equals("FAULT", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PGXL entered FAULT state");
            }
        }
    }

    private void ParseStatusResponse(string response)
    {
        if (string.IsNullOrEmpty(response)) return;

        _logger.LogDebug("Parsing status response: {Response}", response);

        var values = ParseKeyValuePairs(response);

        // Detect operating mode using the 'state' field:
        //   state=STANDBY → amp is in standby mode (not operating)
        //   state=IDLE → amp is in operate mode but not transmitting
        //   state=TRANSMIT_A/TRANSMIT_B → amp is operating and transmitting
        //   state=FAULT → amp has a fault condition
        // Note: vdd (drain voltage) is only non-zero during active transmission,
        // so it cannot be used to detect operate vs standby mode.
        if (values.TryGetValue("state", out var state))
        {
            // IDLE or TRANSMIT means we're in operate mode
            // STANDBY means we're in standby mode
            IsOperating = !state.Equals("STANDBY", StringComparison.OrdinalIgnoreCase)
                       && !state.Equals("FAULT", StringComparison.OrdinalIgnoreCase);
            IsTransmitting = state.StartsWith("TRANSMIT", StringComparison.OrdinalIgnoreCase);
            _logger.LogDebug("PGXL state={State}, IsOperating={IsOperating}, IsTransmitting={IsTransmitting}",
                state, IsOperating, IsTransmitting);
        }

        // Band - PGXL sends bandA and bandB, we use bandA for primary display
        if (values.TryGetValue("bandA", out var bandA))
        {
            Band = FormatBand(bandA);
        }

        // Bias modes - PGXL sends biasA and biasB (e.g., "RADIO_AB", "AUTO_AB")
        if (values.TryGetValue("biasA", out var biasA))
        {
            BiasA = FormatBiasMode(biasA);
        }
        if (values.TryGetValue("biasB", out var biasB))
        {
            BiasB = FormatBiasMode(biasB);
        }

        // Meters
        if (values.TryGetValue("fwd", out var fwd) && double.TryParse(fwd, out var fwdVal))
            _forwardPowerDbm = fwdVal;
        // PGXL sends return loss in a field confusingly named "swr" - it's in dB as negative values
        // e.g., -17.3 means 17.3 dB return loss, -60.0 when idle means no valid reading
        if (values.TryGetValue("swr", out var swrRl) && double.TryParse(swrRl, out var swrRlVal))
            _returnLossDb = Math.Abs(swrRlVal);
        if (values.TryGetValue("drv", out var drv) && double.TryParse(drv, out var drvVal))
            _drivePowerDbm = drvVal;
        if (values.TryGetValue("id", out var id) && double.TryParse(id, out var idVal))
            _paCurrent = idVal;
        if (values.TryGetValue("temp", out var temp) && double.TryParse(temp, out var tempVal))
            _temperatureC = tempVal;

        // Fault flags - check meffa for fault keywords
        if (values.TryGetValue("meffa", out var meffaFaults))
        {
            _highSwr = meffaFaults.Contains("SWR", StringComparison.OrdinalIgnoreCase);
            _overTemp = meffaFaults.Contains("TEMP", StringComparison.OrdinalIgnoreCase);
            _overCurrent = meffaFaults.Contains("CURRENT", StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ParseSetupResponse(string response)
    {
        if (string.IsNullOrEmpty(response)) return;

        _logger.LogDebug("Parsing setup response: {Response}", response);

        var values = ParseKeyValuePairs(response);

        if (values.TryGetValue("nickname", out var nickname))
            _nickname = nickname.Replace('_', ' ');
        if (values.TryGetValue("fan", out var fan))
            { } // Fan mode
        if (values.TryGetValue("led", out var led))
            { } // LED intensity
    }

    private static Dictionary<string, string> ParseKeyValuePairs(string text)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = Regex.Matches(text, @"(\w+)=([^\s]+)");
        foreach (Match match in matches)
        {
            values[match.Groups[1].Value] = match.Groups[2].Value;
        }
        return values;
    }

    private static string FormatBand(string bandCode)
    {
        return bandCode switch
        {
            "0" => "N/A",
            "6" => "6m",
            "10" => "10m",
            "12" => "12m",
            "15" => "15m",
            "17" => "17m",
            "20" => "20m",
            "30" => "30m",
            "40" => "40m",
            "60" => "60m",
            "80" => "80m",
            "160" => "160m",
            _ => $"{bandCode}m"  // Default: append 'm' for any other band value
        };
    }

    /// <summary>
    /// Format bias mode from PGXL format (e.g., "RADIO_AB" -> "AB", "AUTO_AAB" -> "AAB")
    /// </summary>
    private static string FormatBiasMode(string biasValue)
    {
        if (string.IsNullOrEmpty(biasValue))
            return "";

        // PGXL sends bias as "SOURCE_MODE" format (e.g., RADIO_AB, AUTO_AAB, MANUAL_A)
        // Extract just the mode part after the underscore
        var underscoreIndex = biasValue.LastIndexOf('_');
        if (underscoreIndex >= 0 && underscoreIndex < biasValue.Length - 1)
        {
            return biasValue.Substring(underscoreIndex + 1);
        }

        // If no underscore, return as-is
        return biasValue;
    }

    public async Task SetOperateAsync()
    {
        _logger.LogInformation("Setting PGXL {Serial} to OPERATE mode", _device.Serial);
        // PGXL protocol: 'operate=1' enables operate mode
        var response = await SendCommandAsync("operate=1");
        _logger.LogInformation("PGXL operate response: '{Response}'", response);
    }

    public async Task SetStandbyAsync()
    {
        _logger.LogInformation("Setting PGXL {Serial} to STANDBY mode", _device.Serial);
        // PGXL protocol: 'operate=0' sets standby/idle mode
        var response = await SendCommandAsync("operate=0");
        _logger.LogInformation("PGXL standby response: '{Response}'", response);
    }

    public async Task SendPingAsync()
    {
        try
        {
            await SendCommandAsync("ping");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PGXL ping failed");
        }
    }

    /// <summary>
    /// Read FlexRadio pairing configuration for a slice
    /// </summary>
    public async Task<string?> ReadFlexRadioConfigAsync(string slice)
    {
        var response = await SendCommandAsync($"flexradio read={slice.ToUpper()}");
        _logger.LogInformation("PGXL FlexRadio config for slice {Slice}: {Response}", slice, response);
        return response;
    }

    /// <summary>
    /// Disable FlexRadio pairing for a slice to allow other band sources
    /// </summary>
    public async Task DisableFlexRadioPairingAsync(string slice)
    {
        _logger.LogInformation("Disabling FlexRadio pairing for PGXL slice {Slice}", slice);
        // Set active=0 to disable the pairing
        var response = await SendCommandAsync($"flexradio ampslice={slice.ToUpper()} active=0");
        _logger.LogInformation("PGXL disable FlexRadio response: {Response}", response);
    }

    public PgxlStatusEvent GetStatus()
    {
        // Only calculate SWR when transmitting - when not TX, return loss is 0 which gives invalid SWR
        var swrRatio = IsTransmitting ? ReturnLossToSwr(_returnLossDb) : 0;

        var meters = new PgxlMeters(
            ForwardPowerDbm: _forwardPowerDbm,
            ForwardPowerWatts: DbmToWatts(_forwardPowerDbm),
            ReturnLossDb: _returnLossDb,
            SwrRatio: swrRatio,
            DrivePowerDbm: _drivePowerDbm,
            PaCurrent: _paCurrent,
            TemperatureC: _temperatureC
        );

        var setup = new PgxlSetup(
            BandSource: _bandSource,
            SelectedAntenna: _selectedAntenna,
            AttenuatorEnabled: _attenuatorEnabled,
            BiasOffset: _biasOffset,
            PttDelay: _pttDelay,
            KeyDelay: _keyDelay,
            HighSwr: _highSwr,
            OverTemp: _overTemp,
            OverCurrent: _overCurrent
        );

        return new PgxlStatusEvent(
            Serial: _device.Serial,
            IpAddress: _device.IpAddress,
            IsConnected: IsConnected,
            IsOperating: IsOperating,
            IsTransmitting: IsTransmitting,
            Band: Band,
            BiasA: BiasA,
            BiasB: BiasB,
            Meters: meters,
            Setup: setup
        );
    }

    private async Task BroadcastStatusAsync()
    {
        var status = GetStatus();
        await _hubContext.Clients.All.OnPgxlStatus(status);
    }

    // Convert dBm to Watts: P(W) = 10^((dBm - 30) / 10)
    private static double DbmToWatts(double dbm)
    {
        if (dbm <= 0) return 0;
        return Math.Pow(10, (dbm - 30) / 10);
    }

    // Convert Return Loss (dB) to SWR
    private static double ReturnLossToSwr(double rl)
    {
        if (rl <= 0) return 99.9;
        var gamma = Math.Pow(10, -rl / 20);
        if (gamma >= 1) return 99.9;
        var swr = (1 + gamma) / (1 - gamma);
        return Math.Min(swr, 99.9);
    }

    private void Disconnect()
    {
        try
        {
            _reader?.Close();
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch { }

        _reader = null;
        _stream = null;
        _tcpClient = null;
    }
}
