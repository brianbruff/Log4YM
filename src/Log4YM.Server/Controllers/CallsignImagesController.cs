using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/callsign-images")]
[Produces("application/json")]
public class CallsignImagesController : ControllerBase
{
    private readonly ICallsignImageRepository _imageRepository;
    private readonly IDbContext _dbContext;

    public CallsignImagesController(ICallsignImageRepository imageRepository, IDbContext dbContext)
    {
        _imageRepository = imageRepository;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetImages([FromQuery] int limit = 100)
    {
        if (!_dbContext.IsConnected)
            return Ok(Array.Empty<object>());

        var images = await _imageRepository.GetRecentAsync(Math.Min(limit, 500));

        return Ok(images.Select(i => new
        {
            i.Callsign,
            i.ImageUrl,
            i.Latitude,
            i.Longitude,
            i.Name,
            i.Country,
            i.Grid,
            i.SavedAt,
        }));
    }
}
