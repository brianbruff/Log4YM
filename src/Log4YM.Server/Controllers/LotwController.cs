using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LotwController : ControllerBase
{
    private readonly ILotwService _lotwService;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<LotwController> _logger;

    public LotwController(
        ILotwService lotwService,
        ISettingsRepository settingsRepository,
        ILogger<LotwController> logger)
    {
        _lotwService = lotwService;
        _settingsRepository = settingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// Preview which QSOs match the filter, without uploading.
    /// </summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(LotwPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LotwPreviewResponse>> Preview([FromBody] LotwUploadFilter filter)
    {
        var result = await _lotwService.PreviewAsync(filter);
        return Ok(result);
    }

    /// <summary>
    /// Export matching QSOs to ADIF, hand to TQSL for signing and upload, and mark sent on success.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(LotwUploadResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<LotwUploadResult>> Upload([FromBody] LotwUploadFilter filter, CancellationToken cancellationToken)
    {
        var result = await _lotwService.UploadAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Verify a TQSL binary by running `--version`. Used by the Settings panel's Test button.
    /// </summary>
    [HttpPost("test-tqsl")]
    [ProducesResponseType(typeof(LotwTestTqslResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LotwTestTqslResponse>> TestTqsl([FromBody] LotwTestTqslRequest request, CancellationToken cancellationToken)
    {
        var result = await _lotwService.TestTqslAsync(request.Path, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Persist LOTW settings (TQSL path, station location, enabled).
    /// </summary>
    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettings([FromBody] LotwSettings request)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        settings.Lotw.TqslPath = request.TqslPath;
        settings.Lotw.StationCallsign = request.StationCallsign;
        settings.Lotw.Enabled = request.Enabled;

        await _settingsRepository.UpsertAsync(settings);

        return Ok(new { message = "LOTW settings saved" });
    }
}
