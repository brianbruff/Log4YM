using Serilog;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Core.Events;
using Log4YM.Server.Services;
using Log4YM.Server.Hubs;

var builder = WebApplication.CreateBuilder(args);

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

// Register event bus
builder.Services.AddSingleton<IEventBus, EventBus>();

// Register Antenna Genius service
builder.Services.AddSingleton<AntennaGeniusService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AntennaGeniusService>());

// Register PGXL Amplifier service
builder.Services.AddSingleton<PgxlService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<PgxlService>());

// Register SmartUnlink service
builder.Services.AddSingleton<SmartUnlinkService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SmartUnlinkService>());

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
