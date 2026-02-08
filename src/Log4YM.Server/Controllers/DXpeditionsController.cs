using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/dxpeditions")]
[Produces("application/json")]
public class DXpeditionsController : ControllerBase
{
    private readonly ILogger<DXpeditionsController> _logger;
    private readonly HttpClient _httpClient;

    // Cache for DXpedition data (refresh every 30 minutes)
    private static DXpeditionData? _cachedData;
    private static DateTime _lastFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public DXpeditionsController(ILogger<DXpeditionsController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Get active and upcoming DXpeditions from NG3K
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DXpeditionData), StatusCodes.Status200OK)]
    public async Task<ActionResult<DXpeditionData>> GetDXpeditions()
    {
        try
        {
            // Return cached data if still valid
            if (_cachedData != null && DateTime.UtcNow - _lastFetch < CacheExpiration)
            {
                return Ok(_cachedData);
            }

            // Fetch fresh data from NG3K
            var data = await FetchDXpeditionData();

            // Update cache
            _cachedData = data;
            _lastFetch = DateTime.UtcNow;

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch DXpedition data");

            // Return cached data if available, even if expired
            if (_cachedData != null)
            {
                return Ok(_cachedData);
            }

            // Return empty data as fallback
            return Ok(new DXpeditionData([], 0, 0, "NG3K ADXO", DateTime.UtcNow));
        }
    }

    private async Task<DXpeditionData> FetchDXpeditionData()
    {
        // Fetch NG3K ADXO plain text version
        var response = await _httpClient.GetAsync("https://www.ng3k.com/Misc/adxoplain.html");
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync();

        // Strip HTML and normalize
        var text = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        var dxpeditions = new List<DXpedition>();

        // Parse entries - each starts with date pattern like "Jan 1-Feb 16, 2026 DXCC:"
        var entryPattern = new Regex(
            @"((?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2}[^D]*?DXCC:[^·]+?)(?=(?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+\d{1,2}|$)",
            RegexOptions.IgnoreCase
        );

        var entries = entryPattern.Matches(text);

        foreach (Match entry in entries)
        {
            var entryText = entry.Value.Trim();
            if (string.IsNullOrWhiteSpace(entryText)) continue;

            // Skip header/footer content
            if (entryText.Contains("Last updated") || entryText.Contains("Copyright") ||
                entryText.Contains("Expired Announcements") || entryText.Contains("Table Version") ||
                entryText.Contains("ADXB=") || entryText.Contains("OPDX="))
                continue;

            // Parse DXCC entity
            var dxccMatch = Regex.Match(entryText, @"DXCC:\s*([^C\n]+?)(?=Callsign:|QSL:|Source:|Info:|$)", RegexOptions.IgnoreCase);
            var callMatch = Regex.Match(entryText, @"Callsign:\s*([A-Z0-9\/]+)", RegexOptions.IgnoreCase);

            string? callsign = null;
            string? entity = null;

            if (callMatch.Success && dxccMatch.Success)
            {
                callsign = callMatch.Groups[1].Value.Trim().ToUpperInvariant();
                entity = dxccMatch.Groups[1].Value.Trim();
            }

            // Fallback: look for callsign patterns
            if (callsign == null)
            {
                var directCallMatch = Regex.Match(entryText, @"\b([A-Z]{1,2}\d[A-Z0-9]*[A-Z](?:/[A-Z0-9]+)?)\b");
                if (directCallMatch.Success)
                {
                    callsign = directCallMatch.Groups[1].Value;
                }
            }

            if (callsign == null || callsign.Length < 3) continue;

            // Skip obviously wrong matches
            if (Regex.IsMatch(callsign, @"^(DXCC|QSL|INFO|SOURCE|THE|AND|FOR)$", RegexOptions.IgnoreCase))
                continue;

            // Extract date
            var dateMatch = Regex.Match(entryText, @"^([A-Za-z]{3}\s+\d{1,2}[^D]*?)(?=DXCC:)", RegexOptions.IgnoreCase);
            var dateStr = dateMatch.Success ? dateMatch.Groups[1].Value.Trim() : "";

            // Extract QSL and info
            var qslMatch = Regex.Match(entryText, @"QSL:\s*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            var infoMatch = Regex.Match(entryText, @"Info:\s*(.+)", RegexOptions.IgnoreCase);
            var qsl = qslMatch.Success ? qslMatch.Groups[1].Value.Trim() : "";
            var info = infoMatch.Success ? infoMatch.Groups[1].Value.Trim() : "";

            // Parse dates
            DateTime? startDate = null;
            DateTime? endDate = null;
            var isActive = false;
            var isUpcoming = false;

            if (!string.IsNullOrWhiteSpace(dateStr))
            {
                var (start, end) = ParseDateRange(dateStr);
                startDate = start;
                endDate = end;

                if (startDate.HasValue && endDate.HasValue)
                {
                    var today = DateTime.UtcNow.Date;
                    isActive = startDate.Value <= today && endDate.Value >= today;
                    isUpcoming = startDate.Value > today;
                }
            }

            dxpeditions.Add(new DXpedition(
                callsign,
                entity ?? "Unknown",
                dateStr,
                qsl,
                info.Length > 100 ? info.Substring(0, 100) : info,
                "",
                "",
                startDate,
                endDate,
                isActive,
                isUpcoming
            ));
        }

        // Remove duplicates
        var uniqueDxpeditions = dxpeditions
            .GroupBy(d => d.Callsign)
            .Select(g => g.First())
            .ToList();

        // Sort: active first, then upcoming by start date
        uniqueDxpeditions.Sort((a, b) =>
        {
            if (a.IsActive && !b.IsActive) return -1;
            if (!a.IsActive && b.IsActive) return 1;
            if (a.IsUpcoming && !b.IsUpcoming) return -1;
            if (!a.IsUpcoming && b.IsUpcoming) return 1;
            if (a.StartDate.HasValue && b.StartDate.HasValue)
                return a.StartDate.Value.CompareTo(b.StartDate.Value);
            return 0;
        });

        var active = uniqueDxpeditions.Count(d => d.IsActive);
        var upcoming = uniqueDxpeditions.Count(d => d.IsUpcoming);

        return new DXpeditionData(
            uniqueDxpeditions.Take(50).ToList(),
            active,
            upcoming,
            "NG3K ADXO",
            DateTime.UtcNow
        );
    }

    private static (DateTime?, DateTime?) ParseDateRange(string dateStr)
    {
        var monthNames = new[] { "jan", "feb", "mar", "apr", "may", "jun", "jul", "aug", "sep", "oct", "nov", "dec" };
        var pattern = new Regex(@"([A-Za-z]{3})\s+(\d{1,2})(?:,?\s*(\d{4}))?(?:\s*[-–]\s*([A-Za-z]{3})?\s*(\d{1,2})(?:,?\s*(\d{4}))?)?", RegexOptions.IgnoreCase);
        var match = pattern.Match(dateStr);

        if (!match.Success) return (null, null);

        var currentYear = DateTime.UtcNow.Year;

        var startMonthStr = match.Groups[1].Value.ToLowerInvariant();
        var startMonth = Array.IndexOf(monthNames, startMonthStr);
        var startDay = int.Parse(match.Groups[2].Value);
        var startYear = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : currentYear;

        if (startMonth < 0) return (null, null);

        var endMonthStr = match.Groups[4].Success ? match.Groups[4].Value.ToLowerInvariant() : startMonthStr;
        var endMonth = Array.IndexOf(monthNames, endMonthStr);
        if (endMonth < 0) endMonth = startMonth;

        var endDay = match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : startDay + 14;
        var endYear = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : startYear;

        var startDate = new DateTime(startYear, startMonth + 1, startDay);
        var endDate = new DateTime(endYear, endMonth + 1, Math.Min(endDay, DateTime.DaysInMonth(endYear, endMonth + 1)));

        // If end date is before start and no explicit year, assume next year
        if (endDate < startDate && !match.Groups[6].Success)
        {
            endDate = endDate.AddYears(1);
        }

        return (startDate, endDate);
    }
}

public record DXpedition(
    string Callsign,
    string Entity,
    string Dates,
    string Qsl,
    string Info,
    string Bands,
    string Modes,
    DateTime? StartDate,
    DateTime? EndDate,
    bool IsActive,
    bool IsUpcoming
);

public record DXpeditionData(
    List<DXpedition> Dxpeditions,
    int Active,
    int Upcoming,
    string Source,
    DateTime Timestamp
);
