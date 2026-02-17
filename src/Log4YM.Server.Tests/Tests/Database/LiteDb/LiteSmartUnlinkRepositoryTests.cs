using FluentAssertions;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Services;
using Log4YM.Server.Tests.Fixtures;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.LiteDb;

[Trait("Category", "Integration")]
public class LiteSmartUnlinkRepositoryTests : IDisposable
{
    private readonly LiteDbTestFixture _fixture;
    private readonly LiteSmartUnlinkRepository _repo;

    public LiteSmartUnlinkRepositoryTests()
    {
        _fixture = new LiteDbTestFixture();
        _repo = new LiteSmartUnlinkRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private static SmartUnlinkRadioEntity CreateEntity(string? id = null, string name = "FlexRadio 6700", bool enabled = true)
    {
        return new SmartUnlinkRadioEntity
        {
            Id = id,
            Name = name,
            IpAddress = "192.168.1.100",
            Model = "FLEX-6700",
            SerialNumber = "SN12345",
            Callsign = "W1AW",
            Enabled = enabled,
            Version = "4.1.3.39644",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // =========================================================================
    // GetAllAsync
    // =========================================================================

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        var result = await _repo.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        await _repo.InsertAsync(CreateEntity(name: "Radio 1"));
        await _repo.InsertAsync(CreateEntity(name: "Radio 2"));

        var result = await _repo.GetAllAsync();
        result.Should().HaveCount(2);
    }

    // =========================================================================
    // InsertAsync
    // =========================================================================

    [Fact]
    public async Task InsertAsync_GeneratesId_WhenNotProvided()
    {
        var entity = CreateEntity(); // Id = null
        await _repo.InsertAsync(entity);

        var all = await _repo.GetAllAsync();
        all.Should().HaveCount(1);
        all[0].Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InsertAsync_UsesProvidedId()
    {
        var entity = CreateEntity(id: "507f1f77bcf86cd799439011");
        await _repo.InsertAsync(entity);

        var all = await _repo.GetAllAsync();
        all[0].Id.Should().Be("507f1f77bcf86cd799439011");
    }

    [Fact]
    public async Task InsertAsync_PersistsAllFields()
    {
        var entity = CreateEntity(name: "Test Radio");
        await _repo.InsertAsync(entity);

        var all = await _repo.GetAllAsync();
        var saved = all[0];
        saved.Name.Should().Be("Test Radio");
        saved.IpAddress.Should().Be("192.168.1.100");
        saved.Model.Should().Be("FLEX-6700");
        saved.SerialNumber.Should().Be("SN12345");
        saved.Callsign.Should().Be("W1AW");
        saved.Enabled.Should().BeTrue();
    }

    // =========================================================================
    // UpdateAsync
    // =========================================================================

    [Fact]
    public async Task UpdateAsync_UpdatesExistingEntity()
    {
        await _repo.InsertAsync(CreateEntity(name: "Original"));
        var all = await _repo.GetAllAsync();
        var inserted = all[0];

        var updated = inserted with { Name = "Updated", Callsign = "VE3ABC" };
        var success = await _repo.UpdateAsync(updated);

        success.Should().BeTrue();
        var after = await _repo.GetAllAsync();
        after[0].Name.Should().Be("Updated");
        after[0].Callsign.Should().Be("VE3ABC");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentEntity_ReturnsFalse()
    {
        var entity = CreateEntity(id: "000000000000000000000001");
        var result = await _repo.UpdateAsync(entity);
        result.Should().BeFalse();
    }

    // =========================================================================
    // SetEnabledAsync
    // =========================================================================

    [Fact]
    public async Task SetEnabledAsync_TogglesEnabled()
    {
        await _repo.InsertAsync(CreateEntity(enabled: true));
        var all = await _repo.GetAllAsync();
        var id = all[0].Id!;

        var success = await _repo.SetEnabledAsync(id, false);
        success.Should().BeTrue();

        var updated = await _repo.GetAllAsync();
        updated[0].Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabledAsync_SetsToEnabled()
    {
        await _repo.InsertAsync(CreateEntity(enabled: false));
        var all = await _repo.GetAllAsync();
        var id = all[0].Id!;

        await _repo.SetEnabledAsync(id, true);

        var updated = await _repo.GetAllAsync();
        updated[0].Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task SetEnabledAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _repo.SetEnabledAsync("000000000000000000000001", true);
        result.Should().BeFalse();
    }

    // =========================================================================
    // DeleteAsync
    // =========================================================================

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        await _repo.InsertAsync(CreateEntity());
        var all = await _repo.GetAllAsync();
        var id = all[0].Id!;

        var success = await _repo.DeleteAsync(id);
        success.Should().BeTrue();

        (await _repo.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _repo.DeleteAsync("000000000000000000000001");
        result.Should().BeFalse();
    }
}
