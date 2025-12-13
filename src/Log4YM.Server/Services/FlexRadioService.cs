using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

/// <summary>
/// Service for FlexRadio discovery and CAT control via Flex Discovery Protocol
/// </summary>
public class FlexRadioService : BackgroundService
{
    private readonly ILogger<FlexRadioService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ConcurrentDictionary<string, FlexRadioDevice> _discoveredRadios = new();
    private readonly ConcurrentDictionary<string, FlexRadioConnection> _connections = new();

    private const int DiscoveryPort = 4992;
    private const int RadioCleanupSeconds = 30;

    private UdpClient? _discoveryClient;
    private CancellationTokenSource? _discoveryCts;
    private bool _isDiscovering;

    public FlexRadioService(
        ILogger<FlexRadioService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlexRadio service starting...");

        // Run cleanup task periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupStaleRadiosAsync();
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    public Task StartDiscoveryAsync()
    {
        if (_isDiscovering)
        {
            _logger.LogDebug("FlexRadio discovery already running");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting FlexRadio discovery on UDP port {Port}", DiscoveryPort);

        _discoveryCts = new CancellationTokenSource();
        _isDiscovering = true;

        _ = RunDiscoveryAsync(_discoveryCts.Token);

        return Task.CompletedTask;
    }

    public Task StopDiscoveryAsync()
    {
        if (!_isDiscovering)
        {
            return Task.CompletedTask;
        }

        _logger.LogInformation("Stopping FlexRadio discovery");

        _discoveryCts?.Cancel();
        _discoveryClient?.Close();
        _discoveryClient = null;
        _isDiscovering = false;

        return Task.CompletedTask;
    }

    private async Task RunDiscoveryAsync(CancellationToken ct)
    {
        try
        {
            _discoveryClient = new UdpClient();
            _discoveryClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _discoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            _logger.LogInformation("Listening for FlexRadio VITA-49 discovery on UDP port {Port}", DiscoveryPort);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await _discoveryClient.ReceiveAsync(ct);
                    await ProcessDiscoveryPacketAsync(result.Buffer, result.RemoteEndPoint);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error receiving FlexRadio discovery packet");
                    await Task.Delay(1000, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlexRadio discovery error");
        }
        finally
        {
            _isDiscovering = false;
        }
    }

    private async Task ProcessDiscoveryPacketAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            // VITA-49 discovery packet parsing
            // FlexRadio discovery packets contain key=value pairs
            var message = Encoding.UTF8.GetString(data);

            // Check if this is a FlexRadio discovery packet
            if (!message.Contains("discovery_protocol_version") && !message.Contains("model=FLEX"))
            {
                return;
            }

            _logger.LogDebug("FlexRadio discovery packet: {Message}", message);

            var values = ParseKeyValuePairs(message);

            var serial = values.GetValueOrDefault("serial", "");
            if (string.IsNullOrEmpty(serial))
            {
                return;
            }

            var device = new FlexRadioDevice
            {
                Id = $"flex-{serial}",
                Serial = serial,
                Model = values.GetValueOrDefault("model", "FlexRadio"),
                Nickname = values.GetValueOrDefault("nickname", ""),
                IpAddress = values.GetValueOrDefault("ip", remoteEndPoint.Address.ToString()),
                Port = int.TryParse(values.GetValueOrDefault("port", "4992"), out var port) ? port : 4992,
                Version = values.GetValueOrDefault("version", ""),
                LastSeen = DateTime.UtcNow
            };

            var isNew = !_discoveredRadios.ContainsKey(device.Id);
            _discoveredRadios[device.Id] = device;

            if (isNew)
            {
                _logger.LogInformation("Discovered FlexRadio: {Model} ({Serial}) at {Ip}:{Port}",
                    device.Model, device.Serial, device.IpAddress, device.Port);

                var evt = new RadioDiscoveredEvent(
                    device.Id,
                    RadioType.FlexRadio,
                    device.Model,
                    device.IpAddress,
                    device.Port,
                    device.Nickname,
                    null
                );

                await _hubContext.BroadcastRadioDiscovered(evt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse FlexRadio discovery packet");
        }
    }

    private async Task CleanupStaleRadiosAsync()
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-RadioCleanupSeconds);
        var staleRadios = _discoveredRadios.Values
            .Where(r => r.LastSeen < cutoff)
            .ToList();

        foreach (var radio in staleRadios)
        {
            if (_discoveredRadios.TryRemove(radio.Id, out _))
            {
                _logger.LogInformation("FlexRadio {Serial} no longer available", radio.Serial);
                await _hubContext.BroadcastRadioRemoved(new RadioRemovedEvent(radio.Id));
            }
        }
    }

    public bool HasRadio(string radioId) => _discoveredRadios.ContainsKey(radioId);

    public async Task ConnectAsync(string radioId)
    {
        if (!_discoveredRadios.TryGetValue(radioId, out var device))
        {
            _logger.LogWarning("FlexRadio {RadioId} not found", radioId);
            return;
        }

        if (_connections.ContainsKey(radioId))
        {
            _logger.LogDebug("Already connected to FlexRadio {RadioId}", radioId);
            return;
        }

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Connecting));

        var connection = new FlexRadioConnection(device, _logger, _hubContext);
        if (_connections.TryAdd(radioId, connection))
        {
            _ = connection.ConnectAsync();
        }
    }

    public async Task DisconnectAsync(string radioId)
    {
        if (_connections.TryRemove(radioId, out var connection))
        {
            await connection.DisconnectAsync();
            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Disconnected));
        }
    }

    public Task SelectSliceAsync(string radioId, string sliceId)
    {
        if (_connections.TryGetValue(radioId, out var connection))
        {
            connection.SelectSlice(sliceId);
        }
        return Task.CompletedTask;
    }

    public IEnumerable<RadioDiscoveredEvent> GetDiscoveredRadios()
    {
        return _discoveredRadios.Values.Select(d => new RadioDiscoveredEvent(
            d.Id,
            RadioType.FlexRadio,
            d.Model,
            d.IpAddress,
            d.Port,
            d.Nickname,
            null
        ));
    }

    public IEnumerable<RadioStateChangedEvent> GetRadioStates()
    {
        return _connections.Values
            .Where(c => c.IsConnected)
            .Select(c => c.GetCurrentState())
            .Where(s => s != null)!;
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
}

internal class FlexRadioDevice
{
    public required string Id { get; set; }
    public required string Serial { get; set; }
    public required string Model { get; set; }
    public string? Nickname { get; set; }
    public required string IpAddress { get; set; }
    public int Port { get; set; }
    public string? Version { get; set; }
    public DateTime LastSeen { get; set; }
}

internal class FlexRadioConnection
{
    private readonly FlexRadioDevice _device;
    private readonly ILogger _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;

    private string? _selectedSlice;
    private long _currentFrequencyHz;
    private string _currentMode = "USB";
    private bool _isTransmitting;

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public FlexRadioConnection(FlexRadioDevice device, ILogger logger, IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _device = device;
        _logger = logger;
        _hubContext = hubContext;
    }

    public async Task ConnectAsync()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            _logger.LogInformation("Connecting to FlexRadio {Serial} at {Ip}:{Port}",
                _device.Serial, _device.IpAddress, _device.Port);

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(_device.IpAddress, _device.Port, ct);
            _stream = _tcpClient.GetStream();

            _logger.LogInformation("Connected to FlexRadio {Serial}", _device.Serial);

            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Connected));

            // Start receive loop
            await ReceiveLoopAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FlexRadio connection error for {Serial}", _device.Serial);
            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Error, ex.Message));
        }
    }

    public Task DisconnectAsync()
    {
        _cts?.Cancel();
        _stream?.Close();
        _tcpClient?.Close();
        _tcpClient = null;
        _stream = null;
        return Task.CompletedTask;
    }

    public void SelectSlice(string sliceId)
    {
        _selectedSlice = sliceId;
        _logger.LogInformation("FlexRadio {Serial} monitoring slice {Slice}", _device.Serial, sliceId);

        _ = _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Monitoring));
    }

    public RadioStateChangedEvent? GetCurrentState()
    {
        if (!IsConnected) return null;

        return new RadioStateChangedEvent(
            _device.Id,
            _currentFrequencyHz,
            _currentMode,
            _isTransmitting,
            BandHelper.GetBand(_currentFrequencyHz),
            _selectedSlice
        );
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested && _tcpClient?.Connected == true)
        {
            try
            {
                var bytesRead = await _stream!.ReadAsync(buffer, ct);
                if (bytesRead == 0)
                {
                    _logger.LogWarning("FlexRadio connection closed");
                    break;
                }

                var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                await ProcessMessageAsync(data);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FlexRadio receive error");
                break;
            }
        }

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Disconnected));
    }

    private async Task ProcessMessageAsync(string data)
    {
        // Parse FlexRadio protocol messages
        // Look for slice status updates with frequency/mode
        var lines = data.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("slice") && (_selectedSlice == null || line.Contains(_selectedSlice)))
            {
                // Parse frequency from slice status
                var freqMatch = Regex.Match(line, @"RF_frequency=(\d+\.?\d*)");
                if (freqMatch.Success && double.TryParse(freqMatch.Groups[1].Value, out var freqMhz))
                {
                    var newFreq = (long)(freqMhz * 1_000_000);
                    if (newFreq != _currentFrequencyHz)
                    {
                        _currentFrequencyHz = newFreq;
                        await BroadcastStateAsync();
                    }
                }

                // Parse mode
                var modeMatch = Regex.Match(line, @"mode=(\w+)");
                if (modeMatch.Success)
                {
                    var newMode = modeMatch.Groups[1].Value.ToUpper();
                    if (newMode != _currentMode)
                    {
                        _currentMode = newMode;
                        await BroadcastStateAsync();
                    }
                }
            }

            // Parse TX state
            if (line.Contains("interlock") || line.Contains("transmit"))
            {
                var txMatch = Regex.Match(line, @"state=(\w+)");
                if (txMatch.Success)
                {
                    var newTx = txMatch.Groups[1].Value.Equals("TRANSMITTING", StringComparison.OrdinalIgnoreCase);
                    if (newTx != _isTransmitting)
                    {
                        _isTransmitting = newTx;
                        await BroadcastStateAsync();
                    }
                }
            }
        }
    }

    private async Task BroadcastStateAsync()
    {
        var state = GetCurrentState();
        if (state != null)
        {
            await _hubContext.BroadcastRadioStateChanged(state);
        }
    }
}
