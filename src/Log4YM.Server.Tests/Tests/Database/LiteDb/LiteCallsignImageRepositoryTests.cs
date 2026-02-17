using FluentAssertions;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Tests.Fixtures;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.LiteDb;

[Trait("Category", "Integration")]
public class LiteCallsignImageRepositoryTests : IDisposable
{
    private readonly LiteDbTestFixture _fixture;
    private readonly LiteCallsignImageRepository _repo;

    public LiteCallsignImageRepositoryTests()
    {
        _fixture = new LiteDbTestFixture();
        _repo = new LiteCallsignImageRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private static CallsignMapImage CreateImage(
        string callsign = "W1AW",
        string? imageUrl = "https://example.com/image.jpg",
        double latitude = 41.7,
        double longitude = -72.7)
    {
        return new CallsignMapImage
        {
            Callsign = callsign,
            ImageUrl = imageUrl,
            Latitude = latitude,
            Longitude = longitude,
            Name = "Test Station",
            Country = "United States",
            Grid = "FN31"
        };
    }

    // =========================================================================
    // GetRecentAsync
    // =========================================================================

    [Fact]
    public async Task GetRecentAsync_Empty_ReturnsEmptyList()
    {
        var result = await _repo.GetRecentAsync(10);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentAsync_RespectsLimit()
    {
        await _repo.UpsertAsync(CreateImage("W1AW"));
        await _repo.UpsertAsync(CreateImage("VE3ABC"));
        await _repo.UpsertAsync(CreateImage("K1TTT"));

        var result = await _repo.GetRecentAsync(2);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentAsync_OrdersByNewestFirst()
    {
        await _repo.UpsertAsync(CreateImage("W1AW"));
        await Task.Delay(10); // ensure different timestamps
        await _repo.UpsertAsync(CreateImage("VE3ABC"));

        var result = await _repo.GetRecentAsync(10);
        result.Should().HaveCount(2);
        result[0].Callsign.Should().Be("VE3ABC"); // newest first
    }

    // =========================================================================
    // UpsertAsync
    // =========================================================================

    [Fact]
    public async Task UpsertAsync_InsertsNewImage()
    {
        var image = CreateImage("W1AW");
        await _repo.UpsertAsync(image);

        var result = await _repo.GetRecentAsync(10);
        result.Should().HaveCount(1);
        result[0].Callsign.Should().Be("W1AW");
    }

    [Fact]
    public async Task UpsertAsync_SetsOrUpdatesSavedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var image = CreateImage("W1AW");
        await _repo.UpsertAsync(image);

        var result = await _repo.GetRecentAsync(1);
        result[0].SavedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingByCallsign()
    {
        var image = CreateImage("W1AW", latitude: 41.7, longitude: -72.7);
        await _repo.UpsertAsync(image);

        var updated = CreateImage("W1AW", latitude: 50.0, longitude: -100.0);
        await _repo.UpsertAsync(updated);

        var result = await _repo.GetRecentAsync(10);
        result.Should().HaveCount(1); // No duplicates
        result[0].Latitude.Should().BeApproximately(50.0, 0.001);
        result[0].Longitude.Should().BeApproximately(-100.0, 0.001);
    }

    [Fact]
    public async Task UpsertAsync_MultipleCallsigns_StoresAllSeparately()
    {
        await _repo.UpsertAsync(CreateImage("W1AW"));
        await _repo.UpsertAsync(CreateImage("VE3ABC"));
        await _repo.UpsertAsync(CreateImage("K1TTT"));

        var result = await _repo.GetRecentAsync(10);
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task UpsertAsync_PreservesExistingId_OnUpdate()
    {
        var image = CreateImage("W1AW");
        await _repo.UpsertAsync(image);

        var first = (await _repo.GetRecentAsync(1))[0];
        first.Id.Should().NotBeNullOrEmpty();

        var updated = CreateImage("W1AW", latitude: 99.0);
        await _repo.UpsertAsync(updated);

        var after = (await _repo.GetRecentAsync(1))[0];
        after.Id.Should().Be(first.Id); // Same document updated, not a new insert
    }
}
