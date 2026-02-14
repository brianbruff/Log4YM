using Microsoft.AspNetCore.Mvc;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class SystemController : ControllerBase
{
    private readonly IDbContext _dbContext;
    private readonly IUserConfigService _userConfigService;

    public SystemController(IDbContext dbContext, IUserConfigService userConfigService)
    {
        _dbContext = dbContext;
        _userConfigService = userConfigService;
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> GetHealth()
    {
        var config = await _userConfigService.GetConfigAsync();
        var provider = config.Provider == DatabaseProvider.MongoDb ? "mongodb" : "local";
        return Ok(new HealthResponse("Healthy", DateTime.UtcNow, _dbContext.IsConnected, _dbContext.IsConnected, provider));
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
            new PluginInfo("map-globe", "Map/Globe", "1.0.0", true),
            new PluginInfo("antenna-genius", "Antenna Genius", "1.0.0", true)
        };

        return Ok(plugins);
    }
}

public record HealthResponse(string Status, DateTime Timestamp, bool MongoDbConnected, bool DatabaseConnected, string DatabaseProvider);
public record PluginInfo(string Id, string Name, string Version, bool Enabled);
