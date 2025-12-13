using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

/// <summary>
/// Service for TCI (Thetis, Hermes, ANAN) radio discovery and CAT control
/// </summary>
public class TciRadioService : BackgroundService
{
    private readonly ILogger<TciRadioService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ConcurrentDictionary<string, TciRadioDevice> _discoveredRadios = new();
    private readonly ConcurrentDictionary<string, TciRadioConnection> _connections = new();

    private const int DiscoveryPort = 1024;
    private const int TciDefaultPort = 50001;
    private const int RadioCleanupSeconds = 30;
    private const int DiscoveryBroadcastIntervalMs = 10000;

    private UdpClient? _discoveryClient;
    private CancellationTokenSource? _discoveryCts;
    private bool _isDiscovering;

    public TciRadioService(
        ILogger<TciRadioService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TCI Radio service starting...");

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
            _logger.LogDebug("TCI discovery already running");
            return Task.CompletedTask;
        }

        _logger.LogInformation("Starting TCI discovery on UDP port {Port}", DiscoveryPort);

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

        _logger.LogInformation("Stopping TCI discovery");

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
            _discoveryClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0)); // Bind to any available port for sending
            _discoveryClient.EnableBroadcast = true;

            _logger.LogInformation("TCI discovery started, broadcasting on UDP port {Port}", DiscoveryPort);

            // Start listener task
            var listenerTask = ListenForDiscoveryResponsesAsync(ct);

            // Send broadcast discovery requests periodically
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await SendDiscoveryBroadcastAsync();
                    await Task.Delay(DiscoveryBroadcastIntervalMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            await listenerTask;
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCI discovery error");
        }
        finally
        {
            _isDiscovering = false;
        }
    }

    private async Task SendDiscoveryBroadcastAsync()
    {
        try
        {
            // Thetis/Hermes discovery message
            var discoveryMessage = Encoding.UTF8.GetBytes("discovery");
            var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

            await _discoveryClient!.SendAsync(discoveryMessage, discoveryMessage.Length, broadcastEndpoint);
            _logger.LogDebug("Sent TCI discovery broadcast");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send TCI discovery broadcast");
        }
    }

    private async Task ListenForDiscoveryResponsesAsync(CancellationToken ct)
    {
        using var listener = new UdpClient(DiscoveryPort);
        listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await listener.ReceiveAsync(ct);
                await ProcessDiscoveryResponseAsync(result.Buffer, result.RemoteEndPoint);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving TCI discovery response");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessDiscoveryResponseAsync(byte[] data, IPEndPoint remoteEndPoint)
    {
        try
        {
            var message = Encoding.UTF8.GetString(data);
            _logger.LogDebug("TCI discovery response from {Ip}: {Message}", remoteEndPoint.Address, message);

            // Parse Thetis/Hermes discovery response
            // Format varies but typically contains: name, model, version, tci_port
            var values = ParseKeyValuePairs(message);

            // Generate ID from IP if no serial available
            var id = values.GetValueOrDefault("serial", remoteEndPoint.Address.ToString().Replace(".", "-"));
            var deviceId = $"tci-{id}";

            var device = new TciRadioDevice
            {
                Id = deviceId,
                Model = values.GetValueOrDefault("model", values.GetValueOrDefault("name", "TCI Radio")),
                IpAddress = remoteEndPoint.Address.ToString(),
                TciPort = int.TryParse(values.GetValueOrDefault("tci_port", TciDefaultPort.ToString()), out var port) ? port : TciDefaultPort,
                Version = values.GetValueOrDefault("version", ""),
                Instances = int.TryParse(values.GetValueOrDefault("receivers", "1"), out var instances) ? instances : 1,
                LastSeen = DateTime.UtcNow
            };

            var isNew = !_discoveredRadios.ContainsKey(device.Id);
            _discoveredRadios[device.Id] = device;

            if (isNew)
            {
                _logger.LogInformation("Discovered TCI radio: {Model} at {Ip}:{Port}",
                    device.Model, device.IpAddress, device.TciPort);

                var evt = new RadioDiscoveredEvent(
                    device.Id,
                    RadioType.Tci,
                    device.Model,
                    device.IpAddress,
                    device.TciPort,
                    null,
                    null
                );

                await _hubContext.BroadcastRadioDiscovered(evt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TCI discovery response");
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
                _logger.LogInformation("TCI radio {Id} no longer available", radio.Id);
                await _hubContext.BroadcastRadioRemoved(new RadioRemovedEvent(radio.Id));
            }
        }
    }

    public bool HasRadio(string radioId) => _discoveredRadios.ContainsKey(radioId);

    public async Task ConnectAsync(string radioId)
    {
        if (!_discoveredRadios.TryGetValue(radioId, out var device))
        {
            _logger.LogWarning("TCI radio {RadioId} not found", radioId);
            return;
        }

        if (_connections.ContainsKey(radioId))
        {
            _logger.LogDebug("Already connected to TCI radio {RadioId}", radioId);
            return;
        }

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Connecting));

        var connection = new TciRadioConnection(device, _logger, _hubContext);
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

    public Task SelectInstanceAsync(string radioId, int instance)
    {
        if (_connections.TryGetValue(radioId, out var connection))
        {
            connection.SelectInstance(instance);
        }
        return Task.CompletedTask;
    }

    public IEnumerable<RadioDiscoveredEvent> GetDiscoveredRadios()
    {
        return _discoveredRadios.Values.Select(d => new RadioDiscoveredEvent(
            d.Id,
            RadioType.Tci,
            d.Model,
            d.IpAddress,
            d.TciPort,
            null,
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

        // Try parsing as key=value pairs
        var matches = Regex.Matches(text, @"(\w+)=([^\s;]+)");
        foreach (Match match in matches)
        {
            values[match.Groups[1].Value] = match.Groups[2].Value;
        }

        // Also try parsing as key:value pairs (some TCI implementations use this)
        var colonMatches = Regex.Matches(text, @"(\w+):([^\s;,]+)");
        foreach (Match match in colonMatches)
        {
            if (!values.ContainsKey(match.Groups[1].Value))
            {
                values[match.Groups[1].Value] = match.Groups[2].Value;
            }
        }

        return values;
    }
}

internal class TciRadioDevice
{
    public required string Id { get; set; }
    public required string Model { get; set; }
    public required string IpAddress { get; set; }
    public int TciPort { get; set; }
    public string? Version { get; set; }
    public int Instances { get; set; } = 1;
    public DateTime LastSeen { get; set; }
}

internal class TciRadioConnection
{
    private readonly TciRadioDevice _device;
    private readonly ILogger _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;

    private int _selectedInstance;
    private long _currentFrequencyHz;
    private string _currentMode = "USB";
    private bool _isTransmitting;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public TciRadioConnection(TciRadioDevice device, ILogger logger, IHubContext<LogHub, ILogHubClient> hubContext)
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
            _logger.LogInformation("Connecting to TCI radio at {Ip}:{Port}",
                _device.IpAddress, _device.TciPort);

            _webSocket = new ClientWebSocket();
            var uri = new Uri($"ws://{_device.IpAddress}:{_device.TciPort}");

            await _webSocket.ConnectAsync(uri, ct);

            _logger.LogInformation("Connected to TCI radio {Id}", _device.Id);

            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Connected));

            // Subscribe to frequency and mode updates
            await SendCommandAsync("vfo_frequency;");
            await SendCommandAsync("modulation;");
            await SendCommandAsync("trx;");

            // Start receive loop
            await ReceiveLoopAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TCI connection error for {Id}", _device.Id);
            await _hubContext.BroadcastRadioConnectionStateChanged(
                new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Error, ex.Message));
        }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel();
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
        }
        _webSocket?.Dispose();
        _webSocket = null;
    }

    public void SelectInstance(int instance)
    {
        _selectedInstance = instance;
        _logger.LogInformation("TCI radio {Id} monitoring instance {Instance}", _device.Id, instance);

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
            _selectedInstance.ToString()
        );
    }

    private async Task SendCommandAsync(string command)
    {
        if (_webSocket?.State != WebSocketState.Open) return;

        var buffer = Encoding.UTF8.GetBytes(command);
        await _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, _cts!.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("TCI connection closed by server");
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessageAsync(message);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TCI receive error");
                break;
            }
        }

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(_device.Id, RadioConnectionState.Disconnected));
    }

    private async Task ProcessMessageAsync(string message)
    {
        _logger.LogDebug("TCI message: {Message}", message);

        // TCI protocol format: command:receiver,value; or command:value;
        var parts = message.TrimEnd(';').Split(':');
        if (parts.Length < 2) return;

        var command = parts[0].ToLower();
        var args = parts[1].Split(',');

        // Check if this message is for our selected instance
        if (args.Length > 1 && int.TryParse(args[0], out var instance) && instance != _selectedInstance)
        {
            return; // Skip messages for other instances
        }

        switch (command)
        {
            case "vfo":
            case "vfo_frequency":
                // Format: vfo:receiver,frequency;
                var freqStr = args.Length > 1 ? args[1] : args[0];
                if (long.TryParse(freqStr, out var freq))
                {
                    if (freq != _currentFrequencyHz)
                    {
                        _currentFrequencyHz = freq;
                        await BroadcastStateAsync();
                    }
                }
                break;

            case "modulation":
                // Format: modulation:receiver,mode;
                var mode = (args.Length > 1 ? args[1] : args[0]).ToUpper();
                if (mode != _currentMode)
                {
                    _currentMode = mode;
                    await BroadcastStateAsync();
                }
                break;

            case "trx":
                // Format: trx:receiver,state; (true/false)
                var txStr = args.Length > 1 ? args[1] : args[0];
                var newTx = txStr.Equals("true", StringComparison.OrdinalIgnoreCase);
                if (newTx != _isTransmitting)
                {
                    _isTransmitting = newTx;
                    await BroadcastStateAsync();
                }
                break;
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
