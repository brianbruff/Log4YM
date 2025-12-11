using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Api;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class QsosController : ControllerBase
{
    private readonly IQsoService _qsoService;

    public QsosController(IQsoService qsoService)
    {
        _qsoService = qsoService;
    }

    /// <summary>
    /// Get QSOs with optional filtering and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedQsoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedQsoResponse>> GetQsos(
        [FromQuery] string? callsign = null,
        [FromQuery] string? name = null,
        [FromQuery] string? band = null,
        [FromQuery] string? mode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;
        var request = new QsoSearchRequest(
            Callsign: callsign,
            Name: name,
            Band: band,
            Mode: mode,
            FromDate: fromDate,
            ToDate: toDate,
            Limit: pageSize,
            Skip: skip
        );

        var result = await _qsoService.GetQsosAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific QSO by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(QsoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QsoResponse>> GetQsoById(string id)
    {
        var qso = await _qsoService.GetByIdAsync(id);
        if (qso is null)
            return NotFound();

        return Ok(qso);
    }

    /// <summary>
    /// Create a new QSO
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(QsoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QsoResponse>> CreateQso([FromBody] CreateQsoRequest request)
    {
        var created = await _qsoService.CreateAsync(request);
        return CreatedAtAction(nameof(GetQsoById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update an existing QSO
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(QsoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QsoResponse>> UpdateQso(string id, [FromBody] UpdateQsoRequest request)
    {
        var updated = await _qsoService.UpdateAsync(id, request);
        if (updated is null)
            return NotFound();

        return Ok(updated);
    }

    /// <summary>
    /// Delete a QSO
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteQso(string id)
    {
        var deleted = await _qsoService.DeleteAsync(id);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    /// <summary>
    /// Get QSO statistics
    /// </summary>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(QsoStatistics), StatusCodes.Status200OK)]
    public async Task<ActionResult<QsoStatistics>> GetStatistics()
    {
        var stats = await _qsoService.GetStatisticsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Search QSOs with advanced criteria
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(PaginatedQsoResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedQsoResponse>> SearchQsos([FromBody] QsoSearchRequest request)
    {
        var result = await _qsoService.GetQsosAsync(request);
        return Ok(result);
    }
}
