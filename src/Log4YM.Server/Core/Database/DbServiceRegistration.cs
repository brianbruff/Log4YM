using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Services;

namespace Log4YM.Server.Core.Database;

public enum DatabaseProvider
{
    Local,
    MongoDb
}

public static class DbServiceRegistration
{
    public static IServiceCollection AddDatabase(
        this IServiceCollection services,
        UserConfig config)
    {
        switch (config.Provider)
        {
            case DatabaseProvider.Local:
                services.AddSingleton<LiteDbContext>();
                services.AddSingleton<IDbContext>(sp => sp.GetRequiredService<LiteDbContext>());
                services.AddScoped<IQsoRepository, LiteQsoRepository>();
                services.AddScoped<ISettingsRepository, LiteSettingsRepository>();
                services.AddScoped<ISmartUnlinkRepository, LiteSmartUnlinkRepository>();
                services.AddScoped<ICallsignImageRepository, LiteCallsignImageRepository>();
                break;

            case DatabaseProvider.MongoDb:
                services.AddSingleton<MongoDbContext>();
                services.AddSingleton<IDbContext>(sp => sp.GetRequiredService<MongoDbContext>());
                services.AddScoped<IQsoRepository, QsoRepository>();
                services.AddScoped<ISettingsRepository, SettingsRepository>();
                services.AddScoped<ISmartUnlinkRepository, SmartUnlinkRepository>();
                services.AddScoped<ICallsignImageRepository, CallsignImageRepository>();
                break;
        }

        return services;
    }
}
