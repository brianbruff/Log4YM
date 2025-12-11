using Microsoft.AspNetCore.Mvc;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse("Healthy", DateTime.UtcNow));
    }

    /// <summary>
    /// Get available plugins
    /// </summary>
    [HttpGet("plugins")]
    [ProducesResponseType(typeof(IEnumerable<PluginInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<PluginInfo>> GetPlugins()
    {
        var plugins = new[]
        {
            new PluginInfo("cluster", "DX Cluster", "1.0.0", true),
            new PluginInfo("log-entry", "Log Entry", "1.0.0", true),
            new PluginInfo("log-history", "Log History", "1.0.0", true),
            new PluginInfo("map-globe", "Map/Globe", "1.0.0", true)
        };

        return Ok(plugins);
    }
}

public record HealthResponse(string Status, DateTime Timestamp);
public record PluginInfo(string Id, string Name, string Version, bool Enabled);
