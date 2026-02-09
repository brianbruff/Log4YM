using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public interface ISettingsService
{
    Task<UserSettings> GetSettingsAsync(string id = "default");
    Task<UserSettings> SaveSettingsAsync(UserSettings settings);
}

public class SettingsService : ISettingsService
{
    private readonly ISettingsRepository _repository;

    public SettingsService(ISettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<UserSettings> GetSettingsAsync(string id = "default")
    {
        var settings = await _repository.GetAsync(id);
        if (settings == null)
            return new UserSettings { Id = id };

        // Ensure nested settings objects are never null (handles documents
        // created before new settings sections were added to the schema)
        settings.Station ??= new();
        settings.Qrz ??= new();
        settings.Appearance ??= new();
        settings.Rotator ??= new();
        settings.Radio ??= new();
        settings.Map ??= new();
        settings.Cluster ??= new();
        settings.Ai ??= new();

        return settings;
    }

    public async Task<UserSettings> SaveSettingsAsync(UserSettings settings)
    {
        return await _repository.UpsertAsync(settings);
    }
}
