using MongoDB.Driver;
using Log4YM.Server.Services;

namespace Log4YM.Server.Core.Database;

public interface ISmartUnlinkRepository
{
    Task<List<SmartUnlinkRadioEntity>> GetAllAsync();
    Task InsertAsync(SmartUnlinkRadioEntity entity);
    Task<bool> UpdateAsync(SmartUnlinkRadioEntity entity);
    Task<bool> SetEnabledAsync(string id, bool enabled);
    Task<bool> DeleteAsync(string id);
}

public class SmartUnlinkRepository : ISmartUnlinkRepository
{
    private readonly MongoDbContext _context;

    public SmartUnlinkRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<List<SmartUnlinkRadioEntity>> GetAllAsync()
    {
        return await _context.SmartUnlinkRadios.Find(_ => true).ToListAsync();
    }

    public async Task InsertAsync(SmartUnlinkRadioEntity entity)
    {
        await _context.SmartUnlinkRadios.InsertOneAsync(entity);
    }

    public async Task<bool> UpdateAsync(SmartUnlinkRadioEntity entity)
    {
        var update = Builders<SmartUnlinkRadioEntity>.Update
            .Set(r => r.Name, entity.Name)
            .Set(r => r.IpAddress, entity.IpAddress)
            .Set(r => r.Model, entity.Model)
            .Set(r => r.SerialNumber, entity.SerialNumber)
            .Set(r => r.Version, entity.Version)
            .Set(r => r.Callsign, entity.Callsign)
            .Set(r => r.Enabled, entity.Enabled)
            .Set(r => r.UpdatedAt, entity.UpdatedAt);

        var result = await _context.SmartUnlinkRadios.UpdateOneAsync(
            r => r.Id == entity.Id,
            update);

        return result.ModifiedCount > 0;
    }

    public async Task<bool> SetEnabledAsync(string id, bool enabled)
    {
        var update = Builders<SmartUnlinkRadioEntity>.Update
            .Set(r => r.Enabled, enabled)
            .Set(r => r.UpdatedAt, DateTime.UtcNow);

        var result = await _context.SmartUnlinkRadios.UpdateOneAsync(r => r.Id == id, update);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _context.SmartUnlinkRadios.DeleteOneAsync(r => r.Id == id);
        return result.DeletedCount > 0;
    }
}
