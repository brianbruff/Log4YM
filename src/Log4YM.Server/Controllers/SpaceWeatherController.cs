using Microsoft.AspNetCore.Mvc;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/spaceweather")]
[Produces("application/json")]
public class SpaceWeatherController : ControllerBase
{
    private readonly ISpaceWeatherService _spaceWeatherService;

    public SpaceWeatherController(ISpaceWeatherService spaceWeatherService)
    {
        _spaceWeatherService = spaceWeatherService;
    }

    /// <summary>
    /// Get current space weather indices (SFI, K-Index, SSN)
    /// Data sourced from NOAA Space Weather Prediction Center
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SpaceWeatherData), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpaceWeatherData>> GetSpaceWeather()
    {
        var data = await _spaceWeatherService.GetCurrentAsync();
        return Ok(data);
    }
}
