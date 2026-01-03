using Microsoft.AspNetCore.Mvc;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClusterController : ControllerBase
{
    private readonly IDxClusterService _clusterService;
    private readonly ILogger<ClusterController> _logger;

    public ClusterController(IDxClusterService clusterService, ILogger<ClusterController> logger)
    {
        _clusterService = clusterService;
        _logger = logger;
    }

    /// <summary>
    /// Get all cluster connection statuses
    /// </summary>
    [HttpGet("status")]
    public ActionResult<IReadOnlyDictionary<string, ClusterConnectionStatus>> GetStatus()
    {
        return Ok(_clusterService.GetStatuses());
    }

    /// <summary>
    /// Connect to a specific cluster by ID
    /// </summary>
    [HttpPost("connect/{id}")]
    public async Task<ActionResult> Connect(string id)
    {
        _logger.LogInformation("Connect request for cluster {ClusterId}", id);
        await _clusterService.ConnectClusterAsync(id);
        return Ok(new { message = "Connection initiated" });
    }

    /// <summary>
    /// Disconnect from a specific cluster by ID
    /// </summary>
    [HttpPost("disconnect/{id}")]
    public async Task<ActionResult> Disconnect(string id)
    {
        _logger.LogInformation("Disconnect request for cluster {ClusterId}", id);
        await _clusterService.DisconnectClusterAsync(id);
        return Ok(new { message = "Disconnected" });
    }

    /// <summary>
    /// Reconnect to a specific cluster by ID
    /// </summary>
    [HttpPost("reconnect/{id}")]
    public async Task<ActionResult> Reconnect(string id)
    {
        _logger.LogInformation("Reconnect request for cluster {ClusterId}", id);
        await _clusterService.DisconnectClusterAsync(id);
        await _clusterService.ConnectClusterAsync(id);
        return Ok(new { message = "Reconnection initiated" });
    }
}
