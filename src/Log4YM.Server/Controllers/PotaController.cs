using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PotaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PotaController> _logger;
    private readonly IMemoryCache _cache;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PotaController(IHttpClientFactory httpClientFactory, ILogger<PotaController> logger, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Get active POTA spots from POTA API, enriched with park coordinates
    /// </summary>
    [HttpGet("spots")]
    [ProducesResponseType(typeof(IEnumerable<PotaSpot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PotaSpot>>> GetSpots()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);

            var response = await httpClient.GetAsync("https://api.pota.app/spot/");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var spots = JsonSerializer.Deserialize<List<PotaSpot>>(content, JsonOptions)
                ?? new List<PotaSpot>();

            // Enrich spots with park coordinates from cache or park API
            var uniqueRefs = spots
                .Where(s => !string.IsNullOrEmpty(s.Reference) && s.Latitude is null)
                .Select(s => s.Reference)
                .Distinct()
                .ToList();

            var parkCoords = await GetParkCoordinates(httpClient, uniqueRefs);

            foreach (var spot in spots)
            {
                if (spot.Latitude is null && parkCoords.TryGetValue(spot.Reference, out var coords))
                {
                    spot.Latitude = coords.Lat;
                    spot.Longitude = coords.Lon;
                }
            }

            return Ok(spots);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch POTA spots");
            return Ok(new List<PotaSpot>());
        }
    }

    private async Task<Dictionary<string, (double Lat, double Lon)>> GetParkCoordinates(
        HttpClient httpClient, List<string> references)
    {
        var result = new Dictionary<string, (double Lat, double Lon)>();
        var toFetch = new List<string>();

        // Check cache first
        foreach (var reference in references)
        {
            var cacheKey = $"pota_park_{reference}";
            if (_cache.TryGetValue(cacheKey, out (double Lat, double Lon) cached))
            {
                result[reference] = cached;
            }
            else
            {
                toFetch.Add(reference);
            }
        }

        // Fetch missing park data in parallel (limited concurrency)
        if (toFetch.Count > 0)
        {
            using var semaphore = new SemaphoreSlim(10);
            var tasks = toFetch.Select(async reference =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var parkResponse = await httpClient.GetAsync($"https://api.pota.app/park/{reference}");
                    if (parkResponse.IsSuccessStatusCode)
                    {
                        var parkJson = await parkResponse.Content.ReadAsStringAsync();
                        var park = JsonSerializer.Deserialize<PotaPark>(parkJson, JsonOptions);
                        if (park?.Latitude is not null && park.Longitude is not null)
                        {
                            var coords = (park.Latitude.Value, park.Longitude.Value);
                            _cache.Set($"pota_park_{reference}", coords, TimeSpan.FromHours(24));
                            return (reference, coords: ((double Lat, double Lon)?)coords);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch park {Reference}", reference);
                }
                finally
                {
                    semaphore.Release();
                }
                return (reference, coords: ((double Lat, double Lon)?)null);
            });

            foreach (var (reference, coords) in await Task.WhenAll(tasks))
            {
                if (coords.HasValue)
                    result[reference] = coords.Value;
            }
        }

        return result;
    }
}

public class PotaSpot
{
    public int SpotId { get; set; }
    public string Activator { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public string ParkName { get; set; } = string.Empty;
    public string SpotTime { get; set; } = string.Empty;
    public string Spotter { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool? Invalid { get; set; }
    public string? Name { get; set; }
    public string? LocationDesc { get; set; }
    public string? Grid4 { get; set; }
    public string? Grid6 { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class PotaPark
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
