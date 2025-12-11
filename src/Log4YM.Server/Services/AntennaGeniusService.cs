using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public class AntennaGeniusService : BackgroundService
{
    private readonly ILogger<AntennaGeniusService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ConcurrentDictionary<string, AntennaGeniusConnection> _connections = new();

    private const int DiscoveryPort = 9007;
    private const int KeepAliveIntervalMs = 30000;
    private const int ReconnectDelayMs = 5000;

    public AntennaGeniusService(
        ILogger<AntennaGeniusService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Antenna Genius service starting...");

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
            _logger.LogInformation("Antenna Genius service stopping...");
        }
    }

    private async Task RunDiscoveryListenerAsync(CancellationToken ct)
    {
        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        _logger.LogInformation("Listening for Antenna Genius discovery on UDP port {Port}", DiscoveryPort);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(ct);
                var message = Encoding.ASCII.GetString(result.Buffer);

                if (message.StartsWith("AG "))
                {
                    var device = ParseDiscoveryMessage(message);
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
                _logger.LogError(ex, "Error in discovery listener");
                await Task.Delay(1000, ct);
            }
        }
    }

    private AntennaGeniusDiscoveredEvent? ParseDiscoveryMessage(string message)
    {
        // AG ip=192.168.1.39 port=9007 v=4.0.22 serial=9A-3A-DC name=Ranko_4O3A ports=2 antennas=8 mode=master uptime=3034
        try
        {
            var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var values = new Dictionary<string, string>();

            foreach (var part in parts.Skip(1)) // Skip "AG"
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                {
                    values[kv[0]] = kv[1];
                }
            }

            return new AntennaGeniusDiscoveredEvent(
                IpAddress: values.GetValueOrDefault("ip", ""),
                Port: int.Parse(values.GetValueOrDefault("port", "9007")),
                Version: values.GetValueOrDefault("v", ""),
                Serial: values.GetValueOrDefault("serial", ""),
                Name: values.GetValueOrDefault("name", "Unknown")!.Replace('_', ' '),
                RadioPorts: int.Parse(values.GetValueOrDefault("ports", "2")),
                AntennaPorts: int.Parse(values.GetValueOrDefault("antennas", "8")),
                Mode: values.GetValueOrDefault("mode", "master"),
                Uptime: int.Parse(values.GetValueOrDefault("uptime", "0"))
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse discovery message: {Message}", message);
            return null;
        }
    }

    private async Task HandleDeviceDiscoveredAsync(AntennaGeniusDiscoveredEvent device, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(device.Serial))
            return;

        if (!_connections.TryGetValue(device.Serial, out var connection))
        {
            _logger.LogInformation("Discovered Antenna Genius: {Name} ({Serial}) at {Ip}:{Port}",
                device.Name, device.Serial, device.IpAddress, device.Port);

            connection = new AntennaGeniusConnection(device, _logger, _hubContext);
            if (_connections.TryAdd(device.Serial, connection))
            {
                // Broadcast discovery event
                await _hubContext.Clients.All.OnAntennaGeniusDiscovered(device);

                // Start TCP connection
                _ = connection.ConnectAsync(ct);
            }
        }
        else
        {
            // Update uptime
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

    public async Task SelectAntennaAsync(string serial, int portId, int antennaId)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            await connection.SelectAntennaAsync(portId, antennaId);
        }
        else
        {
            _logger.LogWarning("Cannot select antenna: device {Serial} not found", serial);
        }
    }

    public AntennaGeniusStatusEvent? GetDeviceStatus(string serial)
    {
        if (_connections.TryGetValue(serial, out var connection))
        {
            return connection.GetStatus();
        }
        return null;
    }

    public IEnumerable<AntennaGeniusStatusEvent> GetAllDeviceStatuses()
    {
        return _connections.Values
            .Select(c => c.GetStatus())
            .Where(s => s != null)!;
    }
}

internal class AntennaGeniusConnection
{
    private readonly ILogger _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private AntennaGeniusDiscoveredEvent _device;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private StreamReader? _reader;
    private int _sequenceNumber = 0;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<List<string>>> _pendingCommands = new();

    private List<AntennaGeniusAntennaInfo> _antennas = new();
    private List<AntennaGeniusBandInfo> _bands = new();
    private AntennaGeniusPortStatus _portA = new(1, true, "AUTO", 0, 0, 0, false, false);
    private AntennaGeniusPortStatus _portB = new(2, true, "AUTO", 0, 0, 0, false, false);
    private string _version = "";

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public AntennaGeniusConnection(
        AntennaGeniusDiscoveredEvent device,
        ILogger logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _device = device;
        _logger = logger;
        _hubContext = hubContext;
    }

    public void UpdateDiscovery(AntennaGeniusDiscoveredEvent device)
    {
        _device = device;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Connecting to Antenna Genius at {Ip}:{Port}...",
                    _device.IpAddress, _device.Port);

                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_device.IpAddress, _device.Port, ct);
                _stream = _tcpClient.GetStream();
                _reader = new StreamReader(_stream, Encoding.ASCII);

                // Read prologue (V4.0.22 AG)
                var prologue = await _reader.ReadLineAsync(ct);
                if (prologue != null && prologue.StartsWith("V"))
                {
                    _version = prologue.Split(' ')[0].Substring(1);
                    _logger.LogInformation("Connected to Antenna Genius {Name}, firmware {Version}",
                        _device.Name, _version);
                }

                // Start receive loop in background FIRST so it can process responses
                var receiveTask = Task.Run(() => ReceiveLoopAsync(ct), ct);

                // Small delay to let receive loop start
                await Task.Delay(100, ct);

                // Initialize: get antenna list, bands, and port status
                await InitializeAsync(ct);

                // Subscribe to updates
                await SubscribeToUpdatesAsync(ct);

                // Broadcast full status
                await BroadcastStatusAsync();

                // Wait for receive loop to complete (usually when connection drops)
                await receiveTask;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection error to {Name}", _device.Name);
                await _hubContext.Clients.All.OnAntennaGeniusDisconnected(
                    new AntennaGeniusDisconnectedEvent(_device.Serial));
            }

            // Cleanup and retry
            Disconnect();

            if (!ct.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnecting to {Name} in 5 seconds...", _device.Name);
                await Task.Delay(5000, ct);
            }
        }
    }

    private async Task InitializeAsync(CancellationToken ct)
    {
        // Get antenna list
        var antennaResponse = await SendCommandAsync("antenna list", ct);
        _antennas = ParseAntennaList(antennaResponse);
        _logger.LogDebug("Loaded {Count} antennas", _antennas.Count);

        // Get band list
        var bandResponse = await SendCommandAsync("band list", ct);
        _bands = ParseBandList(bandResponse);
        _logger.LogDebug("Loaded {Count} bands", _bands.Count);

        // Get port status
        var port1Response = await SendCommandAsync("port get 1", ct);
        _portA = ParsePortStatus(port1Response, 1);

        var port2Response = await SendCommandAsync("port get 2", ct);
        _portB = ParsePortStatus(port2Response, 2);

        _logger.LogInformation("Port A: Band={Band}, Antenna={Ant}, Port B: Band={BandB}, Antenna={AntB}",
            _bands.FirstOrDefault(b => b.Id == _portA.Band)?.Name ?? "None",
            _antennas.FirstOrDefault(a => a.Id == _portA.RxAntenna)?.Name ?? "None",
            _bands.FirstOrDefault(b => b.Id == _portB.Band)?.Name ?? "None",
            _antennas.FirstOrDefault(a => a.Id == _portB.RxAntenna)?.Name ?? "None");
    }

    private async Task SubscribeToUpdatesAsync(CancellationToken ct)
    {
        await SendCommandAsync("sub port all", ct);
        await SendCommandAsync("sub relay", ct);
        _logger.LogDebug("Subscribed to port and relay updates");
    }

    private async Task<List<string>> SendCommandAsync(string command, CancellationToken ct = default)
    {
        await _sendLock.WaitAsync(ct);
        try
        {
            var seq = Interlocked.Increment(ref _sequenceNumber) % 256;
            if (seq == 0) seq = 1;

            var tcs = new TaskCompletionSource<List<string>>();
            _pendingCommands[seq] = tcs;

            var commandLine = $"C{seq}|{command}\r\n";
            var bytes = Encoding.ASCII.GetBytes(commandLine);

            await _stream!.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);

            _logger.LogInformation("Sent: {Command}", commandLine.TrimEnd());

            // Wait for response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(5000);

            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Command timeout: {Command}", command);
                return new List<string>();
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
        _logger.LogInformation("Receive loop starting...");

        while (!ct.IsCancellationRequested && _tcpClient?.Connected == true)
        {
            try
            {
                var line = await _reader!.ReadLineAsync(ct);
                if (line == null)
                {
                    _logger.LogWarning("Connection closed by server");
                    break;
                }

                _logger.LogInformation("Received line: {Line}", line);
                await ProcessLineAsync(line);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in receive loop");
                break;
            }
        }
        _logger.LogInformation("Receive loop ended");
    }

    private async Task ProcessLineAsync(string line)
    {
        _logger.LogInformation("Received: {Line}", line);

        if (line.StartsWith("R"))
        {
            // Response: R<seq>|<hex_code>|<message>
            var parts = line.Split('|', 3);
            if (parts.Length >= 2)
            {
                var seqStr = parts[0].Substring(1);
                if (int.TryParse(seqStr, out var seq))
                {
                    var message = parts.Length > 2 ? parts[2] : "";

                    if (string.IsNullOrEmpty(message))
                    {
                        // Empty message means end of response - complete the TCS
                        if (_pendingCommands.TryGetValue(seq, out var tcs))
                        {
                            var responses = _pendingResponses.GetValueOrDefault(seq, new List<string>());
                            _pendingResponses.TryRemove(seq, out _);
                            tcs.TrySetResult(responses);
                            _logger.LogDebug("Command {Seq} completed with {Count} responses (terminator)", seq, responses.Count);
                        }
                    }
                    else
                    {
                        // Non-empty message - accumulate it
                        if (!_pendingResponses.ContainsKey(seq))
                        {
                            _pendingResponses[seq] = new List<string>();
                        }
                        _pendingResponses[seq].Add(message);

                        // For single-line responses like "port get", complete immediately
                        // since they don't send an empty terminator
                        if (message.StartsWith("port ") && _pendingCommands.TryGetValue(seq, out var tcs))
                        {
                            var responses = _pendingResponses.GetValueOrDefault(seq, new List<string>());
                            _pendingResponses.TryRemove(seq, out _);
                            tcs.TrySetResult(responses);
                            _logger.LogDebug("Command {Seq} completed with {Count} responses (single-line)", seq, responses.Count);
                        }
                    }
                }
            }
        }
        else if (line.StartsWith("S0|"))
        {
            // Status message
            var message = line.Substring(3);
            await ProcessStatusMessageAsync(message);
        }
    }

    private readonly ConcurrentDictionary<int, List<string>> _pendingResponses = new();

    private async Task ProcessStatusMessageAsync(string message)
    {
        // S0|port 1 auto=1 source=AUTO band=0 rxant=0 txant=0 inband=0 tx=0 inhibit=0
        // S0|relay tx=00 rx=04 state=04

        if (message.StartsWith("port "))
        {
            var status = ParsePortStatusFromMessage(message);
            if (status != null)
            {
                if (status.PortId == 1)
                    _portA = status;
                else if (status.PortId == 2)
                    _portB = status;

                var evt = new AntennaGeniusPortChangedEvent(
                    _device.Serial,
                    status.PortId,
                    status.Auto,
                    status.Source,
                    status.Band,
                    status.RxAntenna,
                    status.TxAntenna,
                    status.IsTransmitting,
                    status.IsInhibited
                );

                await _hubContext.Clients.All.OnAntennaGeniusPortChanged(evt);

                _logger.LogInformation("Port {Port} changed: Band={Band}, RxAnt={RxAnt}, TxAnt={TxAnt}, TX={Tx}",
                    status.PortId,
                    _bands.FirstOrDefault(b => b.Id == status.Band)?.Name ?? "None",
                    _antennas.FirstOrDefault(a => a.Id == status.RxAntenna)?.Name ?? "None",
                    _antennas.FirstOrDefault(a => a.Id == status.TxAntenna)?.Name ?? "None",
                    status.IsTransmitting);
            }
        }
        else if (message.StartsWith("relay "))
        {
            // Relay status - we could track this but it's mainly for debugging
            _logger.LogDebug("Relay status: {Message}", message);
        }
        else if (message.StartsWith("antenna reload"))
        {
            // Antenna config changed - reload
            _logger.LogInformation("Antenna configuration changed, reloading...");
            var antennaResponse = await SendCommandAsync("antenna list");
            _antennas = ParseAntennaList(antennaResponse);
            await BroadcastStatusAsync();
        }
    }

    private List<AntennaGeniusAntennaInfo> ParseAntennaList(List<string> responses)
    {
        var antennas = new List<AntennaGeniusAntennaInfo>();

        foreach (var line in responses)
        {
            // antenna 1 name=Antenna_1 tx=0000 rx=0001 inband=0000
            var match = Regex.Match(line, @"antenna (\d+) name=(\S+) tx=([0-9A-Fa-f]+) rx=([0-9A-Fa-f]+) inband=([0-9A-Fa-f]+)");
            if (match.Success)
            {
                antennas.Add(new AntennaGeniusAntennaInfo(
                    Id: int.Parse(match.Groups[1].Value),
                    Name: match.Groups[2].Value.Replace('_', ' '),
                    TxBandMask: Convert.ToUInt16(match.Groups[3].Value, 16),
                    RxBandMask: Convert.ToUInt16(match.Groups[4].Value, 16),
                    InbandMask: Convert.ToUInt16(match.Groups[5].Value, 16)
                ));
            }
        }

        return antennas;
    }

    private List<AntennaGeniusBandInfo> ParseBandList(List<string> responses)
    {
        var bands = new List<AntennaGeniusBandInfo>();

        foreach (var line in responses)
        {
            // band 0 name=None freq_start=0.000000 freq_stop=0.000000
            var match = Regex.Match(line, @"band (\d+) name=(\S+) freq_start=(\S+) freq_stop=(\S+)");
            if (match.Success)
            {
                bands.Add(new AntennaGeniusBandInfo(
                    Id: int.Parse(match.Groups[1].Value),
                    Name: match.Groups[2].Value.Replace('_', ' '),
                    FreqStart: double.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture),
                    FreqStop: double.Parse(match.Groups[4].Value, System.Globalization.CultureInfo.InvariantCulture)
                ));
            }
        }

        return bands;
    }

    private AntennaGeniusPortStatus ParsePortStatus(List<string> responses, int portId)
    {
        foreach (var line in responses)
        {
            var status = ParsePortStatusFromMessage($"port {portId} " + line.Replace($"port {portId} ", ""));
            if (status != null && status.PortId == portId)
                return status;

            // Try parsing directly
            status = ParsePortStatusFromMessage(line);
            if (status != null)
                return status;
        }

        return new AntennaGeniusPortStatus(portId, true, "AUTO", 0, 0, 0, false, false);
    }

    private AntennaGeniusPortStatus? ParsePortStatusFromMessage(string message)
    {
        // port 1 auto=1 source=AUTO band=0 rxant=0 txant=0 tx=0 inhibit=0
        var match = Regex.Match(message,
            @"port (\d+) auto=(\d+) source=(\S+) band=(\d+) rxant=(\d+) txant=(\d+).*?tx=(\d+) inhibit=(\d+)");

        if (match.Success)
        {
            return new AntennaGeniusPortStatus(
                PortId: int.Parse(match.Groups[1].Value),
                Auto: match.Groups[2].Value == "1",
                Source: match.Groups[3].Value,
                Band: int.Parse(match.Groups[4].Value),
                RxAntenna: int.Parse(match.Groups[5].Value),
                TxAntenna: int.Parse(match.Groups[6].Value),
                IsTransmitting: match.Groups[7].Value == "1",
                IsInhibited: match.Groups[8].Value == "1"
            );
        }

        return null;
    }

    public async Task SelectAntennaAsync(int portId, int antennaId)
    {
        if (!IsConnected)
        {
            _logger.LogWarning("Cannot select antenna: not connected");
            return;
        }

        // Set both TX and RX antenna to the same value
        var command = $"port set {portId} rxant={antennaId} txant={antennaId}";
        await SendCommandAsync(command);

        _logger.LogInformation("Selected antenna {AntennaId} for port {PortId}", antennaId, portId);
    }

    public async Task SendPingAsync()
    {
        try
        {
            await SendCommandAsync("ping");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ping failed");
        }
    }

    public AntennaGeniusStatusEvent GetStatus()
    {
        return new AntennaGeniusStatusEvent(
            DeviceSerial: _device.Serial,
            DeviceName: _device.Name,
            IpAddress: _device.IpAddress,
            Version: _version,
            IsConnected: IsConnected,
            Antennas: _antennas,
            Bands: _bands,
            PortA: _portA,
            PortB: _portB
        );
    }

    private async Task BroadcastStatusAsync()
    {
        var status = GetStatus();
        await _hubContext.Clients.All.OnAntennaGeniusStatus(status);
    }

    private async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        var buffer = new StringBuilder();
        var readBuffer = new byte[1];

        while (!ct.IsCancellationRequested)
        {
            var bytesRead = await _stream!.ReadAsync(readBuffer, ct);
            if (bytesRead == 0) return null;

            var c = (char)readBuffer[0];
            if (c == '\r' || c == '\n')
            {
                if (buffer.Length > 0)
                    return buffer.ToString();
            }
            else
            {
                buffer.Append(c);
            }
        }

        return buffer.Length > 0 ? buffer.ToString() : null;
    }

    private void Disconnect()
    {
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch { }

        _stream = null;
        _tcpClient = null;
    }
}
