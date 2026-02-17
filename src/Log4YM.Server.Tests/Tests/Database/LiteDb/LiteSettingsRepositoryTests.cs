using FluentAssertions;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Tests.Fixtures;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.LiteDb;

[Trait("Category", "Integration")]
public class LiteSettingsRepositoryTests : IDisposable
{
    private readonly LiteDbTestFixture _fixture;
    private readonly LiteSettingsRepository _repo;

    public LiteSettingsRepositoryTests()
    {
        _fixture = new LiteDbTestFixture();
        _repo = new LiteSettingsRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task GetAsync_WhenEmpty_ReturnsNull()
    {
        var result = await _repo.GetAsync("default");
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewDocument()
    {
        var settings = new UserSettings { Id = "default" };
        settings.Station.Callsign = "W1AW";

        await _repo.UpsertAsync(settings);

        var result = await _repo.GetAsync("default");
        result.Should().NotBeNull();
        result!.Station.Callsign.Should().Be("W1AW");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingDocument()
    {
        var settings = new UserSettings { Id = "default" };
        settings.Station.Callsign = "W1AW";
        await _repo.UpsertAsync(settings);

        settings.Station.Callsign = "VE3ABC";
        await _repo.UpsertAsync(settings);

        var result = await _repo.GetAsync("default");
        result!.Station.Callsign.Should().Be("VE3ABC");
    }

    [Fact]
    public async Task UpsertAsync_SetsUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var settings = new UserSettings { Id = "default" };

        var upserted = await _repo.UpsertAsync(settings);

        upserted.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task UpsertAsync_SupportsCustomId()
    {
        var settings = new UserSettings { Id = "custom-id" };
        settings.Station.Callsign = "K1TTT";
        await _repo.UpsertAsync(settings);

        var result = await _repo.GetAsync("custom-id");
        result.Should().NotBeNull();
        result!.Station.Callsign.Should().Be("K1TTT");
    }

    [Fact]
    public async Task GetAsync_WhenIdNotFound_ReturnsNull()
    {
        var result = await _repo.GetAsync("nonexistent-id");
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_PreservesNestedSettings()
    {
        var settings = new UserSettings { Id = "default" };
        settings.Qrz.Username = "testuser";
        settings.Qrz.Enabled = true;
        settings.Appearance.Theme = "light";

        await _repo.UpsertAsync(settings);

        var result = await _repo.GetAsync("default");
        result!.Qrz.Username.Should().Be("testuser");
        result.Qrz.Enabled.Should().BeTrue();
        result.Appearance.Theme.Should().Be("light");
    }
}
