using MongoDB.Driver;
using Log4YM.Contracts.Models;

namespace Log4YM.Server.Core.Database;

public interface ICallsignImageRepository
{
    Task UpsertAsync(CallsignMapImage image);
    Task<List<CallsignMapImage>> GetRecentAsync(int limit);
}

public class CallsignImageRepository : ICallsignImageRepository
{
    private readonly MongoDbContext _context;

    public CallsignImageRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task UpsertAsync(CallsignMapImage image)
    {
        var filter = Builders<CallsignMapImage>.Filter.Eq(i => i.Callsign, image.Callsign);
        var update = Builders<CallsignMapImage>.Update
            .Set(i => i.Callsign, image.Callsign)
            .Set(i => i.ImageUrl, image.ImageUrl)
            .Set(i => i.Latitude, image.Latitude)
            .Set(i => i.Longitude, image.Longitude)
            .Set(i => i.Name, image.Name)
            .Set(i => i.Country, image.Country)
            .Set(i => i.Grid, image.Grid)
            .Set(i => i.SavedAt, DateTime.UtcNow);

        await _context.CallsignMapImages.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    public async Task<List<CallsignMapImage>> GetRecentAsync(int limit)
    {
        return await _context.CallsignMapImages
            .Find(_ => true)
            .SortByDescending(i => i.SavedAt)
            .Limit(limit)
            .ToListAsync();
    }
}
