using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/satellites")]
[Produces("application/json")]
public class SatellitesController : ControllerBase
{
    private readonly ILogger<SatellitesController> _logger;
    private readonly HttpClient _httpClient;

    // Cache for TLE data (refresh every 6 hours)
    private static Dictionary<string, TLEData>? _cachedTLEData;
    private static DateTime _lastFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(6);

    // Popular amateur radio satellites with their NORAD catalog numbers
    private static readonly Dictionary<string, int> AmateurSatellites = new()
    {
        { "ISS", 25544 },
        { "AO-91", 43017 },
        { "AO-92", 43137 },
        { "SO-50", 27607 },
        { "PO-101", 43678 },
        { "RS-44", 44909 },
        { "IO-117", 52934 },
        { "TEVEL-1", 50988 },
        { "TEVEL-2", 50989 },
        { "TEVEL-3", 50990 },
        { "TEVEL-4", 50991 },
        { "TEVEL-5", 50992 },
        { "TEVEL-6", 50993 },
        { "TEVEL-7", 50994 },
        { "TEVEL-8", 50995 },
    };

    public SatellitesController(ILogger<SatellitesController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Get TLE (Two-Line Element) data for specified satellites
    /// Data sourced from Celestrak
    /// </summary>
    [HttpPost("tle")]
    [ProducesResponseType(typeof(Dictionary<string, TLEData>), StatusCodes.Status200OK)]
    public async Task<ActionResult<Dictionary<string, TLEData>>> GetTLEData([FromBody] TLERequest request)
    {
        try
        {
            // Return cached data if still valid
            if (_cachedTLEData != null && DateTime.UtcNow - _lastFetch < CacheExpiration)
            {
                var cachedResult = FilterTLEData(_cachedTLEData, request.Satellites);
                return Ok(cachedResult);
            }

            // Fetch fresh TLE data from Celestrak
            var tleData = await FetchTLEDataFromCelestrak();

            // Update cache
            _cachedTLEData = tleData;
            _lastFetch = DateTime.UtcNow;

            var result = FilterTLEData(tleData, request.Satellites);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch TLE data");
            return StatusCode(500, new { error = "Failed to fetch TLE data" });
        }
    }

    /// <summary>
    /// Get list of available amateur radio satellites
    /// </summary>
    [HttpGet("list")]
    [ProducesResponseType(typeof(List<SatelliteInfo>), StatusCodes.Status200OK)]
    public ActionResult<List<SatelliteInfo>> GetAvailableSatellites()
    {
        var satellites = AmateurSatellites.Select(kvp => new SatelliteInfo
        {
            Name = kvp.Key,
            NoradId = kvp.Value
        }).ToList();

        return Ok(satellites);
    }

    private async Task<Dictionary<string, TLEData>> FetchTLEDataFromCelestrak()
    {
        var tleData = new Dictionary<string, TLEData>();

        // Fetch amateur radio satellites TLE data from Celestrak
        var url = "https://celestrak.org/NORAD/elements/gp.php?GROUP=amateur&FORMAT=tle";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Parse TLE format (3-line groups: name, line1, line2)
            for (int i = 0; i < lines.Length - 2; i += 3)
            {
                var name = lines[i].Trim();
                var line1 = lines[i + 1].Trim();
                var line2 = lines[i + 2].Trim();

                // Match name with our known satellites
                foreach (var sat in AmateurSatellites)
                {
                    if (name.Contains(sat.Key, StringComparison.OrdinalIgnoreCase) ||
                        name.Contains(sat.Value.ToString()))
                    {
                        tleData[sat.Key] = new TLEData
                        {
                            Name = sat.Key,
                            Line1 = line1,
                            Line2 = line2
                        };
                        break;
                    }
                }
            }

            // If we couldn't find some satellites in amateur group, try other sources
            if (tleData.Count < AmateurSatellites.Count)
            {
                await FetchMissingSatellites(tleData);
            }

            return tleData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch TLE data from Celestrak");
            throw;
        }
    }

    private async Task FetchMissingSatellites(Dictionary<string, TLEData> tleData)
    {
        // Try to fetch specific satellites by NORAD ID
        var missingSatellites = AmateurSatellites.Where(kvp => !tleData.ContainsKey(kvp.Key)).ToList();

        foreach (var sat in missingSatellites)
        {
            try
            {
                var url = $"https://celestrak.org/NORAD/elements/gp.php?CATNR={sat.Value}&FORMAT=tle";
                var response = await _httpClient.GetStringAsync(url);
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length >= 3)
                {
                    tleData[sat.Key] = new TLEData
                    {
                        Name = sat.Key,
                        Line1 = lines[1].Trim(),
                        Line2 = lines[2].Trim()
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch TLE for {Satellite}", sat.Key);
            }
        }
    }

    private Dictionary<string, TLEData> FilterTLEData(Dictionary<string, TLEData> allData, List<string> requestedSatellites)
    {
        if (requestedSatellites == null || requestedSatellites.Count == 0)
        {
            return allData;
        }

        return allData
            .Where(kvp => requestedSatellites.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}

public class TLERequest
{
    public List<string> Satellites { get; set; } = new();
}

public class TLEData
{
    public string Name { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string Line2 { get; set; } = string.Empty;
}

public class SatelliteInfo
{
    public string Name { get; set; } = string.Empty;
    public int NoradId { get; set; }
}
