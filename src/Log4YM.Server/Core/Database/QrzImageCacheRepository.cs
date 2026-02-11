using MongoDB.Driver;
using Log4YM.Contracts.Models;

namespace Log4YM.Server.Core.Database;

public interface IQrzImageCacheRepository
{
    /// <summary>
    /// Get cached QRZ data for a callsign
    /// </summary>
    Task<QrzImageCache?> GetByCallsignAsync(string callsign);

    /// <summary>
    /// Get cached QRZ data for multiple callsigns
    /// </summary>
    Task<IEnumerable<QrzImageCache>> GetByCallsignsAsync(IEnumerable<string> callsigns);

    /// <summary>
    /// Save or update cached QRZ data for a callsign
    /// </summary>
    Task UpsertAsync(QrzImageCache cache);

    /// <summary>
    /// Update the last accessed time for a callsign
    /// </summary>
    Task UpdateLastAccessedAsync(string callsign);

    /// <summary>
    /// Maintain cache by keeping only the most recent N entries (LRU eviction)
    /// </summary>
    Task MaintainCacheSizeAsync(int maxEntries = 100);
}

public class QrzImageCacheRepository : IQrzImageCacheRepository
{
    private readonly IMongoCollection<QrzImageCache> _collection;

    public QrzImageCacheRepository(MongoDbContext context)
    {
        _collection = context.QrzImageCache;
    }

    public async Task<QrzImageCache?> GetByCallsignAsync(string callsign)
    {
        var upperCall = callsign.ToUpperInvariant();
        var result = await _collection.Find(c => c.Callsign == upperCall).FirstOrDefaultAsync();

        if (result != null)
        {
            // Update last accessed time in background (don't await)
            _ = UpdateLastAccessedAsync(upperCall);
        }

        return result;
    }

    public async Task<IEnumerable<QrzImageCache>> GetByCallsignsAsync(IEnumerable<string> callsigns)
    {
        var upperCallsigns = callsigns.Select(c => c.ToUpperInvariant()).ToList();
        var filter = Builders<QrzImageCache>.Filter.In(c => c.Callsign, upperCallsigns);
        var results = await _collection.Find(filter).ToListAsync();

        // Update last accessed times in background for all retrieved entries
        foreach (var result in results)
        {
            _ = UpdateLastAccessedAsync(result.Callsign);
        }

        return results;
    }

    public async Task UpsertAsync(QrzImageCache cache)
    {
        cache.Callsign = cache.Callsign.ToUpperInvariant();
        cache.FetchedAt = DateTime.UtcNow;
        cache.LastAccessedAt = DateTime.UtcNow;

        var filter = Builders<QrzImageCache>.Filter.Eq(c => c.Callsign, cache.Callsign);
        var options = new ReplaceOptions { IsUpsert = true };

        await _collection.ReplaceOneAsync(filter, cache, options);
    }

    public async Task UpdateLastAccessedAsync(string callsign)
    {
        var upperCall = callsign.ToUpperInvariant();
        var filter = Builders<QrzImageCache>.Filter.Eq(c => c.Callsign, upperCall);
        var update = Builders<QrzImageCache>.Update.Set(c => c.LastAccessedAt, DateTime.UtcNow);

        await _collection.UpdateOneAsync(filter, update);
    }

    public async Task MaintainCacheSizeAsync(int maxEntries = 100)
    {
        // Count total entries
        var count = await _collection.CountDocumentsAsync(_ => true);

        if (count <= maxEntries)
        {
            return; // Cache is within limits
        }

        // Find the threshold lastAccessedAt time (Nth most recent)
        var threshold = await _collection
            .Find(_ => true)
            .SortByDescending(c => c.LastAccessedAt)
            .Skip(maxEntries)
            .Limit(1)
            .Project(c => c.LastAccessedAt)
            .FirstOrDefaultAsync();

        if (threshold == default)
        {
            return;
        }

        // Delete entries older than threshold
        var filter = Builders<QrzImageCache>.Filter.Lt(c => c.LastAccessedAt, threshold);
        await _collection.DeleteManyAsync(filter);
    }
}
