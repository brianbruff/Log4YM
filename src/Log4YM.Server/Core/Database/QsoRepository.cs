using MongoDB.Driver;
using Log4YM.Contracts.Models;
using Log4YM.Contracts.Api;

namespace Log4YM.Server.Core.Database;

public interface IQsoRepository
{
    Task<Qso?> GetByIdAsync(string id);
    Task<IEnumerable<Qso>> GetRecentAsync(int limit = 100);
    Task<IEnumerable<Qso>> GetAllAsync();
    Task<IEnumerable<Qso>> GetUnsyncedToQrzAsync();
    Task<(IEnumerable<Qso> Items, int TotalCount)> SearchAsync(QsoSearchRequest criteria);
    Task<Qso> CreateAsync(Qso qso);
    Task<bool> UpdateAsync(string id, Qso qso);
    Task<bool> DeleteAsync(string id);
    Task<QsoStatistics> GetStatisticsAsync();
    Task<int> GetCountAsync();
    Task<bool> ExistsAsync(string callsign, DateTime qsoDate, string timeOn, string band, string mode);
    Task<IEnumerable<Qso>> GetByIdsAsync(IEnumerable<string> ids);
    Task<bool> UpdateQrzSyncStatusAsync(string id, string qrzLogId);
    Task<int> GetPendingSyncCountAsync();
    Task<long> DeleteAllAsync();
}

public class QsoRepository : IQsoRepository
{
    private readonly IMongoCollection<Qso> _collection;

    public QsoRepository(MongoDbContext context)
    {
        _collection = context.Qsos;
    }

    public async Task<Qso?> GetByIdAsync(string id)
    {
        return await _collection.Find(q => q.Id == id).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Qso>> GetRecentAsync(int limit = 100)
    {
        return await _collection
            .Find(_ => true)
            .SortByDescending(q => q.QsoDate)
            .ThenByDescending(q => q.TimeOn)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<Qso>> GetAllAsync()
    {
        return await _collection
            .Find(_ => true)
            .SortByDescending(q => q.QsoDate)
            .ThenByDescending(q => q.TimeOn)
            .ToListAsync();
    }

    public async Task<(IEnumerable<Qso> Items, int TotalCount)> SearchAsync(QsoSearchRequest criteria)
    {
        var builder = Builders<Qso>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrEmpty(criteria.Callsign))
            filter &= builder.Regex(q => q.Callsign, new MongoDB.Bson.BsonRegularExpression(criteria.Callsign, "i"));

        if (!string.IsNullOrEmpty(criteria.Name))
            filter &= builder.Regex(q => q.Name, new MongoDB.Bson.BsonRegularExpression(criteria.Name, "i"));

        if (!string.IsNullOrEmpty(criteria.Band))
            filter &= builder.Eq(q => q.Band, criteria.Band);

        if (!string.IsNullOrEmpty(criteria.Mode))
            filter &= builder.Eq(q => q.Mode, criteria.Mode);

        if (criteria.FromDate.HasValue)
            filter &= builder.Gte(q => q.QsoDate, criteria.FromDate.Value);

        if (criteria.ToDate.HasValue)
            filter &= builder.Lte(q => q.QsoDate, criteria.ToDate.Value.AddDays(1));

        if (criteria.Dxcc.HasValue)
            filter &= builder.Eq(q => q.Dxcc, criteria.Dxcc.Value);

        var totalCount = await _collection.CountDocumentsAsync(filter);

        var items = await _collection
            .Find(filter)
            .SortByDescending(q => q.QsoDate)
            .ThenByDescending(q => q.TimeOn)
            .Skip(criteria.Skip)
            .Limit(criteria.Limit)
            .ToListAsync();

        return (items, (int)totalCount);
    }

    public async Task<Qso> CreateAsync(Qso qso)
    {
        qso.CreatedAt = DateTime.UtcNow;
        qso.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(qso);
        return qso;
    }

    public async Task<bool> UpdateAsync(string id, Qso qso)
    {
        qso.UpdatedAt = DateTime.UtcNow;

        // If QSO was previously synced, mark as Modified (like QLog's trigger)
        if (qso.QrzSyncStatus == SyncStatus.Synced)
        {
            qso.QrzSyncStatus = SyncStatus.Modified;
        }

        var result = await _collection.ReplaceOneAsync(q => q.Id == id, qso);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(q => q.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<QsoStatistics> GetStatisticsAsync()
    {
        var builder = Builders<Qso>.Filter;
        var emptyFilter = builder.Empty;

        var totalQsos = await _collection.CountDocumentsAsync(emptyFilter);

        var uniqueCallsigns = await _collection.Distinct(q => q.Callsign, emptyFilter).ToListAsync();
        var uniqueDxcc = await _collection.Distinct(q => q.Dxcc, emptyFilter).ToListAsync();
        var uniqueGrids = await _collection.Distinct(q => q.Grid, emptyFilter).ToListAsync();

        // Count today's QSOs
        var today = DateTime.UtcNow.Date;
        var todayFilter = builder.Gte(q => q.QsoDate, today);
        var qsosToday = await _collection.CountDocumentsAsync(todayFilter);

        // Aggregate by band
        var bandAgg = await _collection.Aggregate()
            .Group(q => q.Band, g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync();

        // Aggregate by mode
        var modeAgg = await _collection.Aggregate()
            .Group(q => q.Mode, g => new { Mode = g.Key, Count = g.Count() })
            .ToListAsync();

        return new QsoStatistics(
            TotalQsos: (int)totalQsos,
            UniqueCallsigns: uniqueCallsigns.Count,
            UniqueCountries: uniqueDxcc.Count(d => d.HasValue),
            UniqueGrids: uniqueGrids.Count(g => !string.IsNullOrEmpty(g)),
            QsosToday: (int)qsosToday,
            QsosByBand: bandAgg.ToDictionary(x => x.Band ?? "Unknown", x => x.Count),
            QsosByMode: modeAgg.ToDictionary(x => x.Mode ?? "Unknown", x => x.Count)
        );
    }

    public async Task<int> GetCountAsync()
    {
        return (int)await _collection.CountDocumentsAsync(_ => true);
    }

    public async Task<bool> ExistsAsync(string callsign, DateTime qsoDate, string timeOn, string band, string mode)
    {
        var builder = Builders<Qso>.Filter;
        var filter = builder.Eq(q => q.Callsign, callsign.ToUpperInvariant())
            & builder.Eq(q => q.QsoDate, qsoDate.Date)
            & builder.Eq(q => q.TimeOn, timeOn)
            & builder.Eq(q => q.Band, band)
            & builder.Eq(q => q.Mode, mode);

        var count = await _collection.CountDocumentsAsync(filter);
        return count > 0;
    }

    public async Task<IEnumerable<Qso>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        var filter = Builders<Qso>.Filter.In(q => q.Id, idList);
        return await _collection.Find(filter).ToListAsync();
    }

    public async Task<IEnumerable<Qso>> GetUnsyncedToQrzAsync()
    {
        // Get QSOs that need syncing: NotSynced OR Modified (like QLog's N/M status)
        var filter = Builders<Qso>.Filter.Or(
            Builders<Qso>.Filter.Eq(q => q.QrzSyncStatus, SyncStatus.NotSynced),
            Builders<Qso>.Filter.Eq(q => q.QrzSyncStatus, SyncStatus.Modified),
            // Also include legacy records without status (QrzLogId == null)
            Builders<Qso>.Filter.And(
                Builders<Qso>.Filter.Eq(q => q.QrzLogId, null),
                Builders<Qso>.Filter.Eq(q => q.QrzSyncStatus, SyncStatus.NotSynced)
            )
        );
        return await _collection
            .Find(filter)
            .SortByDescending(q => q.QsoDate)
            .ThenByDescending(q => q.TimeOn)
            .ToListAsync();
    }

    public async Task<int> GetPendingSyncCountAsync()
    {
        var filter = Builders<Qso>.Filter.Or(
            Builders<Qso>.Filter.Eq(q => q.QrzSyncStatus, SyncStatus.NotSynced),
            Builders<Qso>.Filter.Eq(q => q.QrzSyncStatus, SyncStatus.Modified)
        );
        return (int)await _collection.CountDocumentsAsync(filter);
    }

    public async Task<bool> UpdateQrzSyncStatusAsync(string id, string qrzLogId)
    {
        var update = Builders<Qso>.Update
            .Set(q => q.QrzLogId, qrzLogId)
            .Set(q => q.QrzSyncedAt, DateTime.UtcNow)
            .Set(q => q.QrzSyncStatus, SyncStatus.Synced)
            .Set(q => q.UpdatedAt, DateTime.UtcNow);

        var result = await _collection.UpdateOneAsync(q => q.Id == id, update);
        return result.ModifiedCount > 0;
    }

    public async Task<long> DeleteAllAsync()
    {
        var result = await _collection.DeleteManyAsync(_ => true);
        return result.DeletedCount;
    }
}
