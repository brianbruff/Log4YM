using LiteDB;
using Log4YM.Server.Services;

namespace Log4YM.Server.Core.Database.LiteDb;

public class LiteSmartUnlinkRepository : ISmartUnlinkRepository
{
    private readonly LiteDbContext _context;

    public LiteSmartUnlinkRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<List<SmartUnlinkRadioEntity>> GetAllAsync()
    {
        var results = _context.SmartUnlinkRadios.FindAll().ToList();
        return Task.FromResult(results);
    }

    public Task InsertAsync(SmartUnlinkRadioEntity entity)
    {
        if (string.IsNullOrEmpty(entity.Id))
        {
            entity = entity with { Id = ObjectId.NewObjectId().ToString() };
        }

        _context.SmartUnlinkRadios.Insert(entity);
        _context.Database.Checkpoint();
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(SmartUnlinkRadioEntity entity)
    {
        var success = _context.SmartUnlinkRadios.Update(entity);
        _context.Database.Checkpoint();
        return Task.FromResult(success);
    }

    public Task<bool> SetEnabledAsync(string id, bool enabled)
    {
        var entity = _context.SmartUnlinkRadios.FindById(new BsonValue(id));
        if (entity == null) return Task.FromResult(false);

        var updated = entity with
        {
            Enabled = enabled,
            UpdatedAt = DateTime.UtcNow
        };

        var success = _context.SmartUnlinkRadios.Update(updated);
        _context.Database.Checkpoint();
        return Task.FromResult(success);
    }

    public Task<bool> DeleteAsync(string id)
    {
        var success = _context.SmartUnlinkRadios.Delete(new BsonValue(id));
        _context.Database.Checkpoint();
        return Task.FromResult(success);
    }
}
