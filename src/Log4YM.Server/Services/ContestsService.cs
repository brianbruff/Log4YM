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
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(6);

    // WA7BNM Contest Calendar RSS feed
    private const string WA7BNM_RSS_URL = "https://www.contestcalendar.com/weeklycont.ics";

    public ContestsService(IHttpClientFactory httpClientFactory, ILogger<ContestsService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public async Task<List<Contest>> GetUpcomingContestsAsync(int days = 7)
    {
        // Check cache
        if (DateTime.UtcNow - _lastFetch < _cacheExpiration && _cachedContests.Any())
        {
            return FilterContests(_cachedContests, days);
        }

        try
        {
            // Fetch from WA7BNM Contest Calendar
            var response = await _httpClient.GetStringAsync(WA7BNM_RSS_URL);
            var contests = ParseICalendar(response);

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
                c.TimeRemaining = c.IsLive ? c.EndTime - now : null;
                return c;
            })
            .ToList();
    }

    private List<Contest> ParseICalendar(string icalData)
    {
        var contests = new List<Contest>();

        // Split by VEVENT blocks
        var events = Regex.Split(icalData, @"BEGIN:VEVENT").Skip(1);

        foreach (var eventBlock in events)
        {
            try
            {
                var contest = ParseEvent(eventBlock);
                if (contest != null)
                {
                    contests.Add(contest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse contest event");
            }
        }

        return contests;
    }

    private Contest? ParseEvent(string eventBlock)
    {
        var lines = eventBlock.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var contest = new Contest();

        foreach (var line in lines)
        {
            if (line.StartsWith("SUMMARY:"))
            {
                contest.Name = line.Substring(8).Trim();
            }
            else if (line.StartsWith("DTSTART:"))
            {
                contest.StartTime = ParseICalDate(line.Substring(8));
            }
            else if (line.StartsWith("DTEND:"))
            {
                contest.EndTime = ParseICalDate(line.Substring(6));
            }
            else if (line.StartsWith("URL:"))
            {
                contest.Url = line.Substring(4).Trim();
            }
        }

        // Infer mode from contest name
        contest.Mode = InferMode(contest.Name);

        // Only return if we have required fields
        if (string.IsNullOrEmpty(contest.Name) ||
            contest.StartTime == default ||
            contest.EndTime == default)
        {
            return null;
        }

        return contest;
    }

    private DateTime ParseICalDate(string dateStr)
    {
        // iCal format: YYYYMMDDTHHMMSSZ or YYYYMMDD
        dateStr = dateStr.Trim().Replace(":", "").Replace("-", "");

        if (dateStr.Length >= 15 && dateStr.Contains('T'))
        {
            // Format: 20260207T120000Z
            var year = int.Parse(dateStr.Substring(0, 4));
            var month = int.Parse(dateStr.Substring(4, 2));
            var day = int.Parse(dateStr.Substring(6, 2));
            var hour = int.Parse(dateStr.Substring(9, 2));
            var minute = int.Parse(dateStr.Substring(11, 2));
            var second = int.Parse(dateStr.Substring(13, 2));

            return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
        }
        else if (dateStr.Length >= 8)
        {
            // Format: 20260207
            var year = int.Parse(dateStr.Substring(0, 4));
            var month = int.Parse(dateStr.Substring(4, 2));
            var day = int.Parse(dateStr.Substring(6, 2));

            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }

        return DateTime.MinValue;
    }

    private string InferMode(string contestName)
    {
        var nameLower = contestName.ToLower();

        if (nameLower.Contains("cw"))
            return "CW";
        if (nameLower.Contains("ssb") || nameLower.Contains("phone"))
            return "SSB";
        if (nameLower.Contains("rtty") || nameLower.Contains("digi"))
            return "RTTY";
        if (nameLower.Contains("ft8") || nameLower.Contains("ft4"))
            return "FT8";

        // Default to mixed if mode unclear
        return "Mixed";
    }
}
