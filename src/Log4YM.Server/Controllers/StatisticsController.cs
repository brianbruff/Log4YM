using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Api;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StatisticsController : ControllerBase
{
    private readonly IAwardsService _awardsService;

    public StatisticsController(IAwardsService awardsService)
    {
        _awardsService = awardsService;
    }

    /// <summary>
    /// Get DXCC statistics with worked/confirmed status by band
    /// </summary>
    [HttpGet("dxcc")]
    [ProducesResponseType(typeof(DxccStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<DxccStatistics>> GetDxccStatistics(
        [FromQuery] string? band = null,
        [FromQuery] string? mode = null,
        [FromQuery] string? continent = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var filters = new StatisticsFilters(
            Band: band,
            Mode: mode,
            Continent: continent,
            Status: status,
            FromDate: fromDate,
            ToDate: toDate
        );

        var statistics = await _awardsService.GetDxccStatisticsAsync(filters);
        return Ok(statistics);
    }
}
