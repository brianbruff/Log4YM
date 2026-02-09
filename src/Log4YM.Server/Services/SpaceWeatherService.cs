using System.Text.Json;
using System.Text.Json.Serialization;

namespace Log4YM.Server.Services;

public record SpaceWeatherData(
    int SolarFluxIndex,    // SFI: 10.7cm solar flux (typical range 65-300, higher is better for HF)
    int KIndex,            // Planetary K-index (0-9, lower is better, >=4 indicates geomagnetic storm)
    int SunspotNumber,     // SSN: Daily sunspot number (0-300+, higher indicates more solar activity)
    DateTime Timestamp
);

public interface ISpaceWeatherService
{
    Task<SpaceWeatherData> GetCurrentAsync();
}

public class SpaceWeatherService : ISpaceWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SpaceWeatherService> _logger;

    private SpaceWeatherData? _cached;
    private DateTime _lastFetch = DateTime.MinValue;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);
    private readonly SemaphoreSlim _fetchLock = new(1, 1);

    public SpaceWeatherService(IHttpClientFactory httpClientFactory, ILogger<SpaceWeatherService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    public async Task<SpaceWeatherData> GetCurrentAsync()
    {
        if (_cached != null && DateTime.UtcNow - _lastFetch < _cacheExpiration)
            return _cached;

        await _fetchLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_cached != null && DateTime.UtcNow - _lastFetch < _cacheExpiration)
                return _cached;

            var data = await FetchSpaceWeatherData();
            _cached = data;
            _lastFetch = DateTime.UtcNow;
            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch space weather data");
            return _cached ?? new SpaceWeatherData(70, 2, 0, DateTime.UtcNow);
        }
        finally
        {
            _fetchLock.Release();
        }
    }

    private async Task<SpaceWeatherData> FetchSpaceWeatherData()
    {
        var url = "https://services.swpc.noaa.gov/json/solar-cycle/observed-solar-cycle-indices.json";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<List<NoaaSolarData>>(response);

            if (data != null && data.Count > 0)
            {
                var latest = data.OrderByDescending(d => d.TimeTag).First();
                var kIndex = await FetchKIndex();

                return new SpaceWeatherData(
                    (int)Math.Round(latest.F107),
                    kIndex,
                    (int)Math.Round(latest.Ssn),
                    DateTime.UtcNow
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch from NOAA, trying fallback");
        }

        return await FetchFromHamQSL();
    }

    private async Task<int> FetchKIndex()
    {
        try
        {
            var url = "https://services.swpc.noaa.gov/json/planetary_k_index_1m.json";
            var response = await _httpClient.GetStringAsync(url);
            var data = JsonSerializer.Deserialize<List<KIndexData>>(response);

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

        return 2;
    }

    private async Task<SpaceWeatherData> FetchFromHamQSL()
    {
        try
        {
            var url = "https://www.hamqsl.com/solarxml.php";
            var response = await _httpClient.GetStringAsync(url);

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

        return new SpaceWeatherData(70, 2, 0, DateTime.UtcNow);
    }
}

internal class NoaaSolarData
{
    [JsonPropertyName("time-tag")]
    public string TimeTag { get; set; } = string.Empty;

    [JsonPropertyName("ssn")]
    public double Ssn { get; set; }

    [JsonPropertyName("f10.7")]
    public double F107 { get; set; }
}

internal class KIndexData
{
    [JsonPropertyName("time_tag")]
    public string TimeTag { get; set; } = string.Empty;

    [JsonPropertyName("kp_index")]
    public double KpIndex { get; set; }
}
