using Microsoft.AspNetCore.Mvc;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/spaceweather")]
[Produces("application/json")]
public class SpaceWeatherController : ControllerBase
{
    private readonly ILogger<SpaceWeatherController> _logger;
    private readonly HttpClient _httpClient;

    // Cache for space weather data (refresh every 15 minutes)
    private static SpaceWeatherData? _cachedData;
    private static DateTime _lastFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    public SpaceWeatherController(ILogger<SpaceWeatherController> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Get current space weather indices (SFI, K-Index, SSN)
    /// Data sourced from NOAA Space Weather Prediction Center
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SpaceWeatherData), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpaceWeatherData>> GetSpaceWeather()
    {
        try
        {
            // Return cached data if still valid
            if (_cachedData != null && DateTime.UtcNow - _lastFetch < CacheExpiration)
            {
                return Ok(_cachedData);
            }

            // Fetch fresh data from NOAA
            var data = await FetchSpaceWeatherData();

            // Update cache
            _cachedData = data;
            _lastFetch = DateTime.UtcNow;

            return Ok(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch space weather data");

            // Return cached data if available, even if expired
            if (_cachedData != null)
            {
                return Ok(_cachedData);
            }

            // Return default values as fallback
            return Ok(new SpaceWeatherData(0, 0, 0, DateTime.UtcNow));
        }
    }

    private async Task<SpaceWeatherData> FetchSpaceWeatherData()
    {
        // NOAA Space Weather data from their JSON API
        // This endpoint provides current space weather conditions
        var url = "https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var data = System.Text.Json.JsonSerializer.Deserialize<List<NoaaSolarData>>(response);

            if (data != null && data.Count > 0)
            {
                // Get the most recent data point
                var latest = data.OrderByDescending(d => d.TimeTag).First();

                // K-index needs to come from a different endpoint
                var kIndex = await FetchKIndex();

                return new SpaceWeatherData(
                    (int)Math.Round(latest.F107),  // Solar Flux Index (10.7cm flux)
                    kIndex,
                    (int)Math.Round(latest.Ssn),   // Sunspot Number
                    DateTime.UtcNow
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch from NOAA, trying fallback");
        }

        // Fallback: try to get data from HamQSL
        return await FetchFromHamQSL();
    }

    private async Task<int> FetchKIndex()
    {
        try
        {
            var url = "https://services.swpc.noaa.gov/json/planetary_k_index_1m.json";
            var response = await _httpClient.GetStringAsync(url);
            var data = System.Text.Json.JsonSerializer.Deserialize<List<KIndexData>>(response);

            if (data != null && data.Count > 0)
            {
                var latest = data.OrderByDescending(d => d.TimeTag).First();
                return (int)Math.Round(latest.KpIndex);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch K-index");
        }

        return 2; // Default moderate value
    }

    private async Task<SpaceWeatherData> FetchFromHamQSL()
    {
        try
        {
            // HamQSL provides a simple text format with current indices
            var url = "https://www.hamqsl.com/solarxml.php";
            var response = await _httpClient.GetStringAsync(url);

            // Parse XML response
            var doc = System.Xml.Linq.XDocument.Parse(response);
            var solar = doc.Root?.Element("solardata") ?? doc.Root;

            if (solar != null)
            {
                var sfi = int.Parse(solar.Element("solarflux")?.Value?.Trim() ?? "70");
                var kIndex = int.Parse(solar.Element("kindex")?.Value?.Trim() ?? "2");
                var ssn = int.Parse(solar.Element("sunspots")?.Value?.Trim() ?? "0");

                return new SpaceWeatherData(sfi, kIndex, ssn, DateTime.UtcNow);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch from HamQSL fallback");
        }

        // Return reasonable defaults if all sources fail
        return new SpaceWeatherData(70, 2, 0, DateTime.UtcNow);
    }
}

public record SpaceWeatherData(
    int SolarFluxIndex,    // SFI: 10.7cm solar flux (typical range 65-300, higher is better for HF)
    int KIndex,            // Planetary K-index (0-9, lower is better, >=4 indicates geomagnetic storm)
    int SunspotNumber,     // SSN: Daily sunspot number (0-300+, higher indicates more solar activity)
    DateTime Timestamp
);

// NOAA JSON response structure
internal class NoaaSolarData
{
    [System.Text.Json.Serialization.JsonPropertyName("time-tag")]
    public string TimeTag { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("ssn")]
    public double Ssn { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("f10.7")]
    public double F107 { get; set; }
}

internal class KIndexData
{
    [System.Text.Json.Serialization.JsonPropertyName("time_tag")]
    public string TimeTag { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("kp_index")]
    public double KpIndex { get; set; }
}
