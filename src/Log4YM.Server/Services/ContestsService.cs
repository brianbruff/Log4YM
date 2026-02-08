using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Log4YM.Contracts.Models;

namespace Log4YM.Server.Services;

public class ContestsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ContestsService> _logger;
    private List<Contest> _cachedContests = new();
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    // WA7BNM Contest Calendar RSS feed (same source as OpenHamClock)
    private const string WA7BNM_RSS_URL = "https://www.contestcalendar.com/calendar.rss";

    private static readonly Dictionary<string, int> MonthMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Jan"] = 1, ["Feb"] = 2, ["Mar"] = 3, ["Apr"] = 4,
        ["May"] = 5, ["Jun"] = 6, ["Jul"] = 7, ["Aug"] = 8,
        ["Sep"] = 9, ["Oct"] = 10, ["Nov"] = 11, ["Dec"] = 12,
    };

    public ContestsService(IHttpClientFactory httpClientFactory, ILogger<ContestsService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Log4YM/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/rss+xml, application/xml, text/xml");
        _logger = logger;
    }

    public async Task<List<Contest>> GetUpcomingContestsAsync(int days = 7)
    {
        if (DateTime.UtcNow - _lastFetch < _cacheExpiration && _cachedContests.Any())
        {
            return FilterContests(_cachedContests, days);
        }

        try
        {
            var response = await _httpClient.GetStringAsync(WA7BNM_RSS_URL);
            var contests = ParseRssFeed(response);
            _logger.LogInformation("Fetched {Count} contests from WA7BNM RSS feed", contests.Count);

            _cachedContests = contests;
            _lastFetch = DateTime.UtcNow;

            return FilterContests(contests, days);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch contests from WA7BNM");
            return _cachedContests.Any() ? FilterContests(_cachedContests, days) : new List<Contest>();
        }
    }

    public List<Contest> GetLiveContests()
    {
        var now = DateTime.UtcNow;
        return _cachedContests
            .Where(c => c.StartTime <= now && c.EndTime >= now)
            .OrderBy(c => c.EndTime)
            .ToList();
    }

    private List<Contest> FilterContests(List<Contest> contests, int days)
    {
        var now = DateTime.UtcNow;
        var endDate = now.AddDays(days);

        return contests
            .Where(c => c.EndTime >= now && c.StartTime <= endDate)
            .OrderBy(c => c.StartTime)
            .Select(c => {
                c.IsLive = c.StartTime <= now && c.EndTime >= now;
                c.IsStartingSoon = !c.IsLive && c.StartTime <= now.AddHours(24);
                if (c.IsLive)
                {
                    var timeRemaining = c.EndTime - now;
                    c.TimeRemaining = FormatTimeSpan(timeRemaining);
                }
                return c;
            })
            .ToList();
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h";
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        return $"{timeSpan.Minutes}m";
    }

    private List<Contest> ParseRssFeed(string rssXml)
    {
        var contests = new List<Contest>();

        try
        {
            var doc = XDocument.Parse(rssXml);
            var items = doc.Descendants("item");

            foreach (var item in items)
            {
                try
                {
                    var title = item.Element("title")?.Value?.Trim();
                    var link = item.Element("link")?.Value?.Trim();
                    var description = item.Element("description")?.Value?.Trim();

                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description))
                        continue;

                    var (start, end) = ParseContestDateTime(description);
                    if (start == default || end == default)
                    {
                        _logger.LogDebug("Skipping contest '{Name}': could not parse dates from '{Desc}'", title, description);
                        continue;
                    }

                    contests.Add(new Contest
                    {
                        Name = title,
                        Mode = InferMode(title),
                        StartTime = start,
                        EndTime = end,
                        Url = link ?? "",
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse RSS contest item");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse RSS XML");
        }

        return contests;
    }

    /// <summary>
    /// Parse WA7BNM date formats (same patterns as OpenHamClock):
    /// Pattern 1: "0000Z, Feb 7 to 2400Z, Feb 8"  (multi-day)
    /// Pattern 2: "1400Z-2400Z, Feb 7"  (same-day)
    /// Pattern 3: "1600Z, Feb 7 to 0359Z, Feb 8 and 1600Z-2359Z, Feb 8"  (multi-session, take first)
    /// </summary>
    private (DateTime start, DateTime end) ParseContestDateTime(string description)
    {
        // Handle "and" multi-session: take first session only
        var andIdx = description.IndexOf(" and ", StringComparison.Ordinal);
        if (andIdx > 0)
            description = description.Substring(0, andIdx).Trim();

        var now = DateTime.UtcNow;

        // Pattern 1: "HHmmZ, Mon DD to HHmmZ, Mon DD"
        var multiDay = Regex.Match(description,
            @"(\d{4})Z,\s*(\w+)\s+(\d+)\s+to\s+(\d{4})Z,\s*(\w+)\s+(\d+)",
            RegexOptions.IgnoreCase);
        if (multiDay.Success)
        {
            var startTime = multiDay.Groups[1].Value;
            var startMonth = multiDay.Groups[2].Value;
            var startDay = int.Parse(multiDay.Groups[3].Value);
            var endTime = multiDay.Groups[4].Value;
            var endMonth = multiDay.Groups[5].Value;
            var endDay = int.Parse(multiDay.Groups[6].Value);

            var start = BuildDateTime(startMonth, startDay, startTime, now.Year);
            var end = BuildDateTime(endMonth, endDay, endTime, now.Year);

            // Handle year rollover (Dec -> Jan)
            if (end < start)
                end = end.AddYears(1);

            return (start, end);
        }

        // Pattern 2: "HHmmZ-HHmmZ, Mon DD"
        var sameDay = Regex.Match(description,
            @"(\d{4})Z-(\d{4})Z,\s*(\w+)\s+(\d+)",
            RegexOptions.IgnoreCase);
        if (sameDay.Success)
        {
            var startTime = sameDay.Groups[1].Value;
            var endTime = sameDay.Groups[2].Value;
            var month = sameDay.Groups[3].Value;
            var day = int.Parse(sameDay.Groups[4].Value);

            var start = BuildDateTime(month, day, startTime, now.Year);
            var end = BuildDateTime(month, day, endTime, now.Year);

            // Overnight contest (end <= start means next day)
            if (end <= start)
                end = end.AddDays(1);

            return (start, end);
        }

        return (default, default);
    }

    private DateTime BuildDateTime(string monthStr, int day, string timeStr, int year)
    {
        if (!MonthMap.TryGetValue(monthStr, out var month))
            return default;

        var hour = int.Parse(timeStr.Substring(0, 2));
        var minute = int.Parse(timeStr.Substring(2, 2));

        // "2400Z" means midnight of the next day
        if (hour == 24)
        {
            hour = 0;
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc).AddDays(1);
        }

        return new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);
    }

    private string InferMode(string contestName)
    {
        var nameLower = contestName.ToLower();

        if (nameLower.Contains("cw") || nameLower.Contains("morse"))
            return "CW";
        if (nameLower.Contains("ssb") || nameLower.Contains("phone") || nameLower.Contains("sideband"))
            return "SSB";
        if (nameLower.Contains("rtty"))
            return "RTTY";
        if (nameLower.Contains("ft4") || nameLower.Contains("ft8") || nameLower.Contains("digi"))
            return "Digital";
        if (nameLower.Contains("vhf") || nameLower.Contains("uhf"))
            return "VHF";

        return "Mixed";
    }
}
