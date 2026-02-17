using Moq;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Services;

namespace Log4YM.Server.Tests.Fixtures;

/// <summary>
/// Creates an isolated LiteDB context backed by a unique temp directory.
/// Each test instance gets its own directory so the "log4ym.db" file doesn't collide.
/// LiteDbContext uses GetDirectoryName(configPath) + "log4ym.db" as the actual DB path,
/// so we point the config path to a unique file inside a unique temp directory.
/// </summary>
public class LiteDbTestFixture : IDisposable
{
    public LiteDbContext Context { get; }
    private readonly string _testDir;
    // The actual db file LiteDbContext will create
    private readonly string _actualDbPath;

    public LiteDbTestFixture()
    {
        // Create a unique directory per test instance
        _testDir = Path.Combine(Path.GetTempPath(), $"log4ym_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);

        // The config path can be any file in this directory; LiteDbContext extracts the directory
        var configPath = Path.Combine(_testDir, "config.json");
        _actualDbPath = Path.Combine(_testDir, "log4ym.db");

        var userConfigMock = new Mock<IUserConfigService>();
        userConfigMock.Setup(s => s.GetConfigPath()).Returns(configPath);
        userConfigMock.Setup(s => s.SaveConfigAsync(It.IsAny<UserConfig>()))
            .Returns(Task.CompletedTask);

        Context = new LiteDbContext(userConfigMock.Object);
    }

    public void Dispose()
    {
        Context.Dispose();

        // Remove the entire temp directory
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }
}
