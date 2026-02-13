using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/callsign-images")]
[Produces("application/json")]
public class CallsignImagesController : ControllerBase
{
    private readonly MongoDbContext _db;

    public CallsignImagesController(MongoDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetImages([FromQuery] int limit = 100)
    {
        if (!_db.IsConnected)
            return Ok(Array.Empty<object>());

        var images = await _db.CallsignMapImages
            .Find(_ => true)
            .SortByDescending(i => i.SavedAt)
            .Limit(Math.Min(limit, 500))
            .ToListAsync();

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
