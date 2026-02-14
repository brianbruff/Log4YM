using Log4YM.Contracts.Models;

namespace Log4YM.Server.Core.Database.LiteDb;

public class LiteCallsignImageRepository : ICallsignImageRepository
{
    private readonly LiteDbContext _context;

    public LiteCallsignImageRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task UpsertAsync(CallsignMapImage image)
    {
        var existing = _context.CallsignMapImages.FindOne(i => i.Callsign == image.Callsign);

        if (existing != null)
        {
            image.Id = existing.Id;
            image.SavedAt = DateTime.UtcNow;
            _context.CallsignMapImages.Update(image);
        }
        else
        {
            image.SavedAt = DateTime.UtcNow;
            _context.CallsignMapImages.Insert(image);
        }

        _context.Database.Checkpoint();
        return Task.CompletedTask;
    }

    public Task<List<CallsignMapImage>> GetRecentAsync(int limit)
    {
        var results = _context.CallsignMapImages.Query()
            .OrderByDescending(i => i.SavedAt)
            .Limit(limit)
            .ToList();

        return Task.FromResult(results);
    }
}
