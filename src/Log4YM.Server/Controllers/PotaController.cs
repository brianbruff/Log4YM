using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PotaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PotaController> _logger;

    public PotaController(IHttpClientFactory httpClientFactory, ILogger<PotaController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get active POTA spots from POTA API
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
            var spots = JsonSerializer.Deserialize<List<PotaSpot>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Ok(spots ?? new List<PotaSpot>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch POTA spots");
            return Ok(new List<PotaSpot>());
        }
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
