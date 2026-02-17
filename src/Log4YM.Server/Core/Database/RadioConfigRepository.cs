using MongoDB.Bson;
using MongoDB.Driver;
using Log4YM.Contracts.Models;
using Log4YM.Server.Native.Hamlib;

namespace Log4YM.Server.Core.Database;

public interface IRadioConfigRepository
{
    Task<List<RadioConfigEntity>> GetAllAsync();
    Task<RadioConfigEntity?> GetByRadioIdAsync(string radioId);
    Task<List<RadioConfigEntity>> GetByTypeAsync(string radioType);
    Task UpsertByRadioIdAsync(RadioConfigEntity entity);
    Task<bool> DeleteByRadioIdAsync(string radioId);

    /// <summary>
    /// One-time migration: move old hamlib_config doc from settings collection to radio_configs.
    /// Returns true if migration was performed.
    /// </summary>
    Task<bool> MigrateOldHamlibConfigAsync();

    /// <summary>
    /// Self-healing: fix any radio_configs docs that have _id: null from a previous bug.
    /// Re-inserts them with a proper auto-generated _id.
    /// </summary>
    Task FixNullIdsAsync();
}

public class RadioConfigRepository : IRadioConfigRepository
{
    private readonly MongoDbContext _context;

    public RadioConfigRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<List<RadioConfigEntity>> GetAllAsync()
    {
        return await _context.RadioConfigs.Find(_ => true).ToListAsync();
    }

    public async Task<RadioConfigEntity?> GetByRadioIdAsync(string radioId)
    {
        return await _context.RadioConfigs.Find(r => r.RadioId == radioId).FirstOrDefaultAsync();
    }

    public async Task<List<RadioConfigEntity>> GetByTypeAsync(string radioType)
    {
        return await _context.RadioConfigs.Find(r => r.RadioType == radioType).ToListAsync();
    }

    public async Task UpsertByRadioIdAsync(RadioConfigEntity entity)
    {
        await _context.RadioConfigs.ReplaceOneAsync(
            r => r.RadioId == entity.RadioId,
            entity,
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task<bool> DeleteByRadioIdAsync(string radioId)
    {
        var result = await _context.RadioConfigs.DeleteOneAsync(r => r.RadioId == radioId);
        return result.DeletedCount > 0;
    }

    public async Task<bool> MigrateOldHamlibConfigAsync()
    {
        // Skip if radio_configs already has hamlib entries
        var existing = await GetByTypeAsync("hamlib");
        if (existing.Count > 0) return false;

        // Look for the old hamlib_config doc in the settings collection
        var settingsCollection = _context.Settings.Database.GetCollection<BsonDocument>("settings");
        var oldDoc = await settingsCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "hamlib_config"))
            .FirstOrDefaultAsync();

        if (oldDoc == null) return false;

        // Extract fields from the old BSON document
        var modelId = oldDoc.GetValue("ModelId", 0).AsInt32;
        if (modelId == 0) return false;

        var entity = new RadioConfigEntity
        {
            RadioId = $"hamlib-{modelId}",
            RadioType = "hamlib",
            DisplayName = oldDoc.GetValue("ModelName", "").AsString,
            HamlibModelId = modelId,
            HamlibModelName = oldDoc.GetValue("ModelName", "").AsString,
            ConnectionType = GetBsonEnumString<HamlibConnectionType>(oldDoc, "ConnectionType", "Serial"),
            SerialPort = GetBsonNullableString(oldDoc, "SerialPort"),
            BaudRate = oldDoc.GetValue("BaudRate", 9600).AsInt32,
            DataBits = oldDoc.GetValue("DataBits", 8).AsInt32,
            StopBits = oldDoc.GetValue("StopBits", 1).AsInt32,
            FlowControl = GetBsonEnumString<HamlibFlowControl>(oldDoc, "FlowControl", "None"),
            Parity = GetBsonEnumString<HamlibParity>(oldDoc, "Parity", "None"),
            Hostname = GetBsonNullableString(oldDoc, "Hostname"),
            NetworkPort = oldDoc.GetValue("NetworkPort", 4532).AsInt32,
            PttType = GetBsonEnumString<HamlibPttType>(oldDoc, "PttType", "Rig"),
            PttPort = GetBsonNullableString(oldDoc, "PttPort"),
            GetFrequency = oldDoc.GetValue("GetFrequency", true).AsBoolean,
            GetMode = oldDoc.GetValue("GetMode", true).AsBoolean,
            GetVfo = oldDoc.GetValue("GetVfo", true).AsBoolean,
            GetPtt = oldDoc.GetValue("GetPtt", true).AsBoolean,
            GetPower = oldDoc.GetValue("GetPower", false).AsBoolean,
            GetRit = oldDoc.GetValue("GetRit", false).AsBoolean,
            GetXit = oldDoc.GetValue("GetXit", false).AsBoolean,
            GetKeySpeed = oldDoc.GetValue("GetKeySpeed", false).AsBoolean,
            PollIntervalMs = oldDoc.GetValue("PollIntervalMs", 250).AsInt32,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await UpsertByRadioIdAsync(entity);

        // Delete the old document
        await settingsCollection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", "hamlib_config"));

        return true;
    }

    public async Task FixNullIdsAsync()
    {
        // Find documents with _id: null (from a previous bug where BsonIgnoreIfDefault was missing)
        var rawCollection = _context.RadioConfigs.Database.GetCollection<BsonDocument>("radio_configs");
        var nullIdDocs = await rawCollection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", BsonNull.Value))
            .ToListAsync();

        foreach (var doc in nullIdDocs)
        {
            // Delete the null-_id original first (avoids unique index conflict on RadioId)
            await rawCollection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", BsonNull.Value));
            // Re-insert with a proper ObjectId
            doc["_id"] = ObjectId.GenerateNewId();
            await rawCollection.InsertOneAsync(doc);
        }
    }

    /// <summary>
    /// Read an enum field from a BsonDocument, handling both int (driver default) and string storage.
    /// </summary>
    private static string GetBsonEnumString<TEnum>(BsonDocument doc, string key, string fallback) where TEnum : struct, Enum
    {
        if (!doc.Contains(key)) return fallback;
        var val = doc[key];
        if (val.IsString) return val.AsString;
        if (val.IsInt32 && Enum.IsDefined(typeof(TEnum), val.AsInt32))
            return ((TEnum)(object)val.AsInt32).ToString();
        return fallback;
    }

    private static string? GetBsonNullableString(BsonDocument doc, string key)
    {
        if (!doc.Contains(key) || doc[key].IsBsonNull) return null;
        return doc[key].AsString;
    }
}
