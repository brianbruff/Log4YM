namespace Log4YM.Server.Core.Database;

public interface IDbContext
{
    bool IsConnected { get; }
    string? DatabaseName { get; }
    bool TryInitialize();
    Task<bool> ReinitializeAsync(string connectionString, string databaseName);
}
