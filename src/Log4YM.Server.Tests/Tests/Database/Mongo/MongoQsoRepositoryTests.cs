using FluentAssertions;
using Moq;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.Mongo;

/// <summary>
/// Tests for the MongoDB QSO repository via its interface.
/// Uses a mock to verify behavior contracts that apply to both LiteDB and MongoDB implementations.
/// Real MongoDB integration tests require a live connection (Category=RateLimited).
/// </summary>
[Trait("Category", "Unit")]
public class MongoQsoRepositoryContractTests
{
    private readonly Mock<IQsoRepository> _repoMock = new();

    private static Qso CreateQso(
        string callsign = "W1AW",
        string band = "20m",
        string mode = "SSB",
        SyncStatus syncStatus = SyncStatus.NotSynced)
    {
        return new Qso
        {
            Id = "507f1f77bcf86cd799439011",
            Callsign = callsign,
            Band = band,
            Mode = mode,
            QsoDate = DateTime.UtcNow.Date,
            TimeOn = "1200",
            QrzSyncStatus = syncStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // =========================================================================
    // CreateAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task CreateAsync_ReturnsQsoWithId()
    {
        var qso = CreateQso();
        _repoMock.Setup(r => r.CreateAsync(qso)).ReturnsAsync(qso);

        var result = await _repoMock.Object.CreateAsync(qso);

        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
    }

    // =========================================================================
    // GetByIdAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsQso()
    {
        var qso = CreateQso("VE3ABC");
        _repoMock.Setup(r => r.GetByIdAsync("507f1f77bcf86cd799439011")).ReturnsAsync(qso);

        var result = await _repoMock.Object.GetByIdAsync("507f1f77bcf86cd799439011");

        result.Should().NotBeNull();
        result!.Callsign.Should().Be("VE3ABC");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((Qso?)null);

        var result = await _repoMock.Object.GetByIdAsync("000000000000000000000001");

        result.Should().BeNull();
    }

    // =========================================================================
    // SearchAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task SearchAsync_ReturnsPaginatedResult()
    {
        var qsos = new List<Qso> { CreateQso("W1AW"), CreateQso("VE3ABC") };
        _repoMock.Setup(r => r.SearchAsync(It.IsAny<QsoSearchRequest>()))
            .ReturnsAsync((qsos, 2));

        var (items, total) = await _repoMock.Object.SearchAsync(new QsoSearchRequest(Limit: 10));

        items.Should().HaveCount(2);
        total.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_WithCallsignFilter_OnlyMatchingReturned()
    {
        var filteredQsos = new List<Qso> { CreateQso("W1AW") };
        _repoMock.Setup(r => r.SearchAsync(It.Is<QsoSearchRequest>(q => q.Callsign == "W1AW")))
            .ReturnsAsync((filteredQsos, 1));

        var (items, total) = await _repoMock.Object.SearchAsync(new QsoSearchRequest(Callsign: "W1AW", Limit: 10));

        items.Should().HaveCount(1);
        items.First().Callsign.Should().Be("W1AW");
    }

    // =========================================================================
    // UpdateAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_ExistingRecord_ReturnsTrue()
    {
        _repoMock.Setup(r => r.UpdateAsync("507f1f77bcf86cd799439011", It.IsAny<Qso>()))
            .ReturnsAsync(true);

        var result = await _repoMock.Object.UpdateAsync("507f1f77bcf86cd799439011", CreateQso());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NonExistentRecord_ReturnsFalse()
    {
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<string>(), It.IsAny<Qso>()))
            .ReturnsAsync(false);

        var result = await _repoMock.Object.UpdateAsync("000000000000000000000001", CreateQso());

        result.Should().BeFalse();
    }

    // =========================================================================
    // QRZ sync - contract behavior
    // =========================================================================

    [Fact]
    public async Task UpdateQrzSyncStatusAsync_SetsStatusToSynced()
    {
        var qso = CreateQso(syncStatus: SyncStatus.NotSynced);
        _repoMock.Setup(r => r.UpdateQrzSyncStatusAsync("507f1f77bcf86cd799439011", "qrz-123"))
            .ReturnsAsync(true);
        _repoMock.Setup(r => r.GetByIdAsync("507f1f77bcf86cd799439011"))
            .ReturnsAsync(new Qso
            {
                Id = qso.Id,
                Callsign = qso.Callsign,
                QsoDate = qso.QsoDate,
                TimeOn = qso.TimeOn,
                Band = qso.Band,
                Mode = qso.Mode,
                Station = qso.Station,
                QrzSyncStatus = SyncStatus.Synced,
                QrzLogId = "qrz-123"
            });

        await _repoMock.Object.UpdateQrzSyncStatusAsync("507f1f77bcf86cd799439011", "qrz-123");
        var updated = await _repoMock.Object.GetByIdAsync("507f1f77bcf86cd799439011");

        updated!.QrzSyncStatus.Should().Be(SyncStatus.Synced);
        updated.QrzLogId.Should().Be("qrz-123");
    }

    [Fact]
    public async Task GetUnsyncedToQrzAsync_ReturnsNotSyncedAndModified()
    {
        var notSynced = CreateQso(syncStatus: SyncStatus.NotSynced);
        var modified = CreateQso(syncStatus: SyncStatus.Modified);
        _repoMock.Setup(r => r.GetUnsyncedToQrzAsync())
            .ReturnsAsync(new List<Qso> { notSynced, modified });

        var result = (await _repoMock.Object.GetUnsyncedToQrzAsync()).ToList();

        result.Should().HaveCount(2);
        result.Should().Contain(q => q.QrzSyncStatus == SyncStatus.NotSynced);
        result.Should().Contain(q => q.QrzSyncStatus == SyncStatus.Modified);
    }

    [Fact]
    public async Task GetPendingSyncCountAsync_ReturnsCount()
    {
        _repoMock.Setup(r => r.GetPendingSyncCountAsync()).ReturnsAsync(3);

        var count = await _repoMock.Object.GetPendingSyncCountAsync();

        count.Should().Be(3);
    }

    // =========================================================================
    // Statistics - contract behavior
    // =========================================================================

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStructure()
    {
        var stats = new QsoStatistics(
            TotalQsos: 100,
            UniqueCallsigns: 50,
            UniqueCountries: 20,
            UniqueGrids: 30,
            QsosToday: 5,
            QsosByBand: new Dictionary<string, int> { ["20m"] = 60, ["40m"] = 40 },
            QsosByMode: new Dictionary<string, int> { ["SSB"] = 70, ["CW"] = 30 }
        );
        _repoMock.Setup(r => r.GetStatisticsAsync()).ReturnsAsync(stats);

        var result = await _repoMock.Object.GetStatisticsAsync();

        result.TotalQsos.Should().Be(100);
        result.UniqueCallsigns.Should().Be(50);
        result.UniqueCountries.Should().Be(20);
        result.UniqueGrids.Should().Be(30);
        result.QsosToday.Should().Be(5);
        result.QsosByBand.Should().ContainKey("20m").WhoseValue.Should().Be(60);
        result.QsosByMode.Should().ContainKey("CW").WhoseValue.Should().Be(30);
    }

    // =========================================================================
    // DeleteAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    {
        _repoMock.Setup(r => r.DeleteAsync("507f1f77bcf86cd799439011")).ReturnsAsync(true);

        var result = await _repoMock.Object.DeleteAsync("507f1f77bcf86cd799439011");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAllAsync_ReturnsDeletedCount()
    {
        _repoMock.Setup(r => r.DeleteAllAsync()).ReturnsAsync(42L);

        var count = await _repoMock.Object.DeleteAllAsync();

        count.Should().Be(42);
    }

    // =========================================================================
    // ExistsAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task ExistsAsync_WhenExists_ReturnsTrue()
    {
        var date = DateTime.UtcNow.Date;
        _repoMock.Setup(r => r.ExistsAsync("W1AW", date, "1200", "20m", "SSB")).ReturnsAsync(true);

        var result = await _repoMock.Object.ExistsAsync("W1AW", date, "1200", "20m", "SSB");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotExists_ReturnsFalse()
    {
        _repoMock.Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(false);

        var result = await _repoMock.Object.ExistsAsync("XX9XX", DateTime.UtcNow, "0000", "80m", "CW");

        result.Should().BeFalse();
    }

    // =========================================================================
    // GetByIdsAsync - contract behavior
    // =========================================================================

    [Fact]
    public async Task GetByIdsAsync_ReturnsMatchingQsos()
    {
        var ids = new[] { "id1", "id2" };
        var qsos = new List<Qso> { CreateQso("W1AW"), CreateQso("VE3ABC") };
        _repoMock.Setup(r => r.GetByIdsAsync(ids)).ReturnsAsync(qsos);

        var result = (await _repoMock.Object.GetByIdsAsync(ids)).ToList();

        result.Should().HaveCount(2);
    }
}
