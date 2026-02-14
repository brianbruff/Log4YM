using LiteDB;
using Log4YM.Contracts.Models;
using Log4YM.Contracts.Api;

namespace Log4YM.Server.Core.Database.LiteDb;

public class LiteQsoRepository : IQsoRepository
{
    private readonly LiteDbContext _context;

    public LiteQsoRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<Qso?> GetByIdAsync(string id)
    {
        var qso = _context.Qsos.FindById(new BsonValue(id));
        return Task.FromResult<Qso?>(qso);
    }

    public Task<IEnumerable<Qso>> GetRecentAsync(int limit = 100)
    {
        var results = _context.Qsos.Query()
            .OrderByDescending(q => q.QsoDate)
            .Limit(limit)
            .ToList();

        return Task.FromResult<IEnumerable<Qso>>(results);
    }

    public Task<IEnumerable<Qso>> GetAllAsync()
    {
        var results = _context.Qsos.Query()
            .OrderByDescending(q => q.QsoDate)
            .ToList();

        return Task.FromResult<IEnumerable<Qso>>(results);
    }

    public Task<(IEnumerable<Qso> Items, int TotalCount)> SearchAsync(QsoSearchRequest criteria)
    {
        var query = _context.Qsos.Query();

        // Build filters using Where clauses
        if (!string.IsNullOrEmpty(criteria.Callsign))
        {
            var pattern = criteria.Callsign;
            query = query.Where(q => q.Callsign.Contains(pattern) ||
                q.Callsign.ToUpper().Contains(pattern.ToUpper()));
        }

        if (!string.IsNullOrEmpty(criteria.Name))
        {
            var namePattern = criteria.Name;
            query = query.Where(q => q.Name != null &&
                (q.Name.Contains(namePattern) ||
                 q.Name.ToUpper().Contains(namePattern.ToUpper())));
        }

        if (!string.IsNullOrEmpty(criteria.Band))
            query = query.Where(q => q.Band == criteria.Band);

        if (!string.IsNullOrEmpty(criteria.Mode))
            query = query.Where(q => q.Mode == criteria.Mode);

        if (criteria.FromDate.HasValue)
        {
            var fromDate = criteria.FromDate.Value;
            query = query.Where(q => q.QsoDate >= fromDate);
        }

        if (criteria.ToDate.HasValue)
        {
            var toDate = criteria.ToDate.Value.AddDays(1);
            query = query.Where(q => q.QsoDate <= toDate);
        }

        if (criteria.Dxcc.HasValue)
        {
            var dxcc = criteria.Dxcc.Value;
            query = query.Where(q => q.Dxcc == dxcc);
        }

        // Get total count by executing the query
        var allMatches = query.ToList();
        var totalCount = allMatches.Count;

        // Apply sorting, skip, and limit in-memory
        var items = allMatches
            .OrderByDescending(q => q.QsoDate)
            .ThenByDescending(q => q.TimeOn)
            .Skip(criteria.Skip)
            .Take(criteria.Limit)
            .ToList();

        return Task.FromResult<(IEnumerable<Qso> Items, int TotalCount)>((items, totalCount));
    }

    public Task<Qso> CreateAsync(Qso qso)
    {
        qso.CreatedAt = DateTime.UtcNow;
        qso.UpdatedAt = DateTime.UtcNow;

        // Generate an ID if not set
        if (string.IsNullOrEmpty(qso.Id))
        {
            qso.Id = ObjectId.NewObjectId().ToString();
        }

        _context.Qsos.Insert(qso);
        _context.Database.Checkpoint();
        return Task.FromResult(qso);
    }

    public Task<bool> UpdateAsync(string id, Qso qso)
    {
        qso.UpdatedAt = DateTime.UtcNow;

        // If QSO was previously synced, mark as Modified (like QLog's trigger)
        if (qso.QrzSyncStatus == SyncStatus.Synced)
        {
            qso.QrzSyncStatus = SyncStatus.Modified;
        }

        qso.Id = id;
        var success = _context.Qsos.Update(qso);
        _context.Database.Checkpoint();
        return Task.FromResult(success);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var success = _context.Qsos.Delete(new BsonValue(id));
        _context.Database.Checkpoint();
        return Task.FromResult(success);
    }

    public Task<QsoStatistics> GetStatisticsAsync()
    {
        var all = _context.Qsos.FindAll().ToList();

        var today = DateTime.UtcNow.Date;
        var qsosToday = all.Count(q => q.QsoDate >= today);

        var stats = new QsoStatistics(
            TotalQsos: all.Count,
            UniqueCallsigns: all.Select(q => q.Callsign).Distinct().Count(),
            UniqueCountries: all.Where(q => q.Dxcc.HasValue).Select(q => q.Dxcc).Distinct().Count(),
            UniqueGrids: all.Where(q => !string.IsNullOrEmpty(q.Grid)).Select(q => q.Grid).Distinct().Count(),
            QsosToday: qsosToday,
            QsosByBand: all.GroupBy(q => q.Band ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count()),
            QsosByMode: all.GroupBy(q => q.Mode ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count())
        );

        return Task.FromResult(stats);
    }

    public Task<int> GetCountAsync()
    {
        var count = _context.Qsos.Count();
        return Task.FromResult(count);
    }

    public Task<bool> ExistsAsync(string callsign, DateTime qsoDate, string timeOn, string band, string mode)
    {
        var upperCallsign = callsign.ToUpperInvariant();
        var date = qsoDate.Date;

        var exists = _context.Qsos.Query()
            .Where(q => q.Callsign == upperCallsign
                && q.QsoDate == date
                && q.TimeOn == timeOn
                && q.Band == band
                && q.Mode == mode)
            .Exists();

        return Task.FromResult(exists);
    }

    public Task<IEnumerable<Qso>> GetByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        var results = _context.Qsos.Find(q => idList.Contains(q.Id)).ToList();
        return Task.FromResult<IEnumerable<Qso>>(results);
    }

    public Task<IEnumerable<Qso>> GetUnsyncedToQrzAsync()
    {
        var results = _context.Qsos.Find(q =>
            q.QrzSyncStatus == SyncStatus.NotSynced ||
            q.QrzSyncStatus == SyncStatus.Modified)
            .OrderByDescending(q => q.QsoDate)
            .ThenByDescending(q => q.TimeOn)
            .ToList();

        return Task.FromResult<IEnumerable<Qso>>(results);
    }

    public Task<int> GetPendingSyncCountAsync()
    {
        var count = _context.Qsos.Count(q =>
            q.QrzSyncStatus == SyncStatus.NotSynced ||
            q.QrzSyncStatus == SyncStatus.Modified);

        return Task.FromResult(count);
    }

    public Task<bool> UpdateQrzSyncStatusAsync(string id, string qrzLogId)
    {
        var qso = _context.Qsos.FindById(new BsonValue(id));
        if (qso == null) return Task.FromResult(false);

        qso.QrzLogId = qrzLogId;
        qso.QrzSyncedAt = DateTime.UtcNow;
        qso.QrzSyncStatus = SyncStatus.Synced;
        qso.UpdatedAt = DateTime.UtcNow;

        var success = _context.Qsos.Update(qso);
        _context.Database.Checkpoint();
        return Task.FromResult(success);
    }

    public Task<long> DeleteAllAsync()
    {
        var count = _context.Qsos.DeleteAll();
        _context.Database.Checkpoint();
        return Task.FromResult((long)count);
    }
}
