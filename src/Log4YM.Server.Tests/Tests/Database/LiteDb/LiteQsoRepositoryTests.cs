using FluentAssertions;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Tests.Fixtures;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.LiteDb;

[Trait("Category", "Integration")]
public class LiteQsoRepositoryTests : IDisposable
{
    private readonly LiteDbTestFixture _fixture;
    private readonly LiteQsoRepository _repo;

    public LiteQsoRepositoryTests()
    {
        _fixture = new LiteDbTestFixture();
        _repo = new LiteQsoRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private static Qso CreateQso(
        string callsign = "W1AW",
        string band = "20m",
        string mode = "SSB",
        DateTime? qsoDate = null,
        string timeOn = "1200",
        int? dxcc = null,
        string? grid = null,
        string? name = null,
        SyncStatus syncStatus = SyncStatus.NotSynced)
    {
        return new Qso
        {
            Callsign = callsign,
            Band = band,
            Mode = mode,
            QsoDate = qsoDate?.Date ?? DateTime.UtcNow.Date,
            TimeOn = timeOn,
            Dxcc = dxcc,
            Grid = grid,
            Name = name,
            QrzSyncStatus = syncStatus
        };
    }

    // =========================================================================
    // CreateAsync
    // =========================================================================

    [Fact]
    public async Task CreateAsync_GeneratesId_WhenNotProvided()
    {
        var qso = CreateQso();
        var created = await _repo.CreateAsync(qso);
        created.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_UsesProvidedId_WhenSet()
    {
        var qso = CreateQso();
        qso.Id = "507f1f77bcf86cd799439011";
        var created = await _repo.CreateAsync(qso);
        created.Id.Should().Be("507f1f77bcf86cd799439011");
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedAtAndUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var qso = CreateQso();
        var created = await _repo.CreateAsync(qso);
        created.CreatedAt.Should().BeAfter(before);
        created.UpdatedAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task CreateAsync_CanRetrieveById()
    {
        var qso = CreateQso("VE3ABC");
        var created = await _repo.CreateAsync(qso);
        var fetched = await _repo.GetByIdAsync(created.Id);
        fetched.Should().NotBeNull();
        fetched!.Callsign.Should().Be("VE3ABC");
    }

    // =========================================================================
    // GetByIdAsync
    // =========================================================================

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync("000000000000000000000001");
        result.Should().BeNull();
    }

    // =========================================================================
    // GetRecentAsync
    // =========================================================================

    [Fact]
    public async Task GetRecentAsync_ReturnsNewestFirst()
    {
        var older = CreateQso(qsoDate: DateTime.UtcNow.AddDays(-2), timeOn: "0900");
        var newer = CreateQso(qsoDate: DateTime.UtcNow.AddDays(-1), timeOn: "1200");
        await _repo.CreateAsync(older);
        await _repo.CreateAsync(newer);

        var results = (await _repo.GetRecentAsync(10)).ToList();
        results.Should().HaveCountGreaterOrEqualTo(2);
        results[0].QsoDate.Should().BeOnOrAfter(results[1].QsoDate);
    }

    [Fact]
    public async Task GetRecentAsync_RespectsLimit()
    {
        for (int i = 0; i < 5; i++)
            await _repo.CreateAsync(CreateQso(callsign: $"W{i}AW"));

        var results = await _repo.GetRecentAsync(3);
        results.Should().HaveCount(3);
    }

    // =========================================================================
    // UpdateAsync
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var qso = await _repo.CreateAsync(CreateQso("W1AW", "20m", "SSB"));
        var updated = new Qso
        {
            Id = qso.Id,
            Callsign = "W1AW",
            Band = "40m",
            Mode = "CW",
            QsoDate = qso.QsoDate,
            TimeOn = qso.TimeOn,
            QrzSyncStatus = SyncStatus.NotSynced
        };

        var success = await _repo.UpdateAsync(qso.Id, updated);
        success.Should().BeTrue();

        var fetched = await _repo.GetByIdAsync(qso.Id);
        fetched!.Band.Should().Be("40m");
        fetched.Mode.Should().Be("CW");
    }

    [Fact]
    public async Task UpdateAsync_SyncedQso_MarksAsModified()
    {
        var qso = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));
        // Mark synced
        await _repo.UpdateQrzSyncStatusAsync(qso.Id, "qrz-log-123");

        var synced = await _repo.GetByIdAsync(qso.Id);
        synced!.QrzSyncStatus.Should().Be(SyncStatus.Synced);

        // Now update - should become Modified
        var updatedQso = new Qso
        {
            Id = qso.Id,
            Callsign = synced.Callsign,
            Band = "40m",
            Mode = synced.Mode,
            QsoDate = synced.QsoDate,
            TimeOn = synced.TimeOn,
            QrzSyncStatus = SyncStatus.Synced  // was synced, update should set Modified
        };
        await _repo.UpdateAsync(qso.Id, updatedQso);

        var result = await _repo.GetByIdAsync(qso.Id);
        result!.QrzSyncStatus.Should().Be(SyncStatus.Modified);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _repo.UpdateAsync("000000000000000000000001", CreateQso());
        result.Should().BeFalse();
    }

    // =========================================================================
    // DeleteAsync
    // =========================================================================

    [Fact]
    public async Task DeleteAsync_RemovesRecord()
    {
        var qso = await _repo.CreateAsync(CreateQso());
        var deleted = await _repo.DeleteAsync(qso.Id);
        deleted.Should().BeTrue();

        var fetched = await _repo.GetByIdAsync(qso.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _repo.DeleteAsync("000000000000000000000001");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAllAsync_RemovesAllRecords()
    {
        await _repo.CreateAsync(CreateQso("W1AW"));
        await _repo.CreateAsync(CreateQso("VE3ABC"));

        var deleted = await _repo.DeleteAllAsync();
        deleted.Should().Be(2);

        var count = await _repo.GetCountAsync();
        count.Should().Be(0);
    }

    // =========================================================================
    // SearchAsync
    // =========================================================================

    [Fact]
    public async Task SearchAsync_ByCallsign_FiltersCorrectly()
    {
        await _repo.CreateAsync(CreateQso("W1AW"));
        await _repo.CreateAsync(CreateQso("VE3ABC"));

        var (items, total) = await _repo.SearchAsync(new QsoSearchRequest(Callsign: "W1AW", Limit: 10));
        items.All(q => q.Callsign.Contains("W1AW", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_ByCallsign_CaseInsensitive()
    {
        await _repo.CreateAsync(CreateQso("W1AW"));

        var (items, _) = await _repo.SearchAsync(new QsoSearchRequest(Callsign: "w1aw", Limit: 10));
        items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_ByBand_FiltersCorrectly()
    {
        await _repo.CreateAsync(CreateQso(band: "20m"));
        await _repo.CreateAsync(CreateQso(band: "40m"));

        var (items, total) = await _repo.SearchAsync(new QsoSearchRequest(Band: "20m", Limit: 10));
        items.All(q => q.Band == "20m").Should().BeTrue();
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_ByMode_FiltersCorrectly()
    {
        await _repo.CreateAsync(CreateQso(mode: "CW"));
        await _repo.CreateAsync(CreateQso(mode: "SSB"));

        var (items, total) = await _repo.SearchAsync(new QsoSearchRequest(Mode: "CW", Limit: 10));
        items.All(q => q.Mode == "CW").Should().BeTrue();
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_ByDateRange_FiltersCorrectly()
    {
        var targetDate = DateTime.UtcNow.AddDays(-10).Date;
        var outside = DateTime.UtcNow.AddDays(-30).Date;

        await _repo.CreateAsync(CreateQso(qsoDate: targetDate));
        await _repo.CreateAsync(CreateQso(qsoDate: outside));

        var from = targetDate.AddDays(-1);
        var to = targetDate.AddDays(1);
        var (items, total) = await _repo.SearchAsync(new QsoSearchRequest(FromDate: from, ToDate: to, Limit: 10));
        items.All(q => q.QsoDate >= from && q.QsoDate <= to.AddDays(1)).Should().BeTrue();
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_ByDxcc_FiltersCorrectly()
    {
        await _repo.CreateAsync(CreateQso(dxcc: 291));
        await _repo.CreateAsync(CreateQso(dxcc: 1));

        var (items, total) = await _repo.SearchAsync(new QsoSearchRequest(Dxcc: 291, Limit: 10));
        items.All(q => q.Dxcc == 291).Should().BeTrue();
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_ByName_FiltersCorrectly()
    {
        await _repo.CreateAsync(CreateQso(name: "John Smith"));
        await _repo.CreateAsync(CreateQso(name: "Jane Doe"));

        var (items, _) = await _repo.SearchAsync(new QsoSearchRequest(Name: "john", Limit: 10));
        items.Should().NotBeEmpty();
        items.All(q => q.Name != null && q.Name.Contains("john", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task SearchAsync_MultipleCriteria_CombinesFilters()
    {
        await _repo.CreateAsync(CreateQso("W1AW", "20m", "CW"));
        await _repo.CreateAsync(CreateQso("W1AW", "40m", "SSB"));
        await _repo.CreateAsync(CreateQso("VE3ABC", "20m", "CW"));

        var (items, total) = await _repo.SearchAsync(
            new QsoSearchRequest(Callsign: "W1AW", Band: "20m", Mode: "CW", Limit: 10));

        items.Should().HaveCountGreaterThan(0);
        items.All(q => q.Callsign.Contains("W1AW", StringComparison.OrdinalIgnoreCase)
            && q.Band == "20m" && q.Mode == "CW").Should().BeTrue();
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task SearchAsync_NoFilter_ReturnsPaginated()
    {
        for (int i = 0; i < 10; i++)
            await _repo.CreateAsync(CreateQso(callsign: $"W{i}AA"));

        var (items, total) = await _repo.SearchAsync(new QsoSearchRequest(Limit: 3, Skip: 0));
        items.Should().HaveCount(3);
        total.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public async Task SearchAsync_Skip_PaginatesCorrectly()
    {
        for (int i = 0; i < 5; i++)
            await _repo.CreateAsync(CreateQso(callsign: $"K{i}AW"));

        var (page1, _) = await _repo.SearchAsync(new QsoSearchRequest(Limit: 2, Skip: 0));
        var (page2, _) = await _repo.SearchAsync(new QsoSearchRequest(Limit: 2, Skip: 2));

        var p1Ids = page1.Select(q => q.Id).ToHashSet();
        var p2Ids = page2.Select(q => q.Id).ToHashSet();
        p1Ids.Intersect(p2Ids).Should().BeEmpty();
    }

    // =========================================================================
    // ExistsAsync
    // =========================================================================

    [Fact]
    public async Task ExistsAsync_ExistingRecord_ReturnsTrue()
    {
        var date = DateTime.UtcNow.Date;
        await _repo.CreateAsync(CreateQso("W1AW", "20m", "SSB", date, "1200"));

        var exists = await _repo.ExistsAsync("W1AW", date, "1200", "20m", "SSB");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingRecord_ReturnsFalse()
    {
        var date = DateTime.UtcNow.Date;
        var exists = await _repo.ExistsAsync("XX9XX", date, "0000", "80m", "CW");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_CaseInsensitiveCallsign()
    {
        var date = DateTime.UtcNow.Date;
        await _repo.CreateAsync(CreateQso("W1AW", "20m", "SSB", date, "1300"));

        var exists = await _repo.ExistsAsync("w1aw", date, "1300", "20m", "SSB");
        exists.Should().BeTrue();
    }

    // =========================================================================
    // GetByIdsAsync
    // =========================================================================

    [Fact]
    public async Task GetByIdsAsync_ReturnsOnlyMatchingIds()
    {
        var q1 = await _repo.CreateAsync(CreateQso("W1AW"));
        var q2 = await _repo.CreateAsync(CreateQso("VE3ABC"));
        var _ = await _repo.CreateAsync(CreateQso("K1ABC"));

        var results = (await _repo.GetByIdsAsync(new[] { q1.Id, q2.Id })).ToList();
        results.Should().HaveCount(2);
        results.Select(q => q.Id).Should().Contain(q1.Id);
        results.Select(q => q.Id).Should().Contain(q2.Id);
    }

    // =========================================================================
    // QRZ Sync Workflow
    // =========================================================================

    [Fact]
    public async Task UpdateQrzSyncStatusAsync_SetsSyncedStatus()
    {
        var qso = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));

        var success = await _repo.UpdateQrzSyncStatusAsync(qso.Id, "qrz-12345");
        success.Should().BeTrue();

        var updated = await _repo.GetByIdAsync(qso.Id);
        updated!.QrzSyncStatus.Should().Be(SyncStatus.Synced);
        updated.QrzLogId.Should().Be("qrz-12345");
        updated.QrzSyncedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateQrzSyncStatusAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _repo.UpdateQrzSyncStatusAsync("000000000000000000000001", "qrz-xxx");
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetUnsyncedToQrzAsync_ReturnsNotSyncedAndModified()
    {
        var notSynced = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));
        var synced = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));
        await _repo.UpdateQrzSyncStatusAsync(synced.Id, "qrz-123");  // mark synced

        // Make another one Modified
        var modified = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));
        await _repo.UpdateQrzSyncStatusAsync(modified.Id, "qrz-456");  // sync first
        var modifiedQso = (await _repo.GetByIdAsync(modified.Id))!;
        var updatedModified = new Qso
        {
            Id = modifiedQso.Id,
            Callsign = modifiedQso.Callsign,
            Band = "40m",
            Mode = modifiedQso.Mode,
            QsoDate = modifiedQso.QsoDate,
            TimeOn = modifiedQso.TimeOn,
            QrzSyncStatus = SyncStatus.Synced  // triggers Modified
        };
        await _repo.UpdateAsync(modified.Id, updatedModified);

        var unsynced = (await _repo.GetUnsyncedToQrzAsync()).ToList();
        unsynced.Select(q => q.Id).Should().Contain(notSynced.Id);
        unsynced.Select(q => q.Id).Should().Contain(modified.Id);
        unsynced.Select(q => q.Id).Should().NotContain(synced.Id);
    }

    [Fact]
    public async Task GetPendingSyncCountAsync_CountsNotSyncedAndModified()
    {
        // Clear any existing data
        await _repo.DeleteAllAsync();

        var q1 = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));
        var q2 = await _repo.CreateAsync(CreateQso(syncStatus: SyncStatus.NotSynced));
        await _repo.UpdateQrzSyncStatusAsync(q2.Id, "qrz-99");  // synced - should not count

        var count = await _repo.GetPendingSyncCountAsync();
        count.Should().Be(1);
    }

    // =========================================================================
    // GetStatisticsAsync
    // =========================================================================

    [Fact]
    public async Task GetStatisticsAsync_EmptyDb_ReturnsZeros()
    {
        await _repo.DeleteAllAsync();
        var stats = await _repo.GetStatisticsAsync();
        stats.TotalQsos.Should().Be(0);
        stats.UniqueCallsigns.Should().Be(0);
        stats.UniqueCountries.Should().Be(0);
        stats.UniqueGrids.Should().Be(0);
        stats.QsosToday.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_CountsQsosToday()
    {
        await _repo.DeleteAllAsync();
        var today = DateTime.UtcNow.Date;
        await _repo.CreateAsync(CreateQso(qsoDate: today));
        await _repo.CreateAsync(CreateQso(qsoDate: today.AddDays(-1)));

        var stats = await _repo.GetStatisticsAsync();
        stats.TotalQsos.Should().Be(2);
        stats.QsosToday.Should().Be(1);
    }

    [Fact]
    public async Task GetStatisticsAsync_ComputesUniqueCallsigns()
    {
        await _repo.DeleteAllAsync();
        await _repo.CreateAsync(CreateQso("W1AW"));
        await _repo.CreateAsync(CreateQso("W1AW")); // duplicate
        await _repo.CreateAsync(CreateQso("VE3ABC"));

        var stats = await _repo.GetStatisticsAsync();
        stats.UniqueCallsigns.Should().Be(2);
        stats.TotalQsos.Should().Be(3);
    }

    [Fact]
    public async Task GetStatisticsAsync_ComputesQsosByBandAndMode()
    {
        await _repo.DeleteAllAsync();
        await _repo.CreateAsync(CreateQso(band: "20m", mode: "SSB"));
        await _repo.CreateAsync(CreateQso(band: "20m", mode: "CW"));
        await _repo.CreateAsync(CreateQso(band: "40m", mode: "CW"));

        var stats = await _repo.GetStatisticsAsync();
        stats.QsosByBand.Should().ContainKey("20m").WhoseValue.Should().Be(2);
        stats.QsosByBand.Should().ContainKey("40m").WhoseValue.Should().Be(1);
        stats.QsosByMode.Should().ContainKey("SSB").WhoseValue.Should().Be(1);
        stats.QsosByMode.Should().ContainKey("CW").WhoseValue.Should().Be(2);
    }

    [Fact]
    public async Task GetStatisticsAsync_ComputesUniqueGrids()
    {
        await _repo.DeleteAllAsync();
        await _repo.CreateAsync(CreateQso(grid: "FN31"));
        await _repo.CreateAsync(CreateQso(grid: "FN31")); // duplicate
        await _repo.CreateAsync(CreateQso(grid: "EN90"));
        await _repo.CreateAsync(CreateQso()); // no grid

        var stats = await _repo.GetStatisticsAsync();
        stats.UniqueGrids.Should().Be(2);
    }

    [Fact]
    public async Task GetStatisticsAsync_ComputesUniqueCountries()
    {
        await _repo.DeleteAllAsync();
        await _repo.CreateAsync(CreateQso(dxcc: 291));
        await _repo.CreateAsync(CreateQso(dxcc: 291)); // duplicate
        await _repo.CreateAsync(CreateQso(dxcc: 1));
        await _repo.CreateAsync(CreateQso()); // no dxcc

        var stats = await _repo.GetStatisticsAsync();
        stats.UniqueCountries.Should().Be(2);
    }

    // =========================================================================
    // GetCountAsync
    // =========================================================================

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        await _repo.DeleteAllAsync();
        await _repo.CreateAsync(CreateQso());
        await _repo.CreateAsync(CreateQso());
        var count = await _repo.GetCountAsync();
        count.Should().Be(2);
    }

    // =========================================================================
    // GetAllAsync
    // =========================================================================

    [Fact]
    public async Task GetAllAsync_ReturnsAllQsos()
    {
        await _repo.DeleteAllAsync();
        await _repo.CreateAsync(CreateQso("W1AW"));
        await _repo.CreateAsync(CreateQso("VE3ABC"));
        var all = (await _repo.GetAllAsync()).ToList();
        all.Should().HaveCount(2);
    }
}
