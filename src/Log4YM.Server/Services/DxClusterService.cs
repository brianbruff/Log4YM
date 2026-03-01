using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public interface IDxClusterService
{
    Task ConnectClusterAsync(string clusterId);
    Task DisconnectClusterAsync(string clusterId);
    IReadOnlyDictionary<string, ClusterConnectionStatus> GetStatuses();
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}

public record ClusterConnectionStatus(
    string ClusterId,
    string Name,
    string Status,  // "connected" | "connecting" | "disconnected" | "error"
    string? ErrorMessage = null
);

public class DxClusterService : IDxClusterService, IHostedService, IDisposable
{
    private readonly ILogger<DxClusterService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISpotStatusService _spotStatusService;
    private readonly ConcurrentDictionary<string, ClusterConnectionHandler> _connections = new();
    private readonly ConcurrentDictionary<string, ClusterConnectionStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentSpots = new();
    private CancellationTokenSource? _cts;
    private Timer? _cleanupTimer;

    private const int DeduplicationWindowSeconds = 60;

    public DxClusterService(
        ILogger<DxClusterService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceProvider serviceProvider,
        ISpotStatusService spotStatusService)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _spotStatusService = spotStatusService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DX Cluster service starting...");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Clear any stale connections from previous session
        _connections.Clear();
        _statuses.Clear();

        // Start cleanup timer for deduplication cache
        _cleanupTimer = new Timer(CleanupRecentSpots, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

        // Auto-connect enabled clusters with autoReconnect
        await AutoConnectClustersAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DX Cluster service stopping...");
        _cts?.Cancel();

        // Disconnect all clusters
        foreach (var connection in _connections.Values)
        {
            await connection.DisconnectAsync();
        }

        _connections.Clear();
        _cleanupTimer?.Dispose();
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _cleanupTimer?.Dispose();
    }

    private async Task AutoConnectClustersAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepo.GetAsync();

            // If no cluster connections configured, create a default VE7CC entry
            if (settings?.Cluster?.Connections == null || settings.Cluster.Connections.Count == 0)
            {
                _logger.LogInformation("No cluster connections configured, creating default VE7CC cluster");
                await CreateDefaultClusterAsync(settingsRepo, settings);
                settings = await settingsRepo.GetAsync();
            }

            if (settings?.Cluster?.Connections == null) return;

            foreach (var cluster in settings.Cluster.Connections.Where(c => c.Enabled && c.AutoReconnect))
            {
                if (!string.IsNullOrEmpty(cluster.Host))
                {
                    _logger.LogInformation("Auto-connecting to cluster: {Name} ({Host}:{Port})",
                        cluster.Name, cluster.Host, cluster.Port);
                    _ = ConnectClusterInternalAsync(cluster, settings.Station?.Callsign);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-connect clusters");
        }
    }

    private async Task CreateDefaultClusterAsync(ISettingsRepository settingsRepo, UserSettings? existingSettings)
    {
        var defaultCluster = new ClusterConnection
        {
            Id = Guid.NewGuid().ToString(),
            Name = "VE7CC",
            Host = "dxc.ve7cc.net",
            Port = 23,
            Callsign = null, // Will use station callsign
            Enabled = true,
            AutoReconnect = true
        };

        if (existingSettings == null)
        {
            existingSettings = new UserSettings
            {
                Cluster = new ClusterSettings { Connections = new List<ClusterConnection> { defaultCluster } }
            };
        }
        else
        {
            existingSettings.Cluster ??= new ClusterSettings();
            existingSettings.Cluster.Connections ??= new List<ClusterConnection>();
            existingSettings.Cluster.Connections.Add(defaultCluster);
        }

        await settingsRepo.UpsertAsync(existingSettings);
        _logger.LogInformation("Default VE7CC cluster created and saved");
    }

    public async Task ConnectClusterAsync(string clusterId)
    {
        using var scope = _serviceProvider.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
        var settings = await settingsRepo.GetAsync();

        var cluster = settings?.Cluster?.Connections?.FirstOrDefault(c => c.Id == clusterId);
        if (cluster == null)
        {
            _logger.LogWarning("Cluster {ClusterId} not found in settings", clusterId);
            return;
        }

        // If already connected or connecting, disconnect first to allow reconnection
        if (_connections.TryGetValue(clusterId, out var existingHandler))
        {
            _logger.LogInformation("Cluster {ClusterId} already exists, disconnecting before reconnect", clusterId);
            await existingHandler.DisconnectAsync();
            _connections.TryRemove(clusterId, out _);
        }

        await ConnectClusterInternalAsync(cluster, settings?.Station?.Callsign);
    }

    private async Task ConnectClusterInternalAsync(ClusterConnection config, string? stationCallsign)
    {
        // Check if already connected - this shouldn't happen after fix in ConnectClusterAsync, but keep as safety
        if (_connections.ContainsKey(config.Id))
        {
            _logger.LogWarning("Cluster {Id} is already in connections map, skipping", config.Id);
            return;
        }

        var callsign = config.Callsign ?? stationCallsign ?? "LOG4YM";
        var handler = new ClusterConnectionHandler(
            config.Id,
            config.Name ?? "Unknown",
            config.Host,
            config.Port,
            callsign,
            config.Password,
            config.AutoReconnect,
            _logger,
            OnSpotReceived,
            OnStatusChanged
        );

        _connections[config.Id] = handler;

        await handler.ConnectAsync(_cts?.Token ?? CancellationToken.None);
    }

    public async Task DisconnectClusterAsync(string clusterId)
    {
        if (_connections.TryRemove(clusterId, out var handler))
        {
            await handler.DisconnectAsync();
        }

        UpdateStatus(clusterId, "Unknown", "disconnected", null);
    }

    public IReadOnlyDictionary<string, ClusterConnectionStatus> GetStatuses()
    {
        return _statuses;
    }

    private async Task OnSpotReceived(ParsedSpot spot, string clusterId, string clusterName)
    {
        // Deduplication: Check if we've seen this spot recently
        var dedupKey = GenerateDeduplicationKey(spot);
        if (!_recentSpots.TryAdd(dedupKey, DateTime.UtcNow))
        {
            _logger.LogDebug("Duplicate spot ignored: {DxCall} {Freq} from {Cluster}",
                spot.DxCall, spot.Frequency, clusterName);
            return;
        }

        // Save and broadcast
        await SaveAndBroadcastSpotAsync(spot, clusterName);
    }

    private static string GenerateDeduplicationKey(ParsedSpot spot)
    {
        // Key: DxCall + rounded frequency (to nearest kHz) + minute timestamp
        var roundedFreq = Math.Round(spot.Frequency);
        var minute = spot.Timestamp.Ticks / TimeSpan.TicksPerMinute;
        return $"{spot.DxCall.ToUpperInvariant()}:{roundedFreq}:{minute}";
    }

    private void CleanupRecentSpots(object? state)
    {
        var cutoff = DateTime.UtcNow.AddSeconds(-DeduplicationWindowSeconds);
        var keysToRemove = _recentSpots.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();

        foreach (var key in keysToRemove)
        {
            _recentSpots.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} expired deduplication entries", keysToRemove.Count);
        }
    }

    private void OnStatusChanged(string clusterId, string name, string status, string? errorMessage)
    {
        UpdateStatus(clusterId, name, status, errorMessage);
    }

    private void UpdateStatus(string clusterId, string name, string status, string? errorMessage)
    {
        var statusObj = new ClusterConnectionStatus(clusterId, name, status, errorMessage);
        _statuses[clusterId] = statusObj;

        // Broadcast to clients
        var evt = new ClusterStatusChangedEvent(clusterId, name, status, errorMessage);
        _ = _hubContext.BroadcastClusterStatusChanged(evt);
    }

    private async Task SaveAndBroadcastSpotAsync(ParsedSpot parsedSpot, string source)
    {
        // Generate a unique ephemeral ID for this spot
        var spotId = Guid.NewGuid().ToString();

        _logger.LogDebug("Spot: {DxCall} on {Freq} by {Spotter} - {Country} (from {Source})",
            parsedSpot.DxCall, parsedSpot.Frequency, parsedSpot.Spotter, parsedSpot.Country ?? "Unknown", source);

        // Lookup spotter location asynchronously (best effort, non-blocking)
        string? spotterGrid = null;
        string? spotterCountry = null;
        int? spotterDxcc = null;
        string? spotterContinent = null;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var rbnService = scope.ServiceProvider.GetService<IRbnService>();

            if (rbnService != null)
            {
                var (grid, _, _, country) = await rbnService.LookupSkimmerLocationAsync(parsedSpot.Spotter);
                spotterGrid = grid;
                spotterCountry = country;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Failed to lookup spotter location for {Spotter}: {Error}",
                parsedSpot.Spotter, ex.Message);
        }

        // Determine spot status (new DXCC, new band, worked, etc.)
        string? spotStatus = null;
        try
        {
            spotStatus = _spotStatusService.GetSpotStatus(
                parsedSpot.DxCall, parsedSpot.Country, parsedSpot.Frequency, parsedSpot.Mode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to determine spot status for {DxCall}", parsedSpot.DxCall);
        }

        // Broadcast to clients (spots are kept in memory on frontend only, not persisted)
        var evt = new SpotReceivedEvent(
            spotId,
            parsedSpot.DxCall,
            parsedSpot.Spotter,
            parsedSpot.Frequency,
            parsedSpot.Mode,
            parsedSpot.Comment,
            parsedSpot.Timestamp,
            source,
            parsedSpot.Country,
            parsedSpot.Dxcc,
            parsedSpot.Grid,
            spotterCountry,
            spotterDxcc,
            spotterGrid,
            spotterContinent,
            spotStatus
        );

        await _hubContext.Clients.All.OnSpotReceived(evt);
    }
}

internal record ParsedSpot(
    string DxCall,
    string Spotter,
    double Frequency,
    string? Mode,
    string? Comment,
    DateTime Timestamp,
    string? Country,
    string? Continent,
    int? Dxcc,
    string? Grid
);

internal class ClusterConnectionHandler
{
    private readonly string _id;
    private readonly string _name;
    private readonly string _host;
    private readonly int _port;
    private readonly string _callsign;
    private readonly string? _password;
    private readonly bool _autoReconnect;
    private readonly ILogger _logger;
    private readonly Func<ParsedSpot, string, string, Task> _onSpotReceived;
    private readonly Action<string, string, string, string?> _onStatusChanged;
    private TcpClient? _tcpClient;
    private CancellationTokenSource? _connectionCts;
    private bool _disconnectRequested;

    private const int ReconnectDelayMs = 10000;
    private const int ReadTimeoutMs = 120000;

    // VE7CC cluster format regex
    private static readonly Regex DxSpotRegex = new(
        @"^DX de ([A-Z0-9/]+):\s+(\d+\.?\d*)\s+([A-Z0-9/]+)\s+(.*?)\s+(\d{4})Z(?:\s+([A-Z]{2}\d{2}))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // VE7CC CC cluster extended format (CC11 format)
    private static readonly Regex CcSpotRegex = new(
        @"^CC\d+\^(\d+\.?\d*)\^([A-Z0-9/]+(?:-[A-Z0-9]+)?)\^[^\^]+\^(\d{4})Z?\^([^\^]*)\^([A-Z0-9/]+(?:-[#0-9]+)?)\^[^\^]*\^[^\^]*\^([^\^]*)\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^([^\^]*)\^[^\^]*\^([^\^]*)\^",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public ClusterConnectionHandler(
        string id,
        string name,
        string host,
        int port,
        string callsign,
        string? password,
        bool autoReconnect,
        ILogger logger,
        Func<ParsedSpot, string, string, Task> onSpotReceived,
        Action<string, string, string, string?> onStatusChanged)
    {
        _id = id;
        _name = name;
        _host = host;
        _port = port;
        _callsign = callsign;
        _password = password;
        _autoReconnect = autoReconnect;
        _logger = logger;
        _onSpotReceived = onSpotReceived;
        _onStatusChanged = onStatusChanged;
    }

    public async Task ConnectAsync(CancellationToken externalToken)
    {
        _disconnectRequested = false;
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var ct = _connectionCts.Token;

        while (!ct.IsCancellationRequested && !_disconnectRequested)
        {
            try
            {
                _onStatusChanged(_id, _name, "connecting", null);
                await ConnectAndReceiveAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested || _disconnectRequested)
            {
                _logger.LogInformation("Cluster {Name} disconnected", _name);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cluster {Name} connection error: {Message}", _name, ex.Message);
                _onStatusChanged(_id, _name, "error", ex.Message);
            }

            if (!ct.IsCancellationRequested && !_disconnectRequested)
            {
                if (_autoReconnect)
                {
                    _logger.LogInformation("Reconnecting to cluster {Name} in {Delay} seconds...",
                        _name, ReconnectDelayMs / 1000);
                    _onStatusChanged(_id, _name, "disconnected", "Reconnecting...");
                    try
                    {
                        await Task.Delay(ReconnectDelayMs, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                else
                {
                    _onStatusChanged(_id, _name, "disconnected", null);
                    break;
                }
            }
        }

        _onStatusChanged(_id, _name, "disconnected", null);
    }

    public async Task DisconnectAsync()
    {
        _disconnectRequested = true;
        _connectionCts?.Cancel();

        if (_tcpClient?.Connected == true)
        {
            _tcpClient.Close();
        }

        await Task.CompletedTask;
    }

    private async Task ConnectAndReceiveAsync(CancellationToken ct)
    {
        using var tcpClient = new TcpClient();
        _tcpClient = tcpClient;
        tcpClient.ReceiveTimeout = ReadTimeoutMs;

        _logger.LogInformation("Connecting to cluster {Name} at {Host}:{Port}...", _name, _host, _port);
        await tcpClient.ConnectAsync(_host, _port, ct);

        using var stream = tcpClient.GetStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

        _logger.LogInformation("Connected to cluster {Name}", _name);
        _onStatusChanged(_id, _name, "connected", null);

        var loginSent = false;
        var passwordSent = false;
        var ccModeEnabled = false;

        // Use chunk-based reading instead of ReadLineAsync to handle telnet-style
        // prompts (login:, password:) that don't end with newlines
        var readBuffer = new byte[4096];
        var pending = new StringBuilder();

        while (!ct.IsCancellationRequested && tcpClient.Connected && !_disconnectRequested)
        {
            var bytesRead = await stream.ReadAsync(readBuffer, ct);
            if (bytesRead == 0)
            {
                _logger.LogWarning("Cluster {Name} connection closed by server", _name);
                break;
            }

            pending.Append(Encoding.ASCII.GetString(readBuffer, 0, bytesRead));

            // Extract and process all complete lines (delimited by \n)
            var content = pending.ToString();
            var lastNewline = content.LastIndexOf('\n');

            if (lastNewline >= 0)
            {
                var completePart = content[..lastNewline];
                pending.Clear();
                pending.Append(content[(lastNewline + 1)..]);

                var lines = completePart.Split('\n');
                foreach (var rawLine in lines)
                {
                    var line = rawLine.TrimEnd('\r');
                    _logger.LogDebug("Cluster {Name} recv: {Line}", _name, line);

                    if (HandleInteractivePrompt(line, writer, ref loginSent, ref passwordSent,
                            ref ccModeEnabled, ct))
                        continue;

                    // Skip empty lines and prompts
                    if (string.IsNullOrWhiteSpace(line) || line.EndsWith(">"))
                        continue;

                    // Try to parse spot
                    await ProcessLineAsync(line);
                }
            }

            // Check the pending (incomplete) buffer for telnet-style prompts
            // that arrive without trailing newlines (e.g. "login: ", "password: ")
            var pendingText = pending.ToString();
            if (HandleInteractivePrompt(pendingText, writer, ref loginSent, ref passwordSent,
                    ref ccModeEnabled, ct))
            {
                pending.Clear();
            }
        }
    }

    /// <summary>
    /// Checks a chunk of text for interactive prompts (login, password, etc.)
    /// and responds accordingly. Returns true if a prompt was handled.
    /// </summary>
    private bool HandleInteractivePrompt(string text, StreamWriter writer,
        ref bool loginSent, ref bool passwordSent,
        ref bool ccModeEnabled,
        CancellationToken ct)
    {
        // Handle login prompt
        if (!loginSent && (text.Contains("login:") || text.Contains("call:") || text.Contains("Please enter your call")))
        {
            _logger.LogInformation("Sending login to {Name}: {Callsign}", _name, _callsign);
            writer.WriteLine(_callsign);
            loginSent = true;
            return true;
        }

        // Handle password prompt (for closed clusters)
        if (loginSent && !passwordSent && (text.Contains("password:") || text.Contains("Password:")))
        {
            if (!string.IsNullOrEmpty(_password))
            {
                _logger.LogInformation("Sending password to {Name}", _name);
                writer.WriteLine(_password);
            }
            else
            {
                _logger.LogWarning("Cluster {Name} requires password but none configured", _name);
                throw new Exception("Password required but not configured");
            }
            passwordSent = true;
            return true;
        }

        // Handle invalid callsign response
        if (text.Contains("not a valid callsign") || text.Contains("invalid call"))
        {
            _logger.LogError("Cluster {Name} rejected callsign '{Callsign}'", _name, _callsign);
            throw new Exception($"Invalid callsign: {_callsign}");
        }

        // Enable CC cluster mode for extended info after seeing the cluster prompt
        // DXSpider prompts end with ">" (e.g. "EI6LF de HB9VQQ-2 ... dxspider >")
        if (loginSent && !ccModeEnabled && text.TrimEnd().EndsWith(">"))
        {
            Task.Delay(500, ct).Wait(ct);
            _logger.LogInformation("Enabling CC cluster mode on {Name}", _name);
            writer.WriteLine("set/ve7cc");
            ccModeEnabled = true;
            return true;
        }

        // Note: No longer disabling skimmers/FT8 to maximize spot coverage
        // Previously filtered with "set/noskimmer" and "set/noft8"

        return false;
    }

    private async Task ProcessLineAsync(string line)
    {
        // Try CC cluster format first (has country info)
        var ccMatch = CcSpotRegex.Match(line);
        if (ccMatch.Success)
        {
            await ProcessCcSpotAsync(ccMatch);
            return;
        }

        // Try standard DX spot format
        var dxMatch = DxSpotRegex.Match(line);
        if (dxMatch.Success)
        {
            await ProcessDxSpotAsync(dxMatch);
            return;
        }
    }

    private async Task ProcessCcSpotAsync(Match match)
    {
        try
        {
            var frequency = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var dxCall = match.Groups[2].Value.ToUpper();
            var timeStr = match.Groups[3].Value;
            var comment = match.Groups[4].Value.Trim();
            var spotter = match.Groups[5].Value.ToUpper().TrimEnd('-', '#', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            var source = match.Groups[6].Value;
            var country = match.Groups[7].Value;
            var grid = match.Groups[8].Value;

            var mode = ExtractMode(comment) ?? InferModeFromFrequency(frequency);
            var timestamp = ParseSpotTime(timeStr);
            // CC clusters provide country name directly; look up continent from CtyService
            var countryName = !string.IsNullOrEmpty(country) ? country : null;
            var continent = countryName != null ? CtyService.GetContinentFromCountryName(countryName) : null;

            // If continent lookup failed, the "country" field is likely a DXCC prefix (e.g. "J5")
            // rather than a real country name — fall back to callsign lookup for both
            if (continent == null)
            {
                var (ctyCountry, ctyContinent) = CtyService.GetCountryFromCallsign(dxCall);
                countryName = ctyCountry;
                continent = ctyContinent;
            }

            var spot = new ParsedSpot(
                dxCall, spotter, frequency, mode, comment, timestamp,
                countryName, continent, null, grid
            );

            await _onSpotReceived(spot, _id, _name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse CC spot: {Line}", match.Value);
        }
    }

    private async Task ProcessDxSpotAsync(Match match)
    {
        try
        {
            var spotter = match.Groups[1].Value.ToUpper();
            var frequency = double.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            var dxCall = match.Groups[3].Value.ToUpper();
            var comment = match.Groups[4].Value.Trim();
            var timeStr = match.Groups[5].Value;
            var grid = match.Groups[6].Success ? match.Groups[6].Value.ToUpper() : null;

            var mode = ExtractMode(comment) ?? InferModeFromFrequency(frequency);
            var timestamp = ParseSpotTime(timeStr);

            var (countryName, continent) = CtyService.GetCountryFromCallsign(dxCall);

            var spot = new ParsedSpot(
                dxCall, spotter, frequency, mode, comment, timestamp,
                countryName, continent, null, grid
            );

            await _onSpotReceived(spot, _id, _name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse DX spot: {Line}", match.Value);
        }
    }

    private static string? ExtractMode(string comment)
    {
        var commentUpper = comment.ToUpper();

        if (commentUpper.Contains("FT8")) return "FT8";
        if (commentUpper.Contains("FT4")) return "FT4";
        if (commentUpper.Contains("RTTY")) return "RTTY";
        if (commentUpper.Contains("PSK")) return "PSK31";
        if (commentUpper.Contains("SSB") || commentUpper.Contains("USB") || commentUpper.Contains("LSB")) return "SSB";
        if (commentUpper.Contains("CW")) return "CW";
        if (commentUpper.Contains("AM")) return "AM";
        if (commentUpper.Contains("FM")) return "FM";
        if (commentUpper.Contains("DIGI")) return "DIGI";
        if (commentUpper.Contains("JT65")) return "JT65";
        if (commentUpper.Contains("JT9")) return "JT9";

        return null;
    }

    private static string InferModeFromFrequency(double frequencyKhz)
    {
        // Check for common FT8 frequencies first (digital mode sub-bands)
        // FT8 typically operates on these frequencies per band (±2 kHz tolerance)
        if (Math.Abs(frequencyKhz - 1840) < 2) return "FT8";      // 160m
        if (Math.Abs(frequencyKhz - 3573) < 2) return "FT8";      // 80m
        if (Math.Abs(frequencyKhz - 7074) < 2) return "FT8";      // 40m
        if (Math.Abs(frequencyKhz - 10136) < 2) return "FT8";     // 30m
        if (Math.Abs(frequencyKhz - 14074) < 2) return "FT8";     // 20m
        if (Math.Abs(frequencyKhz - 18100) < 2) return "FT8";     // 17m
        if (Math.Abs(frequencyKhz - 21074) < 2) return "FT8";     // 15m
        if (Math.Abs(frequencyKhz - 24915) < 2) return "FT8";     // 12m
        if (Math.Abs(frequencyKhz - 28074) < 2) return "FT8";     // 10m
        if (Math.Abs(frequencyKhz - 50313) < 2) return "FT8";     // 6m

        // Standard band plan inference
        if (frequencyKhz >= 1800 && frequencyKhz < 2000)
            return frequencyKhz >= 1843 ? "SSB" : "CW";
        if (frequencyKhz >= 3500 && frequencyKhz < 4000)
            return frequencyKhz >= 3600 ? "SSB" : "CW";
        if (frequencyKhz >= 7000 && frequencyKhz < 7300)
            return frequencyKhz >= 7125 ? "SSB" : "CW";
        if (frequencyKhz >= 10100 && frequencyKhz < 10150)
            return "CW";
        if (frequencyKhz >= 14000 && frequencyKhz < 14350)
            return frequencyKhz >= 14150 ? "SSB" : "CW";
        if (frequencyKhz >= 18068 && frequencyKhz < 18168)
            return frequencyKhz >= 18110 ? "SSB" : "CW";
        if (frequencyKhz >= 21000 && frequencyKhz < 21450)
            return frequencyKhz >= 21200 ? "SSB" : "CW";
        if (frequencyKhz >= 24890 && frequencyKhz < 24990)
            return frequencyKhz >= 24930 ? "SSB" : "CW";
        if (frequencyKhz >= 28000 && frequencyKhz < 29700)
            return frequencyKhz >= 28300 ? "SSB" : "CW";
        if (frequencyKhz >= 50000 && frequencyKhz < 54000)
            return frequencyKhz >= 50100 ? "SSB" : "CW";

        return "SSB";
    }

    private static DateTime ParseSpotTime(string timeStr)
    {
        if (timeStr.Length == 4 &&
            int.TryParse(timeStr.Substring(0, 2), out var hours) &&
            int.TryParse(timeStr.Substring(2, 2), out var minutes))
        {
            var now = DateTime.UtcNow;
            var spotTime = new DateTime(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Utc);

            if (spotTime > now.AddMinutes(5))
            {
                spotTime = spotTime.AddDays(-1);
            }

            return spotTime;
        }

        return DateTime.UtcNow;
    }

}
