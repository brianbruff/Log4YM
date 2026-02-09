using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingsService settingsService, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Get user settings
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> GetSettings()
    {
        var settings = await _settingsService.GetSettingsAsync();
        return Ok(settings);
    }

    /// <summary>
    /// Save user settings
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserSettings>> SaveSettings([FromBody] UserSettings settings)
    {
        if (settings == null)
        {
            return BadRequest("Settings cannot be null");
        }

        _logger.LogInformation("Saving settings for station: {Callsign}", settings.Station?.Callsign);

        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }

    /// <summary>
    /// Update station settings only
    /// </summary>
    [HttpPut("station")]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> UpdateStationSettings([FromBody] StationSettings stationSettings)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.Station = stationSettings;
        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }

    /// <summary>
    /// Update QRZ settings only
    /// </summary>
    [HttpPut("qrz")]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> UpdateQrzSettings([FromBody] QrzSettings qrzSettings)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.Qrz = qrzSettings;
        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }

    /// <summary>
    /// Update appearance settings only
    /// </summary>
    [HttpPut("appearance")]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> UpdateAppearanceSettings([FromBody] AppearanceSettings appearanceSettings)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.Appearance = appearanceSettings;
        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }

    /// <summary>
    /// Update map settings only
    /// </summary>
    [HttpPut("map")]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> UpdateMapSettings([FromBody] MapSettings mapSettings)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.Map = mapSettings;
        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }

    /// <summary>
    /// Update AI settings only
    /// </summary>
    [HttpPut("ai")]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> UpdateAiSettings([FromBody] AiSettings aiSettings)
    {
        var settings = await _settingsService.GetSettingsAsync();
        settings.Ai = aiSettings;
        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }

    /// <summary>
    /// Update layout JSON only
    /// </summary>
    [HttpPut("layout")]
    [ProducesResponseType(typeof(UserSettings), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserSettings>> UpdateLayout([FromBody] string layoutJson)
    {
        _logger.LogInformation("Updating layout configuration");
        var settings = await _settingsService.GetSettingsAsync();
        settings.LayoutJson = layoutJson;
        var saved = await _settingsService.SaveSettingsAsync(settings);
        return Ok(saved);
    }
}
