using Microsoft.AspNetCore.Mvc;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/propagation")]
[Produces("application/json")]
public class PropagationController : ControllerBase
{
    private readonly IPropagationService _propagationService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<PropagationController> _logger;

    public PropagationController(
        IPropagationService propagationService,
        ISettingsService settingsService,
        ILogger<PropagationController> logger)
    {
        _propagationService = propagationService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Get propagation prediction for a DE-DX path.
    /// DE station location comes from user settings.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PropagationPrediction), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PropagationPrediction>> GetPropagation(
        [FromQuery] double dxLat,
        [FromQuery] double dxLon)
    {
        if (dxLat < -90 || dxLat > 90 || dxLon < -180 || dxLon > 180)
            return BadRequest("Invalid DX coordinates");

        var settings = await _settingsService.GetSettingsAsync();
        var deLat = settings.Station.Latitude;
        var deLon = settings.Station.Longitude;

        if (deLat == null || deLon == null)
            return BadRequest("Station location not configured. Set latitude/longitude in Settings.");

        var prediction = await _propagationService.PredictAsync(
            deLat.Value, deLon.Value, dxLat, dxLon);

        return Ok(prediction);
    }

    /// <summary>
    /// Get generic band conditions (when no DX target is selected).
    /// Uses N0NBH data for a general overview.
    /// </summary>
    [HttpGet("conditions")]
    [ProducesResponseType(typeof(GenericBandConditions), StatusCodes.Status200OK)]
    public async Task<ActionResult<GenericBandConditions>> GetGenericConditions()
    {
        var conditions = await _propagationService.GetGenericConditionsAsync();
        return Ok(conditions);
    }
}
