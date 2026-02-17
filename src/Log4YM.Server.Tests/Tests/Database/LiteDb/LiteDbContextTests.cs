using FluentAssertions;
using Moq;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Services;
using Xunit;
#pragma warning disable CS4014 // Because this call is not awaited

namespace Log4YM.Server.Tests.Tests.Database.LiteDb;

[Trait("Category", "Integration")]
public class LiteDbContextTests
{
    /// <summary>
    /// Creates an isolated context in a unique temp directory.
    /// LiteDbContext uses GetDirectoryName(configPath) + "log4ym.db" as the DB file,
    /// so each test must use a unique directory to avoid conflicts.
    /// </summary>
    private static (LiteDbContext ctx, string testDir, Mock<IUserConfigService> mock) CreateContext()
    {
        var testDir = Path.Combine(Path.GetTempPath(), $"litedb_ctx_{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDir);
        var configPath = Path.Combine(testDir, "config.json");

        var mock = new Mock<IUserConfigService>();
        mock.Setup(s => s.GetConfigPath()).Returns(configPath);
        mock.Setup(s => s.SaveConfigAsync(It.IsAny<UserConfig>())).Returns(Task.CompletedTask);

        var ctx = new LiteDbContext(mock.Object);
        return (ctx, testDir, mock);
    }

    private static void Cleanup(LiteDbContext ctx, string testDir)
    {
        ctx.Dispose();
        try { Directory.Delete(testDir, recursive: true); } catch { }
    }

    [Fact]
    public void Constructor_ValidPath_IsConnected()
    {
        var (ctx, testDir, _) = CreateContext();
        try
        {
            ctx.IsConnected.Should().BeTrue();
        }
        finally
        {
            Cleanup(ctx, testDir);
        }
    }

    [Fact]
    public void DatabaseName_ReturnsFileName()
    {
        var (ctx, testDir, _) = CreateContext();
        try
        {
            ctx.DatabaseName.Should().Be("log4ym.db");
        }
        finally
        {
            Cleanup(ctx, testDir);
        }
    }

    [Fact]
    public void TryInitialize_WhenAlreadyInitialized_ReturnsTrue()
    {
        var (ctx, testDir, _) = CreateContext();
        try
        {
            var result = ctx.TryInitialize();
            result.Should().BeTrue();
            ctx.IsConnected.Should().BeTrue();
        }
        finally
        {
            Cleanup(ctx, testDir);
        }
    }

    [Fact]
    public async Task ReinitializeAsync_SwitchesDatabase()
    {
        var (ctx, testDir, mock) = CreateContext();

        // Point to a new directory for reinitialization
        var newDir = Path.Combine(Path.GetTempPath(), $"litedb_new_{Guid.NewGuid():N}");
        Directory.CreateDirectory(newDir);
        mock.Setup(s => s.GetConfigPath()).Returns(Path.Combine(newDir, "config.json"));

        try
        {
            var result = await ctx.ReinitializeAsync("unused_connection_string", "test_db");
            result.Should().BeTrue();
            ctx.IsConnected.Should().BeTrue();
        }
        finally
        {
            Cleanup(ctx, testDir);
            try { Directory.Delete(newDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Dispose_SetsIsConnectedFalse()
    {
        var (ctx, testDir, _) = CreateContext();
        ctx.IsConnected.Should().BeTrue();
        Cleanup(ctx, testDir);
        ctx.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void AccessingRepository_AfterDispose_ThrowsInvalidOperation()
    {
        var (ctx, testDir, _) = CreateContext();
        var repo = new LiteQsoRepository(ctx);
        Cleanup(ctx, testDir);

        // After disposal, any repository operation should throw
        var act = async () => await repo.GetCountAsync();
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public void CreatesDirectory_IfNotExists()
    {
        var outerDir = Path.Combine(Path.GetTempPath(), $"log4ym_outer_{Guid.NewGuid():N}");
        var subDir = Path.Combine(outerDir, "nested");
        var configPath = Path.Combine(subDir, "config.json");

        var mock = new Mock<IUserConfigService>();
        mock.Setup(s => s.GetConfigPath()).Returns(configPath);
        mock.Setup(s => s.SaveConfigAsync(It.IsAny<UserConfig>())).Returns(Task.CompletedTask);

        LiteDbContext? ctx = null;
        try
        {
            ctx = new LiteDbContext(mock.Object);
            Directory.Exists(subDir).Should().BeTrue();
            ctx.IsConnected.Should().BeTrue();
        }
        finally
        {
            ctx?.Dispose();
            try { Directory.Delete(outerDir, recursive: true); } catch { }
        }
    }
}
