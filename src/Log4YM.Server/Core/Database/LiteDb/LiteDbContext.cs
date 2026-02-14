using LiteDB;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;
using Serilog;

namespace Log4YM.Server.Core.Database.LiteDb;

public class LiteDbContext : IDbContext, IDisposable
{
    private LiteDatabase? _database;
    private readonly IUserConfigService _userConfigService;
    private readonly object _initLock = new();
    private bool _isInitialized;
    private string? _dbPath;

    public LiteDbContext(IUserConfigService userConfigService)
    {
        _userConfigService = userConfigService;
        TryInitialize();
    }

    public bool IsConnected => _isInitialized && _database != null;

    public string? DatabaseName => _dbPath != null ? Path.GetFileName(_dbPath) : null;

    public bool TryInitialize()
    {
        if (_isInitialized) return true;

        lock (_initLock)
        {
            if (_isInitialized) return true;

            try
            {
                _dbPath = GetDatabasePath();
                var directory = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Log.Information("LiteDB opening database at: {DbPath}", _dbPath);

                _database = new LiteDatabase(_dbPath);
                _isInitialized = true;

                CreateIndexes();

                Log.Information("LiteDB initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize LiteDB");
                _database?.Dispose();
                _database = null;
                return false;
            }
        }
    }

    public async Task<bool> ReinitializeAsync(string connectionString, string databaseName)
    {
        lock (_initLock)
        {
            _isInitialized = false;
            _database?.Dispose();
            _database = null;
            _dbPath = null;
        }

        await _userConfigService.SaveConfigAsync(new UserConfig
        {
            Provider = DatabaseProvider.Local,
        });

        return TryInitialize();
    }

    internal LiteDatabase Database
    {
        get
        {
            EnsureConnected();
            return _database!;
        }
    }

    internal ILiteCollection<Qso> Qsos
    {
        get { EnsureConnected(); return _database!.GetCollection<Qso>("qsos"); }
    }

    internal ILiteCollection<UserSettings> Settings
    {
        get { EnsureConnected(); return _database!.GetCollection<UserSettings>("settings"); }
    }

    internal ILiteCollection<SmartUnlinkRadioEntity> SmartUnlinkRadios
    {
        get { EnsureConnected(); return _database!.GetCollection<SmartUnlinkRadioEntity>("smartunlink_radios"); }
    }

    internal ILiteCollection<CallsignMapImage> CallsignMapImages
    {
        get { EnsureConnected(); return _database!.GetCollection<CallsignMapImage>("callsign_images"); }
    }

    private void EnsureConnected()
    {
        if (!_isInitialized || _database == null)
        {
            throw new InvalidOperationException(
                "LiteDB is not initialized. Please complete the setup wizard.");
        }
    }

    private string GetDatabasePath()
    {
        var configPath = _userConfigService.GetConfigPath();
        var configDir = Path.GetDirectoryName(configPath)!;
        return Path.Combine(configDir, "log4ym.db");
    }

    private void CreateIndexes()
    {
        // QSO indexes
        Qsos.EnsureIndex(q => q.Callsign);
        Qsos.EnsureIndex(q => q.QsoDate);
        Qsos.EnsureIndex(q => q.Band);
        Qsos.EnsureIndex(q => q.Mode);
        Qsos.EnsureIndex(q => q.QrzSyncStatus);

        // Callsign map image indexes
        CallsignMapImages.EnsureIndex(i => i.Callsign, true);
        CallsignMapImages.EnsureIndex(i => i.SavedAt);
    }

    public void Dispose()
    {
        _database?.Dispose();
        _database = null;
        _isInitialized = false;
    }
}
