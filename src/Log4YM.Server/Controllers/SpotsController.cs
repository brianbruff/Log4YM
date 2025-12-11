using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SpotsController : ControllerBase
{
    private readonly ISpotService _spotService;

    public SpotsController(ISpotService spotService)
    {
        _spotService = spotService;
    }

    /// <summary>
    /// Get recent DX spots
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Spot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Spot>>> GetSpots([FromQuery] int limit = 100)
    {
        var spots = await _spotService.GetRecentAsync(limit);
        return Ok(spots);
    }

    /// <summary>
    /// Get DX spots filtered by frequency band
    /// </summary>
    [HttpGet("band")]
    [ProducesResponseType(typeof(IEnumerable<Spot>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<Spot>>> GetSpotsByBand(
        [FromQuery] double minFreq,
        [FromQuery] double maxFreq,
        [FromQuery] int limit = 50)
    {
        var spots = await _spotService.GetByBandAsync(minFreq, maxFreq, limit);
        return Ok(spots);
    }

    /// <summary>
    /// Get the total count of spots
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<ActionResult<int>> GetCount()
    {
        var count = await _spotService.GetCountAsync();
        return Ok(count);
    }
}
