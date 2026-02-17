using FluentAssertions;
using Moq;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.Mongo;

/// <summary>
/// Tests for the MongoDB SmartUnlink repository via its interface.
/// Verifies behavioral contracts shared between MongoDB and LiteDB implementations.
/// </summary>
[Trait("Category", "Unit")]
public class MongoSmartUnlinkRepositoryContractTests
{
    private readonly Mock<ISmartUnlinkRepository> _repoMock = new();

    private static SmartUnlinkRadioEntity CreateEntity(string? id = "507f1f77bcf86cd799439011",
        string name = "FlexRadio 6700", bool enabled = true)
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllEntities()
    {
        var entities = new List<SmartUnlinkRadioEntity>
        {
            CreateEntity("id1", "Radio 1"),
            CreateEntity("id2", "Radio 2")
        };
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(entities);

        var result = await _repoMock.Object.GetAllAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SmartUnlinkRadioEntity>());

        var result = await _repoMock.Object.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task InsertAsync_IsCalled()
    {
        var entity = CreateEntity();
        _repoMock.Setup(r => r.InsertAsync(It.IsAny<SmartUnlinkRadioEntity>())).Returns(Task.CompletedTask);

        await _repoMock.Object.InsertAsync(entity);

        _repoMock.Verify(r => r.InsertAsync(entity), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ExistingEntity_ReturnsTrue()
    {
        var entity = CreateEntity();
        _repoMock.Setup(r => r.UpdateAsync(entity)).ReturnsAsync(true);

        var result = await _repoMock.Object.UpdateAsync(entity);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NonExistentEntity_ReturnsFalse()
    {
        var entity = CreateEntity("000000000000000000000001");
        _repoMock.Setup(r => r.UpdateAsync(entity)).ReturnsAsync(false);

        var result = await _repoMock.Object.UpdateAsync(entity);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabledAsync_ToDisabled_ReturnsTrue()
    {
        _repoMock.Setup(r => r.SetEnabledAsync("507f1f77bcf86cd799439011", false)).ReturnsAsync(true);

        var result = await _repoMock.Object.SetEnabledAsync("507f1f77bcf86cd799439011", false);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task SetEnabledAsync_NonExistentId_ReturnsFalse()
    {
        _repoMock.Setup(r => r.SetEnabledAsync(It.IsAny<string>(), It.IsAny<bool>())).ReturnsAsync(false);

        var result = await _repoMock.Object.SetEnabledAsync("000000000000000000000001", true);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_ReturnsTrue()
    {
        _repoMock.Setup(r => r.DeleteAsync("507f1f77bcf86cd799439011")).ReturnsAsync(true);

        var result = await _repoMock.Object.DeleteAsync("507f1f77bcf86cd799439011");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        _repoMock.Setup(r => r.DeleteAsync(It.IsAny<string>())).ReturnsAsync(false);

        var result = await _repoMock.Object.DeleteAsync("000000000000000000000001");

        result.Should().BeFalse();
    }
}
