using MongoDB.Driver;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;
using Serilog;

namespace Log4YM.Server.Core.Database;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var connectionString = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
        var databaseName = configuration["MongoDB:DatabaseName"] ?? "log4ym";

        Log.Information("MongoDB connecting to database: {DatabaseName}", databaseName);

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        CreateIndexes();
    }

    public IMongoCollection<Qso> Qsos => _database.GetCollection<Qso>("qsos");
    public IMongoCollection<Spot> Spots => _database.GetCollection<Spot>("spots");
    public IMongoCollection<UserSettings> Settings => _database.GetCollection<UserSettings>("settings");
    public IMongoCollection<PluginSettings> PluginSettings => _database.GetCollection<PluginSettings>("pluginSettings");
    public IMongoCollection<Layout> Layouts => _database.GetCollection<Layout>("layouts");
    public IMongoCollection<SmartUnlinkRadioEntity> SmartUnlinkRadios => _database.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios");

    private void CreateIndexes()
    {
        // QSO indexes
        Qsos.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending(q => q.Callsign)),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Descending(q => q.QsoDate)),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending(q => q.Band).Ascending(q => q.Mode)),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending("station.dxcc")),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending("station.grid"))
        });

        // Spot indexes with TTL (24 hour expiry)
        Spots.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<Spot>(
                Builders<Spot>.IndexKeys.Ascending(s => s.CreatedAt),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }
            ),
            new CreateIndexModel<Spot>(Builders<Spot>.IndexKeys.Ascending(s => s.Frequency)),
            new CreateIndexModel<Spot>(Builders<Spot>.IndexKeys.Ascending(s => s.DxCall)),
            new CreateIndexModel<Spot>(Builders<Spot>.IndexKeys.Descending(s => s.Timestamp))
        });
    }
}
