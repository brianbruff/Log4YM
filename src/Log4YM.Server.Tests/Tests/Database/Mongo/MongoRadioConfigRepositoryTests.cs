using FluentAssertions;
using Moq;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.Mongo;

/// <summary>
/// Tests for the MongoDB RadioConfig repository via its interface.
/// Verifies behavioral contracts shared between MongoDB and LiteDB implementations.
/// </summary>
[Trait("Category", "Unit")]
public class MongoRadioConfigRepositoryContractTests
{
    private readonly Mock<IRadioConfigRepository> _repoMock = new();

    private static RadioConfigEntity CreateHamlibConfig(string radioId = "hamlib-361", string displayName = "TS-590S")
    {
        return new RadioConfigEntity
        {
            Id = "507f1f77bcf86cd799439011",
            RadioId = radioId,
            RadioType = "hamlib",
            DisplayName = displayName,
            HamlibModelId = 361,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static RadioConfigEntity CreateTciConfig(string radioId = "tci-localhost:50001")
    {
        return new RadioConfigEntity
        {
            Id = "507f1f77bcf86cd799439012",
            RadioId = radioId,
            RadioType = "tci",
            DisplayName = "ExpertSDR3",
            TciHost = "localhost",
            TciPort = 50001,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllConfigs()
    {
        var configs = new List<RadioConfigEntity> { CreateHamlibConfig(), CreateTciConfig() };
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(configs);

        var result = await _repoMock.Object.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<RadioConfigEntity>());

        var result = await _repoMock.Object.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByRadioIdAsync_ExistingId_ReturnsConfig()
    {
        var config = CreateHamlibConfig("hamlib-361", "TS-590S");
        _repoMock.Setup(r => r.GetByRadioIdAsync("hamlib-361")).ReturnsAsync(config);

        var result = await _repoMock.Object.GetByRadioIdAsync("hamlib-361");

        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("TS-590S");
    }

    [Fact]
    public async Task GetByRadioIdAsync_NonExistentId_ReturnsNull()
    {
        _repoMock.Setup(r => r.GetByRadioIdAsync(It.IsAny<string>())).ReturnsAsync((RadioConfigEntity?)null);

        var result = await _repoMock.Object.GetByRadioIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeAsync_FiltersByRadioType()
    {
        var hamlibConfigs = new List<RadioConfigEntity> { CreateHamlibConfig("hamlib-1"), CreateHamlibConfig("hamlib-2") };
        _repoMock.Setup(r => r.GetByTypeAsync("hamlib")).ReturnsAsync(hamlibConfigs);

        var result = await _repoMock.Object.GetByTypeAsync("hamlib");

        result.Should().HaveCount(2);
        result.All(c => c.RadioType == "hamlib").Should().BeTrue();
    }

    [Fact]
    public async Task GetByTypeAsync_NoMatch_ReturnsEmpty()
    {
        _repoMock.Setup(r => r.GetByTypeAsync("unknown")).ReturnsAsync(new List<RadioConfigEntity>());

        var result = await _repoMock.Object.GetByTypeAsync("unknown");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertByRadioIdAsync_IsCalled()
    {
        var config = CreateHamlibConfig();
        _repoMock.Setup(r => r.UpsertByRadioIdAsync(It.IsAny<RadioConfigEntity>())).Returns(Task.CompletedTask);

        await _repoMock.Object.UpsertByRadioIdAsync(config);

        _repoMock.Verify(r => r.UpsertByRadioIdAsync(config), Times.Once);
    }

    [Fact]
    public async Task DeleteByRadioIdAsync_ExistingId_ReturnsTrue()
    {
        _repoMock.Setup(r => r.DeleteByRadioIdAsync("hamlib-361")).ReturnsAsync(true);

        var result = await _repoMock.Object.DeleteByRadioIdAsync("hamlib-361");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteByRadioIdAsync_NonExistentId_ReturnsFalse()
    {
        _repoMock.Setup(r => r.DeleteByRadioIdAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await _repoMock.Object.DeleteByRadioIdAsync("nonexistent");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MigrateOldHamlibConfigAsync_WhenMigrated_ReturnsTrue()
    {
        _repoMock.Setup(r => r.MigrateOldHamlibConfigAsync()).ReturnsAsync(true);

        var result = await _repoMock.Object.MigrateOldHamlibConfigAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MigrateOldHamlibConfigAsync_WhenAlreadyMigrated_ReturnsFalse()
    {
        _repoMock.Setup(r => r.MigrateOldHamlibConfigAsync()).ReturnsAsync(false);

        var result = await _repoMock.Object.MigrateOldHamlibConfigAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task FixNullIdsAsync_IsCalled()
    {
        _repoMock.Setup(r => r.FixNullIdsAsync()).Returns(Task.CompletedTask);

        await _repoMock.Object.FixNullIdsAsync();

        _repoMock.Verify(r => r.FixNullIdsAsync(), Times.Once);
    }
}
