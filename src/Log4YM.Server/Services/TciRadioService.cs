using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

/// <summary>
/// Service for TCI (Thetis, Hermes, ANAN) radio discovery and CAT control
/// </summary>
public class TciRadioService : BackgroundService
{
    private readonly ILogger<TciRadioService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
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
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TCI Radio service starting...");

        // Clear any stale connections and discoveries from previous session
        _connections.Clear();
        _discoveredRadios.Clear();

        // Auto-connect to TCI if configured
        await TryAutoConnectAsync();

        // Run cleanup task periodically
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupStaleRadiosAsync();
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task TryAutoConnectAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepository.GetAsync();
            var radioSettings = settings?.Radio;
            var tciSettings = radioSettings?.Tci;

            // Check unified autoReconnect flag and activeRigType
            if (radioSettings is { AutoReconnect: true, ActiveRigType: "tci" }
                && tciSettings != null
                && !string.IsNullOrEmpty(tciSettings.Host))
            {
                _logger.LogInformation("Auto-reconnecting to TCI at {Host}:{Port}", tciSettings.Host, tciSettings.Port);
                await ConnectDirectAsync(tciSettings.Host, tciSettings.Port, tciSettings.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-connect to TCI");
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
            .Where(r => !_connections.ContainsKey(r.Id)) // Don't remove connected radios
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

    public bool HasRadio(string radioId) => _discoveredRadios.ContainsKey(radioId) || _connections.ContainsKey(radioId);

    /// <summary>
    /// Connect directly to a TCI server at a known host:port without discovery
    /// </summary>
    public async Task ConnectDirectAsync(string host, int port = TciDefaultPort, string? name = null)
    {
        var radioId = $"tci-{host}:{port}";

        // If already connected or connecting, disconnect first to allow reconnection
        if (_connections.TryRemove(radioId, out var existingConnection))
        {
            _logger.LogInformation("TCI radio {RadioId} already exists, disconnecting before reconnect", radioId);
            await existingConnection.DisconnectAsync();
        }

        // Create a device entry for direct connection
        var device = new TciRadioDevice
        {
            Id = radioId,
            Model = name ?? $"TCI ({host})",
            IpAddress = host,
            TciPort = port,
            Instances = 1,
            LastSeen = DateTime.UtcNow
        };

        _discoveredRadios[radioId] = device;

        await _hubContext.BroadcastRadioDiscovered(new RadioDiscoveredEvent(
            radioId,
            RadioType.Tci,
            device.Model,
            host,
            port,
            null,
            null
        ));

        await _hubContext.BroadcastRadioConnectionStateChanged(
            new RadioConnectionStateChangedEvent(radioId, RadioConnectionState.Connecting));

        var connection = new TciRadioConnection(device, _logger, _hubContext);
        if (_connections.TryAdd(radioId, connection))
        {
            _ = connection.ConnectAsync();
        }
    }

    public async Task ConnectAsync(string radioId)
    {
        if (!_discoveredRadios.TryGetValue(radioId, out var device))
        {
            _logger.LogWarning("TCI radio {RadioId} not found", radioId);
            return;
        }

        // If already connected or connecting, disconnect first to allow reconnection
        if (_connections.TryRemove(radioId, out var existingConnection))
        {
            _logger.LogInformation("TCI radio {RadioId} already exists, disconnecting before reconnect", radioId);
            await existingConnection.DisconnectAsync();
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

    public async Task<bool> SetFrequencyAsync(string radioId, long frequencyHz)
    {
        if (!_connections.TryGetValue(radioId, out var connection))
        {
            _logger.LogWarning("Cannot set frequency: TCI radio {RadioId} not connected", radioId);
            return false;
        }

        return await connection.SetFrequencyAsync(frequencyHz);
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

    /// <summary>
    /// Get discovered radios including saved TCI config from settings when no active connections exist
    /// </summary>
    public async Task<IEnumerable<RadioDiscoveredEvent>> GetDiscoveredRadiosAsync()
    {
        var radios = GetDiscoveredRadios().ToList();

        // If we already have discovered/connected TCI radios, no need to load from settings
        if (radios.Count > 0) return radios;

        // Load saved TCI config from settings
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepository.GetAsync();
            var tciSettings = settings?.Radio?.Tci;

            if (tciSettings != null && !string.IsNullOrEmpty(tciSettings.Host))
            {
                var radioId = $"tci-{tciSettings.Host}:{tciSettings.Port}";
                radios.Add(new RadioDiscoveredEvent(
                    radioId,
                    RadioType.Tci,
                    !string.IsNullOrEmpty(tciSettings.Name) ? tciSettings.Name : $"TCI ({tciSettings.Host})",
                    tciSettings.Host,
                    tciSettings.Port,
                    null,
                    null
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load saved TCI config from settings");
        }

        return radios;
    }

    public IEnumerable<RadioStateChangedEvent> GetRadioStates()
    {
        return _connections.Values
            .Where(c => c.IsConnected)
            .Select(c => c.GetCurrentState())
            .Where(s => s != null)!;
    }

    public IEnumerable<RadioConnectionStateChangedEvent> GetConnectionStates()
    {
        return _connections.Select(kvp => new RadioConnectionStateChangedEvent(
            kvp.Key,
            kvp.Value.IsConnected ? RadioConnectionState.Connected : RadioConnectionState.Disconnected
        ));
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

            // TCI protocol: server pushes updates to us automatically
            // No need to send subscription commands - just listen for incoming messages

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

    public async Task<bool> SetFrequencyAsync(long frequencyHz)
    {
        if (!IsConnected) return false;

        // TCI protocol: vfo:rx,channel,frequency; (rx=0/1, channel=0 for VFO-A)
        var command = $"vfo:{_selectedInstance},0,{frequencyHz};";
        _logger.LogDebug("Sending TCI command: {Command}", command);
        await SendCommandAsync(command);
        return true;
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

        // TCI protocol format: command:arg1,arg2,...;command2:arg1,...;
        // Multiple commands can be in one message, separated by semicolons
        var commands = message.Trim().Split(';', StringSplitOptions.RemoveEmptyEntries);

        var stateChanged = false;

        foreach (var cmd in commands)
        {
            var colonIndex = cmd.IndexOf(':');
            if (colonIndex < 0) continue;

            var command = cmd[..colonIndex].ToLower().Trim();
            var argsStr = cmd[(colonIndex + 1)..];
            var args = argsStr.Split(',').Select(a => a.Trim()).ToArray();

            switch (command)
            {
                case "vfo":
                    // Format: vfo:rx,channel,frequency; (rx=0/1, channel=0 for VFO-A, frequency in Hz)
                    if (args.Length >= 3)
                    {
                        var rx = int.TryParse(args[0], out var rxVal) ? rxVal : 0;
                        var channel = int.TryParse(args[1], out var chVal) ? chVal : 0;

                        // Only track RX0, VFO-A (channel 0) or match selected instance
                        if (rx == _selectedInstance && channel == 0)
                        {
                            if (long.TryParse(args[2], out var freq) && freq != _currentFrequencyHz)
                            {
                                _currentFrequencyHz = freq;
                                stateChanged = true;
                            }
                        }
                    }
                    break;

                case "modulation":
                    // Format: modulation:rx,MODE;
                    if (args.Length >= 2)
                    {
                        var rx = int.TryParse(args[0], out var rxVal) ? rxVal : 0;
                        if (rx == _selectedInstance)
                        {
                            var mode = args[1].ToUpper();
                            if (mode != _currentMode)
                            {
                                _currentMode = mode;
                                stateChanged = true;
                            }
                        }
                    }
                    break;

                case "trx":
                    // Format: trx:rx,state; (state: true/false or 1/0)
                    if (args.Length >= 2)
                    {
                        var rx = int.TryParse(args[0], out var rxVal) ? rxVal : 0;
                        if (rx == _selectedInstance)
                        {
                            var newTx = args[1].Equals("true", StringComparison.OrdinalIgnoreCase)
                                     || args[1] == "1";
                            if (newTx != _isTransmitting)
                            {
                                _isTransmitting = newTx;
                                stateChanged = true;
                            }
                        }
                    }
                    break;

                case "tx":
                    // Alternative TX format: tx:state;
                    if (args.Length >= 1)
                    {
                        var newTx = args[0].Equals("true", StringComparison.OrdinalIgnoreCase)
                                 || args[0] == "1";
                        if (newTx != _isTransmitting)
                        {
                            _isTransmitting = newTx;
                            stateChanged = true;
                        }
                    }
                    break;

                case "protocol":
                    // Server identification: protocol:name,version;
                    _logger.LogInformation("TCI protocol: {Args}", argsStr);
                    break;

                case "device":
                    // Device name: device:name;
                    _logger.LogInformation("TCI device: {Args}", argsStr);
                    break;

                case "ready":
                    // Server ready signal
                    _logger.LogInformation("TCI server ready");
                    break;
            }
        }

        if (stateChanged)
        {
            await BroadcastStateAsync();
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
