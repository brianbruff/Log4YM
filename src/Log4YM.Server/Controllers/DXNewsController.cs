using Microsoft.AspNetCore.Mvc;
using Log4YM.Server.Services;
using Log4YM.Contracts.Models;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DXNewsController : ControllerBase
{
    private readonly IDXNewsService _newsService;
    private readonly ILogger<DXNewsController> _logger;

    public DXNewsController(IDXNewsService newsService, ILogger<DXNewsController> logger)
    {
        _newsService = newsService;
        _logger = logger;
    }

    /// <summary>
    /// Get DX news headlines from dxnews.com
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DXNewsItem>>> GetNews()
    {
        try
        {
            var news = await _newsService.GetNewsAsync();
            return Ok(news);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching DX news");
            return StatusCode(500, new { error = "Failed to fetch DX news" });
        }
    }
}
