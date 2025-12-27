using Serilog;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Core.Events;
using Log4YM.Server.Services;
using Log4YM.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Listen on all interfaces for development (allows access via IP)
// Use HTTPS to ensure secure context for WebGL features like globe.gl
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000", "https://0.0.0.0:5001");
}

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

// Add SignalR
builder.Services.AddSignalR();

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

// Register MongoDB context
builder.Services.AddSingleton<MongoDbContext>();

// Register repositories
builder.Services.AddScoped<IQsoRepository, QsoRepository>();
builder.Services.AddScoped<ISpotRepository, SpotRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();

// Register services
builder.Services.AddScoped<IQsoService, QsoService>();
builder.Services.AddScoped<ISpotService, SpotService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IQrzService, QrzService>();
builder.Services.AddScoped<IAdifService, AdifService>();

// Register HTTP client for external APIs
builder.Services.AddHttpClient("QRZ", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "Log4YM/1.0");
});

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
builder.Services.AddHostedService<DxClusterService>();

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
