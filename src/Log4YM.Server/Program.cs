using Serilog;
using MongoDB.Driver;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Core.Events;
using Log4YM.Server.Services;
using Log4YM.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Note: URLs are controlled via ASPNETCORE_URLS environment variable
// When running from Electron, main.js sets this to http://localhost:{port}
// For standalone development, run with: ASPNETCORE_URLS=http://localhost:5050 dotnet run
// We don't use UseUrls() here as it would override the environment variable

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add controllers
builder.Services.AddControllers();
builder.Services.AddMemoryCache();

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Log4YM API", Version = "v1" });
});

// Add SignalR with JSON enum string serialization
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Resolve database provider: config.json > env vars > default (Local)
var userConfigService = new UserConfigService(
    LoggerFactory.Create(b => b.AddConsole()).CreateLogger<UserConfigService>());
UserConfig userConfig;
if (File.Exists(userConfigService.GetConfigPath()))
{
    userConfig = await userConfigService.GetConfigAsync();

    // Guard against stale config: if provider is MongoDb but no connection string
    // is saved (e.g. app was uninstalled and reinstalled, config.json persisted),
    // fall back to Local so the user isn't stuck waiting for a dead connection.
    if (userConfig.Provider == DatabaseProvider.MongoDb
        && string.IsNullOrEmpty(userConfig.MongoDbConnectionString))
    {
        Log.Warning("Config says MongoDb but no connection string found — falling back to Local provider");
        userConfig.Provider = DatabaseProvider.Local;
    }

    // Validate MongoDB is reachable before committing to it as the provider.
    // On macOS, uninstalling (drag to Trash) does not remove ~/Library/Application Support/,
    // so config.json may have a stale Atlas connection string from a previous installation.
    if (userConfig.Provider == DatabaseProvider.MongoDb
        && !string.IsNullOrEmpty(userConfig.MongoDbConnectionString))
    {
        try
        {
            var clientSettings = MongoClientSettings.FromConnectionString(userConfig.MongoDbConnectionString);
            clientSettings.ConnectTimeout = TimeSpan.FromSeconds(3);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
            clientSettings.SocketTimeout = TimeSpan.FromSeconds(3);

            var testClient = new MongoClient(clientSettings);
            var testDb = testClient.GetDatabase(userConfig.MongoDbDatabaseName ?? "log4ym");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            testDb.RunCommand<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("ping", 1),
                cancellationToken: cts.Token);

            Log.Information("MongoDB connection validated successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MongoDB not reachable at startup — falling back to Local provider. " +
                "You can reconfigure the database in Settings > Database");
            userConfig.Provider = DatabaseProvider.Local;
        }
    }
}
else if (!string.IsNullOrEmpty(builder.Configuration["MongoDB:ConnectionString"]))
{
    // Docker/env var scenario: MongoDB configured but no config.json yet
    userConfig = new UserConfig
    {
        Provider = DatabaseProvider.MongoDb,
        MongoDbConnectionString = builder.Configuration["MongoDB:ConnectionString"],
        MongoDbDatabaseName = builder.Configuration["MongoDB:DatabaseName"] ?? "log4ym"
    };
    // Seed the resolved config so GetConfigAsync/GetStatus return the correct provider
    await userConfigService.SaveConfigAsync(userConfig);
    Log.Information("No config.json found, using MongoDB from environment variables");
}
else
{
    userConfig = new UserConfig(); // defaults to Local
}

// Register the same instance used at startup so DI callers see the resolved config
builder.Services.AddSingleton<IUserConfigService>(userConfigService);

// Register database provider and repositories
builder.Services.AddDatabase(userConfig);

// Register services
builder.Services.AddScoped<IQsoService, QsoService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IQrzService, QrzService>();
builder.Services.AddScoped<IAdifService, AdifService>();
builder.Services.AddScoped<IAiService, AiService>();

// Register HTTP client for external APIs
builder.Services.AddHttpClient("QRZ", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Log4YM/1.0");
});

builder.Services.AddHttpClient("AI", client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Add("User-Agent", "Log4YM/1.0");
});

// Register default HTTP client factory for space weather and other APIs
builder.Services.AddHttpClient();

// Register Space Weather service (shared data source for controllers and propagation)
builder.Services.AddSingleton<ISpaceWeatherService, SpaceWeatherService>();

// Register Propagation service
builder.Services.AddSingleton<IPropagationService, PropagationService>();

// Register Contests service
builder.Services.AddSingleton<ContestsService>();

// Register DX News service
builder.Services.AddScoped<IDXNewsService, DXNewsService>();

// Register event bus
builder.Services.AddSingleton<IEventBus, EventBus>();

// Register Antenna Genius service
builder.Services.AddSingleton<AntennaGeniusService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AntennaGeniusService>());

// Register PGXL Amplifier service
builder.Services.AddSingleton<PgxlService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PgxlService>());

// Register FlexRadio CAT service
builder.Services.AddSingleton<FlexRadioService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FlexRadioService>());

// Register TCI Radio CAT service
builder.Services.AddSingleton<TciRadioService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TciRadioService>());

// Register Hamlib rigctld CAT service
builder.Services.AddSingleton<HamlibService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HamlibService>());

// Register SmartUnlink service
builder.Services.AddSingleton<SmartUnlinkService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SmartUnlinkService>());

// Register Rotator service (hamlib rotctld)
builder.Services.AddSingleton<RotatorService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RotatorService>());

// Register DX Cluster service
builder.Services.AddSingleton<DxClusterService>();
builder.Services.AddSingleton<IDxClusterService>(sp => sp.GetRequiredService<DxClusterService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DxClusterService>());

// Register RBN service
builder.Services.AddSingleton<RbnService>();
builder.Services.AddSingleton<IRbnService>(sp => sp.GetRequiredService<RbnService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<RbnService>());

// Register CW Keyer service
builder.Services.AddSingleton<CwKeyerService>();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// Serve static files (React build)
app.UseDefaultFiles();
app.UseStaticFiles();

// Map controllers
app.MapControllers();

// Map SignalR hub
app.MapHub<LogHub>("/hubs/log");

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

Log.Information("Log4YM Server starting on {Urls}", string.Join(", ", app.Urls));

app.Run();
