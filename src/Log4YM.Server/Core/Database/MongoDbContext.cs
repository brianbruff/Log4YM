using MongoDB.Driver;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;
using Serilog;

namespace Log4YM.Server.Core.Database;

public class MongoDbContext : IDbContext
{
    private IMongoDatabase? _database;
    private MongoClient? _client;
    private readonly IUserConfigService _userConfigService;
    private readonly IConfiguration _configuration;
    private bool _isInitialized;
    private readonly object _initLock = new();
    private string? _currentDatabaseName;

    public MongoDbContext(IConfiguration configuration, IUserConfigService userConfigService)
    {
        _configuration = configuration;
        _userConfigService = userConfigService;

        // Only attempt initialization if user has already configured MongoDB via setup wizard
        // This prevents blocking on first run when no config.json exists
        // The appsettings.json default (localhost:27017) would cause a long timeout
        if (_userConfigService.IsConfigured())
        {
            TryInitialize();
        }
        else
        {
            Log.Information("MongoDB not configured yet - waiting for setup wizard");
        }
    }

    public bool IsConnected => _isInitialized && _database != null;

    public string? DatabaseName => _currentDatabaseName;

    public bool TryInitialize()
    {
        if (_isInitialized) return true;

        lock (_initLock)
        {
            if (_isInitialized) return true;

            try
            {
                var connectionString = GetConnectionString();
                if (string.IsNullOrEmpty(connectionString))
                {
                    Log.Warning("MongoDB connection string not configured");
                    return false;
                }

                var databaseName = GetDatabaseName();
                _currentDatabaseName = databaseName;

                Log.Information("MongoDB connecting to database: {DatabaseName}", databaseName);

                // Configure client with aggressive timeouts to prevent blocking on startup.
                // SRV DNS lookups for unreachable Atlas clusters can hang for minutes
                // if we rely solely on the driver's built-in timeouts.
                var settings = MongoClientSettings.FromConnectionString(connectionString);
                settings.ConnectTimeout = TimeSpan.FromSeconds(5);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
                settings.SocketTimeout = TimeSpan.FromSeconds(5);

                _client = new MongoClient(settings);
                _database = _client.GetDatabase(databaseName);

                // Test connection with ping, using a hard CancellationToken timeout
                // to guarantee we don't block longer than 10 seconds total even if
                // the driver's internal timeouts don't cover SRV/DNS resolution.
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                _database.RunCommand<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1),
                    cancellationToken: cts.Token);

                // Set initialized BEFORE creating indexes (indexes access collection properties)
                _isInitialized = true;

                CreateIndexes();

                Log.Information("MongoDB connected successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to connect to MongoDB");
                _database = null;
                _client = null;
                _currentDatabaseName = null;
                return false;
            }
        }
    }

    public async Task<bool> ReinitializeAsync(string connectionString, string databaseName)
    {
        // Reset state
        lock (_initLock)
        {
            _isInitialized = false;
            _database = null;
            _client = null;
            _currentDatabaseName = null;
        }

        // Save to user config
        await _userConfigService.SaveConfigAsync(new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = connectionString,
            MongoDbDatabaseName = databaseName
        });

        return TryInitialize();
    }

    private string? GetConnectionString()
    {
        // Use the connection string from config.json (or env var via IConfiguration fallback).
        // The appsettings.json default was removed to prevent fresh desktop installs
        // from accidentally defaulting to MongoDB.
        var config = _userConfigService.GetConfigAsync().GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(config.MongoDbConnectionString))
        {
            return config.MongoDbConnectionString;
        }
        return _configuration["MongoDB:ConnectionString"];
    }

    private string GetDatabaseName()
    {
        var config = _userConfigService.GetConfigAsync().GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(config.MongoDbDatabaseName))
        {
            return config.MongoDbDatabaseName;
        }
        return _configuration["MongoDB:DatabaseName"] ?? "Log4YM";
    }

    private void EnsureConnected()
    {
        if (!_isInitialized || _database == null)
        {
            throw new InvalidOperationException(
                "MongoDB is not connected. Please complete the setup wizard.");
        }
    }

    public IMongoCollection<Qso> Qsos
    {
        get { EnsureConnected(); return _database!.GetCollection<Qso>("qsos"); }
    }

    public IMongoCollection<UserSettings> Settings
    {
        get { EnsureConnected(); return _database!.GetCollection<UserSettings>("settings"); }
    }

    public IMongoCollection<SmartUnlinkRadioEntity> SmartUnlinkRadios
    {
        get { EnsureConnected(); return _database!.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios"); }
    }

    public IMongoCollection<CallsignMapImage> CallsignMapImages
    {
        get { EnsureConnected(); return _database!.GetCollection<CallsignMapImage>("callsign_images"); }
    }

    public IMongoCollection<RadioConfigEntity> RadioConfigs
    {
        get { EnsureConnected(); return _database!.GetCollection<RadioConfigEntity>("radio_configs"); }
    }

    private void CreateIndexes()
    {
        // QSO indexes
        Qsos.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending(q => q.Callsign)),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Descending(q => q.QsoDate)),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending(q => q.Band).Ascending(q => q.Mode)),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending("station.dxcc")),
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending("station.grid")),
            // Index for efficient sync status queries (like QLog's upload status columns)
            new CreateIndexModel<Qso>(Builders<Qso>.IndexKeys.Ascending(q => q.QrzSyncStatus))
        });

        // Callsign map image indexes
        CallsignMapImages.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<CallsignMapImage>(
                Builders<CallsignMapImage>.IndexKeys.Ascending(i => i.Callsign),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<CallsignMapImage>(
                Builders<CallsignMapImage>.IndexKeys.Descending(i => i.SavedAt)),
        });

        // Radio config indexes
        RadioConfigs.Indexes.CreateMany(new[]
        {
            new CreateIndexModel<RadioConfigEntity>(
                Builders<RadioConfigEntity>.IndexKeys.Ascending(r => r.RadioId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RadioConfigEntity>(
                Builders<RadioConfigEntity>.IndexKeys.Ascending(r => r.RadioType)),
        });
    }
}
