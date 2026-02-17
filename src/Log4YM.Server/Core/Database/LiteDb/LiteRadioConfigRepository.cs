using LiteDB;
using Log4YM.Contracts.Models;
using Log4YM.Server.Native.Hamlib;

namespace Log4YM.Server.Core.Database.LiteDb;

public class LiteRadioConfigRepository : IRadioConfigRepository
{
    private readonly LiteDbContext _context;

    public LiteRadioConfigRepository(LiteDbContext context)
    {
        _context = context;
    }

    public Task<List<RadioConfigEntity>> GetAllAsync()
    {
        var results = _context.RadioConfigs.FindAll().ToList();
        return Task.FromResult(results);
    }

    public Task<RadioConfigEntity?> GetByRadioIdAsync(string radioId)
    {
        var result = _context.RadioConfigs.FindOne(r => r.RadioId == radioId);
        return Task.FromResult<RadioConfigEntity?>(result);
    }

    public Task<List<RadioConfigEntity>> GetByTypeAsync(string radioType)
    {
        var results = _context.RadioConfigs.Find(r => r.RadioType == radioType).ToList();
        return Task.FromResult(results);
    }

    public Task UpsertByRadioIdAsync(RadioConfigEntity entity)
    {
        var existing = _context.RadioConfigs.FindOne(r => r.RadioId == entity.RadioId);
        if (existing != null)
        {
            var updated = entity with { Id = existing.Id };
            _context.RadioConfigs.Update(updated);
        }
        else
        {
            var toInsert = entity with { Id = string.IsNullOrEmpty(entity.Id) ? ObjectId.NewObjectId().ToString() : entity.Id };
            _context.RadioConfigs.Insert(toInsert);
        }
        _context.Database.Checkpoint();
        return Task.CompletedTask;
    }

    public Task<bool> DeleteByRadioIdAsync(string radioId)
    {
        var count = _context.RadioConfigs.DeleteMany(r => r.RadioId == radioId);
        _context.Database.Checkpoint();
        return Task.FromResult(count > 0);
    }

    public Task FixNullIdsAsync()
    {
        // LiteDB uses string IDs — find docs where Id is null or empty and assign new ObjectIds
        var rawCollection = _context.Database.GetCollection("radio_configs");
        var allDocs = rawCollection.FindAll().ToList();
        foreach (var doc in allDocs)
        {
            if (!doc.ContainsKey("_id") || doc["_id"].IsNull || (doc["_id"].IsString && string.IsNullOrEmpty(doc["_id"].AsString)))
            {
                // Delete first to avoid unique index conflict on RadioId
                rawCollection.Delete(doc["_id"]);
                doc["_id"] = ObjectId.NewObjectId().ToString();
                rawCollection.Insert(doc);
            }
        }
        _context.Database.Checkpoint();
        return Task.CompletedTask;
    }

    public Task<bool> MigrateOldHamlibConfigAsync()
    {
        // Skip if radio_configs already has hamlib entries
        var existing = _context.RadioConfigs.Find(r => r.RadioType == "hamlib").ToList();
        if (existing.Count > 0) return Task.FromResult(false);

        // Look for the old hamlib_config doc in the settings collection (raw BsonDocument)
        var settingsCollection = _context.Database.GetCollection("settings");
        var oldDoc = settingsCollection.FindById("hamlib_config");
        if (oldDoc == null) return Task.FromResult(false);

        var modelId = oldDoc.ContainsKey("ModelId") ? oldDoc["ModelId"].AsInt32 : 0;
        if (modelId == 0) return Task.FromResult(false);

        var entity = new RadioConfigEntity
        {
            Id = ObjectId.NewObjectId().ToString(),
            RadioId = $"hamlib-{modelId}",
            RadioType = "hamlib",
            DisplayName = oldDoc.ContainsKey("ModelName") ? oldDoc["ModelName"].AsString : "",
            HamlibModelId = modelId,
            HamlibModelName = oldDoc.ContainsKey("ModelName") ? oldDoc["ModelName"].AsString : "",
            // LiteDB stores enums as ints; convert to string name
            ConnectionType = GetEnumString<HamlibConnectionType>(oldDoc, "ConnectionType", "Serial"),
            SerialPort = GetNullableString(oldDoc, "SerialPort"),
            BaudRate = oldDoc.ContainsKey("BaudRate") ? oldDoc["BaudRate"].AsInt32 : 9600,
            DataBits = oldDoc.ContainsKey("DataBits") ? oldDoc["DataBits"].AsInt32 : 8,
            StopBits = oldDoc.ContainsKey("StopBits") ? oldDoc["StopBits"].AsInt32 : 1,
            FlowControl = GetEnumString<HamlibFlowControl>(oldDoc, "FlowControl", "None"),
            Parity = GetEnumString<HamlibParity>(oldDoc, "Parity", "None"),
            Hostname = GetNullableString(oldDoc, "Hostname"),
            NetworkPort = oldDoc.ContainsKey("NetworkPort") ? oldDoc["NetworkPort"].AsInt32 : 4532,
            PttType = GetEnumString<HamlibPttType>(oldDoc, "PttType", "Rig"),
            PttPort = GetNullableString(oldDoc, "PttPort"),
            GetFrequency = oldDoc.ContainsKey("GetFrequency") ? oldDoc["GetFrequency"].AsBoolean : true,
            GetMode = oldDoc.ContainsKey("GetMode") ? oldDoc["GetMode"].AsBoolean : true,
            GetVfo = oldDoc.ContainsKey("GetVfo") ? oldDoc["GetVfo"].AsBoolean : true,
            GetPtt = oldDoc.ContainsKey("GetPtt") ? oldDoc["GetPtt"].AsBoolean : true,
            GetPower = oldDoc.ContainsKey("GetPower") ? oldDoc["GetPower"].AsBoolean : false,
            GetRit = oldDoc.ContainsKey("GetRit") ? oldDoc["GetRit"].AsBoolean : false,
            GetXit = oldDoc.ContainsKey("GetXit") ? oldDoc["GetXit"].AsBoolean : false,
            GetKeySpeed = oldDoc.ContainsKey("GetKeySpeed") ? oldDoc["GetKeySpeed"].AsBoolean : false,
            PollIntervalMs = oldDoc.ContainsKey("PollIntervalMs") ? oldDoc["PollIntervalMs"].AsInt32 : 250,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _context.RadioConfigs.Insert(entity);

        // Delete the old document
        settingsCollection.Delete("hamlib_config");
        _context.Database.Checkpoint();

        return Task.FromResult(true);
    }

    /// <summary>
    /// Read an enum field from a BsonDocument, handling both int (LiteDB default) and string storage.
    /// </summary>
    private static string GetEnumString<TEnum>(BsonDocument doc, string key, string fallback) where TEnum : struct, Enum
    {
        if (!doc.ContainsKey(key)) return fallback;
        var val = doc[key];
        if (val.IsString) return val.AsString;
        if (val.IsInt32 && Enum.IsDefined(typeof(TEnum), val.AsInt32))
            return ((TEnum)(object)val.AsInt32).ToString();
        return fallback;
    }

    private static string? GetNullableString(BsonDocument doc, string key)
    {
        if (!doc.ContainsKey(key) || doc[key].IsNull) return null;
        return doc[key].AsString;
    }
}
