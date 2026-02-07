using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RbnController : ControllerBase
{
    private readonly ILogger<RbnController> _logger;
    private readonly IRbnService _rbnService;

    public RbnController(
        ILogger<RbnController> logger,
        IRbnService rbnService)
    {
        _logger = logger;
        _rbnService = rbnService;
    }

    [HttpGet("spots")]
    public IActionResult GetSpots([FromQuery] int minutes = 5)
    {
        try
        {
            var spots = _rbnService.GetRecentSpots(minutes);
            return Ok(new
            {
                count = spots.Count,
                spots = spots
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving RBN spots");
            return StatusCode(500, new { error = "Failed to retrieve RBN spots" });
        }
    }

    [HttpGet("location/{callsign}")]
    public async Task<IActionResult> GetSkimmerLocation(string callsign)
    {
        try
        {
            var (grid, lat, lon, country) = await _rbnService.LookupSkimmerLocationAsync(callsign);

            if (grid == null)
            {
                return NotFound(new { error = "Location not found" });
            }

            return Ok(new
            {
                callsign = callsign,
                grid = grid,
                lat = lat,
                lon = lon,
                country = country
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up skimmer location for {Callsign}", callsign);
            return StatusCode(500, new { error = "Failed to lookup skimmer location" });
        }
    }
}
