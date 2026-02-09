using Serilog;
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

// Register user config service (must be before MongoDbContext)
builder.Services.AddSingleton<IUserConfigService, UserConfigService>();

// Register MongoDB context (now supports lazy initialization)
builder.Services.AddSingleton<MongoDbContext>();

// Register repositories
builder.Services.AddScoped<IQsoRepository, QsoRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();

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
