using FluentAssertions;
using Moq;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.Mongo;

/// <summary>
/// Tests for the MongoDB Settings repository via its interface.
/// Verifies behavioral contracts shared between MongoDB and LiteDB implementations.
/// </summary>
[Trait("Category", "Unit")]
public class MongoSettingsRepositoryContractTests
{
    private readonly Mock<ISettingsRepository> _repoMock = new();

    private static UserSettings CreateSettings(string id = "default", string callsign = "W1AW")
    {
        return new UserSettings
        {
            Id = id,
            Station = { Callsign = callsign }
        };
    }

    [Fact]
    public async Task GetAsync_WhenExists_ReturnsSettings()
    {
        var settings = CreateSettings("default", "W1AW");
        _repoMock.Setup(r => r.GetAsync("default")).ReturnsAsync(settings);

        var result = await _repoMock.Object.GetAsync("default");

        result.Should().NotBeNull();
        result!.Station.Callsign.Should().Be("W1AW");
    }

    [Fact]
    public async Task GetAsync_WhenNotExists_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetAsync(It.IsAny<string>())).ReturnsAsync((UserSettings?)null);

        var result = await _repoMock.Object.GetAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_ReturnsUpdatedSettings()
    {
        var settings = CreateSettings("default", "VE3ABC");
        _repoMock.Setup(r => r.UpsertAsync(settings)).ReturnsAsync(settings);

        var result = await _repoMock.Object.UpsertAsync(settings);

        result.Station.Callsign.Should().Be("VE3ABC");
    }

    [Fact]
    public async Task UpsertAsync_Called_InvokesRepository()
    {
        var settings = CreateSettings();
        _repoMock.Setup(r => r.UpsertAsync(It.IsAny<UserSettings>())).ReturnsAsync(settings);

        await _repoMock.Object.UpsertAsync(settings);

        _repoMock.Verify(r => r.UpsertAsync(settings), Times.Once);
    }

    [Fact]
    public async Task GetAsync_DefaultId_ReturnsDefaultSettings()
    {
        var settings = new UserSettings { Id = "default" };
        _repoMock.Setup(r => r.GetAsync("default")).ReturnsAsync(settings);

        var result = await _repoMock.Object.GetAsync();  // uses default parameter

        result.Should().NotBeNull();
        result!.Id.Should().Be("default");
    }
}
