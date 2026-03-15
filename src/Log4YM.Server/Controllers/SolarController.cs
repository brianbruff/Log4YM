using Microsoft.AspNetCore.Mvc;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/solar")]
public class SolarController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SolarController> _logger;

    public SolarController(IHttpClientFactory httpClientFactory, ILogger<SolarController> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    /// <summary>
    /// Proxy solar images with fallback sources
    /// Order: 1. NASA SDO, 2. LMSAL, 3. Helioviewer
    /// </summary>
    [HttpGet("image/{type}")]
    public async Task<IActionResult> GetSolarImage(string type)
    {
        // 1. Try NASA SDO Direct
        var nasaUrl = $"https://sdo.gsfc.nasa.gov/assets/img/latest/latest_512_{type}.jpg";
        var result = await TryFetchImage(nasaUrl, "NASA SDO");
        if (result != null) return result;

        // 2. Try LMSAL Sun Today Fallback
        var lmsalUrl = $"https://sdowww.lmsal.com/sdomedia/SunInTime/mostrecent/t{type}.jpg";
        result = await TryFetchImage(lmsalUrl, "LMSAL");
        if (result != null) return result;

        // 3. Try Helioviewer API Fallback
        // Mapping types to Helioviewer IDs if needed, but often the type (e.g. 0193) 
        // can be used to construct a request. 
        // For simplicity, we'll try a generic helioviewer request for that wavelength.
        var helioviewerUrl = $"https://api.helioviewer.org/v2/getScreenshot/?sourceId=11&jp2value={type}&scale=4.8&x0=0&y0=0&width=512&height=512";
        result = await TryFetchImage(helioviewerUrl, "Helioviewer");
        if (result != null) return result;

        return NotFound("Solar image not available from any source");
    }

    private async Task<IActionResult?> TryFetchImage(string url, string sourceName)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
                
                // Helioviewer returns images as 'image/png' usually, others as 'image/jpeg'
                _logger.LogInformation("Successfully fetched solar image from {Source}", sourceName);
                return File(content, contentType);
            }
            
            _logger.LogWarning("Failed to fetch solar image from {Source}: {StatusCode}", sourceName, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching solar image from {Source}", sourceName);
        }
        
        return null;
    }
}
