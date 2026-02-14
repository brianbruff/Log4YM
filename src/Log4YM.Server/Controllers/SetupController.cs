using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SetupController : ControllerBase
{
    private readonly IDbContext _dbContext;
    private readonly IUserConfigService _userConfigService;
    private readonly ILogger<SetupController> _logger;

    public SetupController(
        IDbContext dbContext,
        IUserConfigService userConfigService,
        ILogger<SetupController> logger)
    {
        _dbContext = dbContext;
        _userConfigService = userConfigService;
        _logger = logger;
    }

    /// <summary>
    /// Get the current setup status
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(SetupStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SetupStatusResponse>> GetStatus()
    {
        var config = await _userConfigService.GetConfigAsync();

        return Ok(new SetupStatusResponse
        {
            IsConfigured = _userConfigService.IsConfigured(),
            IsConnected = _dbContext.IsConnected,
            Provider = config.Provider == DatabaseProvider.MongoDb ? "mongodb" : "local",
            ConfiguredAt = config.ConfiguredAt,
            DatabaseName = _dbContext.DatabaseName ?? config.MongoDbDatabaseName
        });
    }

    /// <summary>
    /// Test a MongoDB connection string without saving
    /// </summary>
    [HttpPost("test-connection")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestConnectionResponse>> TestConnection(
        [FromBody] TestConnectionRequest request)
    {
        if (string.IsNullOrEmpty(request.ConnectionString))
        {
            return Ok(new TestConnectionResponse
            {
                Success = false,
                Message = "Connection string is required"
            });
        }

        try
        {
            // Configure client with reasonable timeouts for testing
            var settings = MongoClientSettings.FromConnectionString(request.ConnectionString);
            settings.ConnectTimeout = TimeSpan.FromSeconds(5);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            settings.SocketTimeout = TimeSpan.FromSeconds(10);

            var client = new MongoClient(settings);
            var database = client.GetDatabase(request.DatabaseName ?? "Log4YM");

            // Test with ping command
            await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1));

            // Get server info for display
            var serverInfo = await client.ListDatabaseNamesAsync();
            var databases = await serverInfo.ToListAsync();

            return Ok(new TestConnectionResponse
            {
                Success = true,
                Message = "Connection successful!",
                ServerInfo = new ServerInfo
                {
                    DatabaseCount = databases.Count,
                    AvailableDatabases = databases.Take(5).ToList()
                }
            });
        }
        catch (MongoAuthenticationException)
        {
            return Ok(new TestConnectionResponse
            {
                Success = false,
                Message = "Authentication failed. Please check your username and password."
            });
        }
        catch (MongoConnectionException ex)
        {
            _logger.LogWarning(ex, "Connection test failed - connection error");
            return Ok(new TestConnectionResponse
            {
                Success = false,
                Message = "Could not connect to server. Please check the connection string and ensure the server is accessible."
            });
        }
        catch (TimeoutException)
        {
            return Ok(new TestConnectionResponse
            {
                Success = false,
                Message = "Connection timed out. Please check the server address and network connectivity."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed with unexpected error");
            return Ok(new TestConnectionResponse
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Save the database configuration and connect
    /// </summary>
    [HttpPost("configure")]
    [ProducesResponseType(typeof(ConfigureResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConfigureResponse>> Configure(
        [FromBody] ConfigureRequest request)
    {
        var currentConfig = await _userConfigService.GetConfigAsync();
        var currentProvider = currentConfig.Provider;
        var targetProvider = request.Provider?.ToLowerInvariant() == "mongodb"
            ? DatabaseProvider.MongoDb
            : DatabaseProvider.Local;

        if (targetProvider == DatabaseProvider.Local)
        {
            _logger.LogInformation("Configuring Local (LiteDB) provider");

            await _userConfigService.SaveConfigAsync(new UserConfig
            {
                Provider = DatabaseProvider.Local,
                // Preserve MongoDB settings in case user switches back
                MongoDbConnectionString = currentConfig.MongoDbConnectionString,
                MongoDbDatabaseName = currentConfig.MongoDbDatabaseName,
            });

            var restartRequired = currentProvider != DatabaseProvider.Local;
            return Ok(new ConfigureResponse
            {
                Success = true,
                Message = restartRequired
                    ? "Configuration saved. Restart required to apply provider change."
                    : "Local database configuration saved.",
                RestartRequired = restartRequired,
            });
        }

        // MongoDB provider
        if (string.IsNullOrEmpty(request.ConnectionString))
        {
            return BadRequest(new ConfigureResponse
            {
                Success = false,
                Message = "Connection string is required for MongoDB"
            });
        }

        var databaseName = request.DatabaseName ?? "Log4YM";
        _logger.LogInformation("Configuring MongoDB with database: {DatabaseName}", databaseName);

        // Always persist the config with the correct provider
        await _userConfigService.SaveConfigAsync(new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = request.ConnectionString,
            MongoDbDatabaseName = databaseName,
        });

        // If we're already on MongoDB, attempt a live reinitialize
        if (currentProvider == DatabaseProvider.MongoDb)
        {
            var success = await _dbContext.ReinitializeAsync(
                request.ConnectionString,
                databaseName);

            return Ok(new ConfigureResponse
            {
                Success = success,
                Message = success
                    ? "Configuration saved and connected successfully!"
                    : "Configuration saved but failed to connect. Please verify the connection string.",
                RestartRequired = false,
            });
        }

        // Switching from Local → MongoDB requires restart
        return Ok(new ConfigureResponse
        {
            Success = true,
            Message = "Configuration saved. Restart required to apply provider change.",
            RestartRequired = true,
        });
    }
}

// DTOs
public class SetupStatusResponse
{
    public bool IsConfigured { get; set; }
    public bool IsConnected { get; set; }
    public string? Provider { get; set; }
    public DateTime? ConfiguredAt { get; set; }
    public string? DatabaseName { get; set; }
}

public class TestConnectionRequest
{
    public string ConnectionString { get; set; } = null!;
    public string? DatabaseName { get; set; }
}

public class TestConnectionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public ServerInfo? ServerInfo { get; set; }
}

public class ServerInfo
{
    public int DatabaseCount { get; set; }
    public List<string> AvailableDatabases { get; set; } = new();
}

public class ConfigureRequest
{
    public string? Provider { get; set; }
    public string? ConnectionString { get; set; }
    public string? DatabaseName { get; set; }
}

public class ConfigureResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = null!;
    public bool RestartRequired { get; set; }
}
