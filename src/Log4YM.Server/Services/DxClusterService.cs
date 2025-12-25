using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public class DxClusterService : BackgroundService
{
    private readonly ILogger<DxClusterService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    private const int ReconnectDelayMs = 10000;
    private const int ReadTimeoutMs = 120000; // 2 minutes

    // VE7CC cluster format regex
    // Example: DX de W3LPL:     14025.0  JA1ABC       CW UP 10                        2359Z
    // Example: DX de K3LR:      21074.0  VK2ABC       FT8                             0001Z JO32
    private static readonly Regex DxSpotRegex = new(
        @"^DX de ([A-Z0-9/]+):\s+(\d+\.?\d*)\s+([A-Z0-9/]+)\s+(.*?)\s+(\d{4})Z(?:\s+([A-Z]{2}\d{2}))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // VE7CC CC cluster extended format (CC11 format)
    // Example: CC11^7074^AE1AA^12-Dec-2025^0019Z^FT8  -14 dB 1562 Hz^N2CR-#^1^^RBNFT8^8^5^8^5^NH^NJ^K^K^FN43^FN20^40^43.5/71^
    // Fields: Type^Freq^DxCall^Date^Time^Comment^Spotter^?^?^Source^CQ^ITU^CQ^ITU^State^State^Country^Country^Grid^Grid^Band^Lat/Lon
    private static readonly Regex CcSpotRegex = new(
        @"^CC\d+\^(\d+\.?\d*)\^([A-Z0-9/]+(?:-[A-Z0-9]+)?)\^[^\^]+\^(\d{4})Z?\^([^\^]*)\^([A-Z0-9/]+(?:-[#0-9]+)?)\^[^\^]*\^[^\^]*\^([^\^]*)\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^([^\^]*)\^[^\^]*\^([^\^]*)\^",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public DxClusterService(
        ILogger<DxClusterService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DX Cluster service starting...");

        // Get cluster configuration
        var clusterHost = _configuration.GetValue<string>("DxCluster:Host") ?? "de.ve7cc.net";
        var clusterPort = _configuration.GetValue<int>("DxCluster:Port", 23);
        var callsign = _configuration.GetValue<string>("DxCluster:Callsign") ?? "LOG4YM";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndReceiveAsync(clusterHost, clusterPort, callsign, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("DX Cluster service stopping...");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DX Cluster connection error");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Reconnecting to DX Cluster in {Delay} seconds...", ReconnectDelayMs / 1000);
                await Task.Delay(ReconnectDelayMs, stoppingToken);
            }
        }
    }

    private async Task ConnectAndReceiveAsync(string host, int port, string callsign, CancellationToken ct)
    {
        using var tcpClient = new TcpClient();
        tcpClient.ReceiveTimeout = ReadTimeoutMs;

        _logger.LogInformation("Connecting to DX Cluster {Host}:{Port}...", host, port);
        await tcpClient.ConnectAsync(host, port, ct);

        using var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII);
        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true };

        _logger.LogInformation("Connected to DX Cluster {Host}:{Port}", host, port);

        // Wait for login prompt and send callsign
        var loginSent = false;
        var ccModeEnabled = false;
        var ft8Enabled = false;

        while (!ct.IsCancellationRequested && tcpClient.Connected)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                _logger.LogWarning("DX Cluster connection closed by server");
                break;
            }

            // Log all incoming lines for debugging
            _logger.LogInformation("Cluster recv: {Line}", line);

            // Handle login prompt
            if (!loginSent && (line.Contains("login:") || line.Contains("call:") || line.Contains("Please enter your call")))
            {
                _logger.LogInformation("Sending login: {Callsign}", callsign);
                await writer.WriteLineAsync(callsign);
                loginSent = true;
                continue;
            }

            // Enable CC cluster mode for extended info after seeing the prompt
            if (loginSent && !ccModeEnabled && line.Contains("CCC >"))
            {
                await Task.Delay(500, ct);
                _logger.LogInformation("Enabling CC cluster mode (human spots only - no skimmers)");
                await writer.WriteLineAsync("set/ve7cc");
                ccModeEnabled = true;
                continue;
            }

            // Disable skimmers after CC mode is confirmed to get human-spotted calls only
            // Human spots include SSB/Phone which skimmers cannot detect
            if (ccModeEnabled && !ft8Enabled && line.Contains("Enhanced Spots Enabled"))
            {
                await Task.Delay(200, ct);
                // Explicitly disable skimmers to only receive human-spotted DX
                await writer.WriteLineAsync("set/noskimmer");
                await Task.Delay(200, ct);
                await writer.WriteLineAsync("set/noft8");
                ft8Enabled = true;
                _logger.LogInformation("Skimmers disabled - receiving human spots only (includes SSB/Phone)");
                continue;
            }

            // Handle invalid callsign response
            if (line.Contains("not a valid callsign") || line.Contains("invalid call"))
            {
                _logger.LogError("Cluster rejected callsign '{Callsign}'. Please configure a valid callsign in appsettings.json under DxCluster:Callsign", callsign);
                throw new Exception($"Invalid callsign: {callsign}");
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

        // Log unrecognized lines for debugging (but not system messages)
        if (!line.StartsWith("DX de") && !line.StartsWith("CC") && !line.Contains("***"))
        {
            _logger.LogDebug("Unrecognized cluster line: {Line}", line);
        }
    }

    private async Task ProcessCcSpotAsync(Match match)
    {
        try
        {
            // Groups from regex: 1=freq, 2=dxCall, 3=time, 4=comment, 5=spotter, 6=source, 7=country, 8=grid
            var frequency = double.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var dxCall = match.Groups[2].Value.ToUpper();
            var timeStr = match.Groups[3].Value;
            var comment = match.Groups[4].Value.Trim();
            var spotter = match.Groups[5].Value.ToUpper().TrimEnd('-', '#', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            var source = match.Groups[6].Value;
            var country = match.Groups[7].Value;
            var grid = match.Groups[8].Value;

            // Parse mode from comment, or infer from frequency
            var mode = ExtractMode(comment) ?? InferModeFromFrequency(frequency);

            // Create timestamp from time string (HHMM format, assume today)
            var timestamp = ParseSpotTime(timeStr);

            // Map country prefix to full country name
            var (countryName, continent) = GetCountryFromPrefix(country);

            _logger.LogInformation("Spot: {DxCall} {Freq} {Mode} by {Spotter} - {Country}",
                dxCall, frequency, mode ?? "?", spotter, countryName ?? country);

            await SaveAndBroadcastSpotAsync(
                dxCall, spotter, frequency, mode, comment, timestamp, countryName ?? country, continent, null, grid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse CC spot: {Line}", match.Value);
        }
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
        ["YT"] = ("Serbia", "EU"), ["YU"] = ("Serbia", "EU"),
        ["B"] = ("China", "AS"), ["BD"] = ("China", "AS"), ["BH"] = ("China", "AS"),
        ["BU"] = ("Taiwan", "AS"),
    };

    private static (string? Country, string? Continent) GetCountryFromPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return (null, null);

        // Try exact match first
        if (PrefixToCountry.TryGetValue(prefix, out var result))
            return result;

        // Try progressively shorter prefixes (for compound prefixes like KH6, VP2M)
        for (int len = Math.Min(prefix.Length, 4); len >= 1; len--)
        {
            var shortPrefix = prefix.Substring(0, len);
            if (PrefixToCountry.TryGetValue(shortPrefix, out result))
                return result;
        }

        return (null, null);
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

            await SaveAndBroadcastSpotAsync(
                dxCall, spotter, frequency, mode, comment, timestamp, null, null, null, grid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse DX spot: {Line}", match.Value);
        }
    }

    private async Task SaveAndBroadcastSpotAsync(
        string dxCall, string spotter, double frequency, string? mode, string? comment,
        DateTime timestamp, string? country, string? continent, int? dxcc, string? grid = null)
    {
        var spot = new Spot
        {
            DxCall = dxCall,
            Spotter = spotter,
            Frequency = frequency,
            Mode = mode,
            Comment = comment,
            Source = "DX Cluster",
            Timestamp = timestamp,
            DxStation = new SpotStationInfo
            {
                Country = country,
                Continent = continent,
                Dxcc = dxcc,
                Grid = grid
            }
        };

        // Save to database using scoped service
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ISpotRepository>();
        var savedSpot = await repository.CreateAsync(spot);

        _logger.LogDebug("Spot: {DxCall} on {Freq} by {Spotter} - {Country}",
            dxCall, frequency, spotter, country ?? "Unknown");

        // Broadcast to clients
        var evt = new SpotReceivedEvent(
            savedSpot.Id,
            savedSpot.DxCall,
            savedSpot.Spotter,
            savedSpot.Frequency,
            savedSpot.Mode,
            savedSpot.Comment,
            savedSpot.Timestamp,
            savedSpot.Source ?? "DX Cluster",
            savedSpot.DxStation?.Country,
            savedSpot.DxStation?.Dxcc
        );

        await _hubContext.Clients.All.OnSpotReceived(evt);
    }

    private static string? ExtractMode(string comment)
    {
        var commentUpper = comment.ToUpper();

        // Check for common modes in comment
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

        return null; // Will be inferred from frequency if null
    }

    /// <summary>
    /// Infers mode from frequency when not explicitly specified in the comment.
    /// Phone (SSB) portions of amateur bands are typically in the upper segments.
    /// </summary>
    private static string InferModeFromFrequency(double frequencyKhz)
    {
        // 160m: 1800-2000 kHz - Phone above 1843
        if (frequencyKhz >= 1800 && frequencyKhz < 2000)
            return frequencyKhz >= 1843 ? "SSB" : "CW";

        // 80m: 3500-4000 kHz - Phone above 3600
        if (frequencyKhz >= 3500 && frequencyKhz < 4000)
            return frequencyKhz >= 3600 ? "SSB" : "CW";

        // 40m: 7000-7300 kHz - Phone above 7125 (varies by region)
        if (frequencyKhz >= 7000 && frequencyKhz < 7300)
            return frequencyKhz >= 7125 ? "SSB" : "CW";

        // 30m: 10100-10150 kHz - CW/Digital only, no phone
        if (frequencyKhz >= 10100 && frequencyKhz < 10150)
            return "CW";

        // 20m: 14000-14350 kHz - Phone above 14150
        if (frequencyKhz >= 14000 && frequencyKhz < 14350)
            return frequencyKhz >= 14150 ? "SSB" : "CW";

        // 17m: 18068-18168 kHz - Phone above 18110
        if (frequencyKhz >= 18068 && frequencyKhz < 18168)
            return frequencyKhz >= 18110 ? "SSB" : "CW";

        // 15m: 21000-21450 kHz - Phone above 21200
        if (frequencyKhz >= 21000 && frequencyKhz < 21450)
            return frequencyKhz >= 21200 ? "SSB" : "CW";

        // 12m: 24890-24990 kHz - Phone above 24930
        if (frequencyKhz >= 24890 && frequencyKhz < 24990)
            return frequencyKhz >= 24930 ? "SSB" : "CW";

        // 10m: 28000-29700 kHz - Phone above 28300
        if (frequencyKhz >= 28000 && frequencyKhz < 29700)
            return frequencyKhz >= 28300 ? "SSB" : "CW";

        // 6m: 50000-54000 kHz - Phone above 50100
        if (frequencyKhz >= 50000 && frequencyKhz < 54000)
            return frequencyKhz >= 50100 ? "SSB" : "CW";

        // Default to SSB for unknown frequencies
        return "SSB";
    }

    private static DateTime ParseSpotTime(string timeStr)
    {
        // Time is in HHMM format (UTC)
        if (timeStr.Length == 4 &&
            int.TryParse(timeStr.Substring(0, 2), out var hours) &&
            int.TryParse(timeStr.Substring(2, 2), out var minutes))
        {
            var now = DateTime.UtcNow;
            var spotTime = new DateTime(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Utc);

            // If the spot time is in the future, it's probably from yesterday
            if (spotTime > now.AddMinutes(5))
            {
                spotTime = spotTime.AddDays(-1);
            }

            return spotTime;
        }

        return DateTime.UtcNow;
    }
}
