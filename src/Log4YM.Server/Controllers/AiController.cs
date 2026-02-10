using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Api;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AiController : ControllerBase
{
    private readonly IAiService _aiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IAiService aiService, ILogger<AiController> logger)
    {
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Generate talk points for a callsign based on QSO history and QRZ profile
    /// </summary>
    [HttpPost("talk-points")]
    [ProducesResponseType(typeof(GenerateTalkPointsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<GenerateTalkPointsResponse>> GenerateTalkPoints(
        [FromBody] GenerateTalkPointsRequest request)
    {
        try
        {
            var response = await _aiService.GenerateTalkPointsAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid AI configuration");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating talk points for {Callsign}", request.Callsign);
            return StatusCode(500, new { error = "Failed to generate talk points" });
        }
    }

    /// <summary>
    /// Chat with AI about a callsign
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        try
        {
            var response = await _aiService.ChatAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid AI configuration");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat for {Callsign}", request.Callsign);
            return StatusCode(500, new { error = "Failed to process chat request" });
        }
    }

    /// <summary>
    /// Test an AI API key
    /// </summary>
    [HttpPost("test-key")]
    [ProducesResponseType(typeof(TestApiKeyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestApiKeyResponse>> TestApiKey([FromBody] TestApiKeyRequest request)
    {
        var response = await _aiService.TestApiKeyAsync(request);
        return Ok(response);
    }
}
