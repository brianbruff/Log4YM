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
    private readonly ConcurrentDictionary<string, ClusterConnectionHandler> _connections = new();
    private readonly ConcurrentDictionary<string, ClusterConnectionStatus> _statuses = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentSpots = new();
    private CancellationTokenSource? _cts;
    private Timer? _cleanupTimer;

    private const int DeduplicationWindowSeconds = 60;

    public DxClusterService(
        ILogger<DxClusterService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("DX Cluster service starting...");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

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

        await ConnectClusterInternalAsync(cluster, settings?.Station?.Callsign);
    }

    private async Task ConnectClusterInternalAsync(ClusterConnection config, string? stationCallsign)
    {
        if (_connections.ContainsKey(config.Id))
        {
            _logger.LogWarning("Cluster {Id} is already connected or connecting", config.Id);
            return;
        }

        var callsign = config.Callsign ?? stationCallsign ?? "LOG4YM";
        var handler = new ClusterConnectionHandler(
            config.Id,
            config.Name ?? "Unknown",
            config.Host,
            config.Port,
            callsign,
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
            parsedSpot.Dxcc
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
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

        _logger.LogInformation("Connected to cluster {Name}", _name);
        _onStatusChanged(_id, _name, "connected", null);

        var loginSent = false;
        var ccModeEnabled = false;
        var ft8Enabled = false;

        while (!ct.IsCancellationRequested && tcpClient.Connected && !_disconnectRequested)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                _logger.LogWarning("Cluster {Name} connection closed by server", _name);
                break;
            }

            // Log all incoming lines for debugging
            _logger.LogDebug("Cluster {Name} recv: {Line}", _name, line);

            // Handle login prompt
            if (!loginSent && (line.Contains("login:") || line.Contains("call:") || line.Contains("Please enter your call")))
            {
                _logger.LogInformation("Sending login to {Name}: {Callsign}", _name, _callsign);
                await writer.WriteLineAsync(_callsign);
                loginSent = true;
                continue;
            }

            // Enable CC cluster mode for extended info after seeing the prompt
            if (loginSent && !ccModeEnabled && line.Contains("CCC >"))
            {
                await Task.Delay(500, ct);
                _logger.LogInformation("Enabling CC cluster mode on {Name}", _name);
                await writer.WriteLineAsync("set/ve7cc");
                ccModeEnabled = true;
                continue;
            }

            // Disable skimmers after CC mode is confirmed
            if (ccModeEnabled && !ft8Enabled && line.Contains("Enhanced Spots Enabled"))
            {
                await Task.Delay(200, ct);
                await writer.WriteLineAsync("set/noskimmer");
                await Task.Delay(200, ct);
                await writer.WriteLineAsync("set/noft8");
                ft8Enabled = true;
                _logger.LogInformation("Skimmers disabled on {Name}", _name);
                continue;
            }

            // Handle invalid callsign response
            if (line.Contains("not a valid callsign") || line.Contains("invalid call"))
            {
                _logger.LogError("Cluster {Name} rejected callsign '{Callsign}'", _name, _callsign);
                throw new Exception($"Invalid callsign: {_callsign}");
            }

            // Skip empty lines and prompts
            if (string.IsNullOrWhiteSpace(line) || line.EndsWith(">"))
            {
                continue;
            }

            // Try to parse spot
            await ProcessLineAsync(line);
        }
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
            var (countryName, continent) = GetCountryFromPrefix(country);

            var spot = new ParsedSpot(
                dxCall, spotter, frequency, mode, comment, timestamp,
                countryName ?? country, continent, null, grid
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

            var spot = new ParsedSpot(
                dxCall, spotter, frequency, mode, comment, timestamp,
                null, null, null, grid
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

    private static readonly Dictionary<string, (string Country, string Continent)> PrefixToCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        ["K"] = ("United States", "NA"), ["W"] = ("United States", "NA"), ["N"] = ("United States", "NA"), ["A"] = ("United States", "NA"),
        ["VE"] = ("Canada", "NA"), ["VA"] = ("Canada", "NA"), ["VY"] = ("Canada", "NA"),
        ["XE"] = ("Mexico", "NA"), ["XA"] = ("Mexico", "NA"),
        ["KH6"] = ("Hawaii", "OC"), ["KL7"] = ("Alaska", "NA"), ["KP4"] = ("Puerto Rico", "NA"),

        // South America
        ["PY"] = ("Brazil", "SA"), ["PP"] = ("Brazil", "SA"), ["PT"] = ("Brazil", "SA"), ["PU"] = ("Brazil", "SA"),
        ["LU"] = ("Argentina", "SA"), ["CE"] = ("Chile", "SA"), ["CX"] = ("Uruguay", "SA"),
        ["HC"] = ("Ecuador", "SA"), ["OA"] = ("Peru", "SA"), ["HK"] = ("Colombia", "SA"), ["YV"] = ("Venezuela", "SA"),
        ["CP"] = ("Bolivia", "SA"), ["ZP"] = ("Paraguay", "SA"),

        // Europe
        ["G"] = ("United Kingdom", "EU"), ["M"] = ("United Kingdom", "EU"), ["2E"] = ("United Kingdom", "EU"),
        ["GI"] = ("Northern Ireland", "EU"), ["GM"] = ("Scotland", "EU"), ["GW"] = ("Wales", "EU"),
        ["DL"] = ("Germany", "EU"), ["DA"] = ("Germany", "EU"), ["DB"] = ("Germany", "EU"), ["DC"] = ("Germany", "EU"), ["DD"] = ("Germany", "EU"), ["DF"] = ("Germany", "EU"), ["DG"] = ("Germany", "EU"), ["DH"] = ("Germany", "EU"), ["DJ"] = ("Germany", "EU"), ["DK"] = ("Germany", "EU"), ["DM"] = ("Germany", "EU"), ["DO"] = ("Germany", "EU"),
        ["F"] = ("France", "EU"), ["I"] = ("Italy", "EU"), ["EA"] = ("Spain", "EU"), ["CT"] = ("Portugal", "EU"),
        ["PA"] = ("Netherlands", "EU"), ["PD"] = ("Netherlands", "EU"), ["PE"] = ("Netherlands", "EU"), ["PH"] = ("Netherlands", "EU"),
        ["ON"] = ("Belgium", "EU"), ["OZ"] = ("Denmark", "EU"), ["SM"] = ("Sweden", "EU"), ["SA"] = ("Sweden", "EU"),
        ["OH"] = ("Finland", "EU"), ["LA"] = ("Norway", "EU"), ["SP"] = ("Poland", "EU"), ["SQ"] = ("Poland", "EU"),
        ["OK"] = ("Czech Republic", "EU"), ["OL"] = ("Czech Republic", "EU"), ["HA"] = ("Hungary", "EU"),
        ["YO"] = ("Romania", "EU"), ["LZ"] = ("Bulgaria", "EU"), ["SV"] = ("Greece", "EU"),
        ["9A"] = ("Croatia", "EU"), ["S5"] = ("Slovenia", "EU"), ["OE"] = ("Austria", "EU"),
        ["HB"] = ("Switzerland", "EU"), ["HB0"] = ("Liechtenstein", "EU"),
        ["UA"] = ("Russia", "EU"), ["RA"] = ("Russia", "EU"), ["R"] = ("Russia", "EU"), ["RU"] = ("Russia", "EU"), ["RV"] = ("Russia", "EU"), ["RW"] = ("Russia", "EU"), ["RX"] = ("Russia", "EU"), ["RZ"] = ("Russia", "EU"),
        ["UR"] = ("Ukraine", "EU"), ["UT"] = ("Ukraine", "EU"), ["UX"] = ("Ukraine", "EU"), ["US"] = ("Ukraine", "EU"),
        ["EI"] = ("Ireland", "EU"), ["EW"] = ("Belarus", "EU"), ["EU"] = ("Belarus", "EU"), ["ES"] = ("Estonia", "EU"),
        ["YL"] = ("Latvia", "EU"), ["LY"] = ("Lithuania", "EU"), ["OM"] = ("Slovakia", "EU"),
        ["YU"] = ("Serbia", "EU"), ["Z3"] = ("North Macedonia", "EU"), ["ZA"] = ("Albania", "EU"),
        ["E7"] = ("Bosnia-Herzegovina", "EU"), ["4O"] = ("Montenegro", "EU"),

        // Asia
        ["JA"] = ("Japan", "AS"), ["JH"] = ("Japan", "AS"), ["JR"] = ("Japan", "AS"), ["JE"] = ("Japan", "AS"), ["JF"] = ("Japan", "AS"), ["JG"] = ("Japan", "AS"), ["JI"] = ("Japan", "AS"), ["JJ"] = ("Japan", "AS"), ["JK"] = ("Japan", "AS"), ["JL"] = ("Japan", "AS"), ["JM"] = ("Japan", "AS"), ["JN"] = ("Japan", "AS"), ["JO"] = ("Japan", "AS"), ["JP"] = ("Japan", "AS"), ["JQ"] = ("Japan", "AS"), ["JS"] = ("Japan", "AS"),
        ["HL"] = ("South Korea", "AS"), ["DS"] = ("South Korea", "AS"),
        ["BY"] = ("China", "AS"), ["BV"] = ("Taiwan", "AS"), ["VU"] = ("India", "AS"),
        ["HS"] = ("Thailand", "AS"), ["9M2"] = ("Malaysia", "AS"), ["9M"] = ("Malaysia", "AS"), ["9V"] = ("Singapore", "AS"),
        ["DU"] = ("Philippines", "AS"), ["YB"] = ("Indonesia", "AS"), ["XV"] = ("Vietnam", "AS"),
        ["UN"] = ("Kazakhstan", "AS"), ["UK"] = ("Uzbekistan", "AS"),
        ["A4"] = ("Oman", "AS"), ["A6"] = ("UAE", "AS"), ["A7"] = ("Qatar", "AS"), ["A9"] = ("Bahrain", "AS"),
        ["HZ"] = ("Saudi Arabia", "AS"), ["7Z"] = ("Saudi Arabia", "AS"), ["9K"] = ("Kuwait", "AS"),
        ["4X"] = ("Israel", "AS"), ["OD"] = ("Lebanon", "AS"), ["TA"] = ("Turkey", "AS"),

        // Africa
        ["ZS"] = ("South Africa", "AF"), ["5Z"] = ("Kenya", "AF"), ["5H"] = ("Tanzania", "AF"),
        ["9J"] = ("Zambia", "AF"), ["7Q"] = ("Malawi", "AF"), ["A2"] = ("Botswana", "AF"),
        ["V5"] = ("Namibia", "AF"), ["3B"] = ("Mauritius", "AF"), ["TR"] = ("Gabon", "AF"),
        ["TU"] = ("Ivory Coast", "AF"), ["6W"] = ("Senegal", "AF"), ["CN"] = ("Morocco", "AF"),
        ["SU"] = ("Egypt", "AF"), ["ST"] = ("Sudan", "AF"), ["ET"] = ("Ethiopia", "AF"),
        ["5N"] = ("Nigeria", "AF"), ["5U"] = ("Niger", "AF"), ["TZ"] = ("Mali", "AF"),
        ["9G"] = ("Ghana", "AF"), ["EL"] = ("Liberia", "AF"),

        // Oceania
        ["VK"] = ("Australia", "OC"), ["ZL"] = ("New Zealand", "OC"),
        ["P2"] = ("Papua New Guinea", "OC"), ["3D"] = ("Fiji", "OC"), ["FK"] = ("New Caledonia", "OC"),
        ["KH2"] = ("Guam", "OC"), ["V7"] = ("Marshall Islands", "OC"), ["T8"] = ("Palau", "OC"),

        // Caribbean
        ["VP2M"] = ("Montserrat", "NA"), ["VP2V"] = ("British Virgin Islands", "NA"), ["VP2E"] = ("Anguilla", "NA"),
        ["8P"] = ("Barbados", "NA"), ["J3"] = ("Grenada", "NA"), ["J6"] = ("Saint Lucia", "NA"),
        ["J7"] = ("Dominica", "NA"), ["J8"] = ("Saint Vincent", "NA"), ["PJ"] = ("Netherlands Antilles", "NA"),
        ["HI"] = ("Dominican Republic", "NA"), ["CO"] = ("Cuba", "NA"), ["6Y"] = ("Jamaica", "NA"),
        ["9Y"] = ("Trinidad & Tobago", "SA"), ["8R"] = ("Guyana", "SA"),

        // Additional
        ["8Q"] = ("Maldives", "AS"), ["4S"] = ("Sri Lanka", "AS"), ["AP"] = ("Pakistan", "AS"),
        ["YT"] = ("Serbia", "EU"),
        ["B"] = ("China", "AS"), ["BD"] = ("China", "AS"), ["BH"] = ("China", "AS"),
        ["BU"] = ("Taiwan", "AS"),
    };

    private static (string? Country, string? Continent) GetCountryFromPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return (null, null);

        if (PrefixToCountry.TryGetValue(prefix, out var result))
            return result;

        for (int len = Math.Min(prefix.Length, 4); len >= 1; len--)
        {
            var shortPrefix = prefix.Substring(0, len);
            if (PrefixToCountry.TryGetValue(shortPrefix, out result))
                return result;
        }

        return (null, null);
    }
}
