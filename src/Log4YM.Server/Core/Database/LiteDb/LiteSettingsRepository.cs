using Log4YM.Contracts.Models;

namespace Log4YM.Server.Core.Database.LiteDb;

public class LiteSettingsRepository : ISettingsRepository
{
    private readonly LiteDbContext _context;

    public LiteSettingsRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<UserSettings?> GetAsync(string id = "default")
    {
        var settings = _context.Settings.FindById(id);
        return Task.FromResult<UserSettings?>(settings);
    }

    public Task<UserSettings> UpsertAsync(UserSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        _context.Settings.Upsert(settings);
        return Task.FromResult(settings);
    }
}
