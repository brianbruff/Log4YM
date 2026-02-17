using FluentAssertions;
using LiteDB;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Tests.Fixtures;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Database.LiteDb;

[Trait("Category", "Integration")]
public class LiteRadioConfigRepositoryTests : IDisposable
{
    private readonly LiteDbTestFixture _fixture;
    private readonly LiteRadioConfigRepository _repo;

    public LiteRadioConfigRepositoryTests()
    {
        _fixture = new LiteDbTestFixture();
        _repo = new LiteRadioConfigRepository(_fixture.Context);
    }

    public void Dispose() => _fixture.Dispose();

    private static RadioConfigEntity CreateHamlibConfig(string radioId = "hamlib-361", string displayName = "Kenwood TS-590S")
    {
        return new RadioConfigEntity
        {
            Id = ObjectId.NewObjectId().ToString(),
            RadioId = radioId,
            RadioType = "hamlib",
            DisplayName = displayName,
            HamlibModelId = 361,
            HamlibModelName = "TS-590S",
            ConnectionType = "Serial",
            SerialPort = "/dev/ttyUSB0",
            BaudRate = 9600,
            DataBits = 8,
            StopBits = 1,
            FlowControl = "None",
            Parity = "None",
            NetworkPort = 4532,
            PttType = "Rig",
            GetFrequency = true,
            GetMode = true,
            GetVfo = true,
            GetPtt = true,
            GetPower = false,
            GetRit = false,
            GetXit = false,
            GetKeySpeed = false,
            PollIntervalMs = 250,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static RadioConfigEntity CreateTciConfig(string radioId = "tci-localhost:50001")
    {
        return new RadioConfigEntity
        {
            Id = ObjectId.NewObjectId().ToString(),
            RadioId = radioId,
            RadioType = "tci",
            DisplayName = "ExpertSDR3 TCI",
            TciHost = "localhost",
            TciPort = 50001,
            TciName = "TCI Radio",
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
    public async Task GetAllAsync_ReturnsAllConfigs()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig());
        await _repo.UpsertByRadioIdAsync(CreateTciConfig());

        var result = await _repo.GetAllAsync();
        result.Should().HaveCount(2);
    }

    // =========================================================================
    // GetByRadioIdAsync
    // =========================================================================

    [Fact]
    public async Task GetByRadioIdAsync_ExistingId_ReturnsConfig()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig("hamlib-361", "TS-590S"));

        var result = await _repo.GetByRadioIdAsync("hamlib-361");
        result.Should().NotBeNull();
        result!.DisplayName.Should().Be("TS-590S");
    }

    [Fact]
    public async Task GetByRadioIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repo.GetByRadioIdAsync("nonexistent");
        result.Should().BeNull();
    }

    // =========================================================================
    // GetByTypeAsync
    // =========================================================================

    [Fact]
    public async Task GetByTypeAsync_ReturnsMatchingType()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig("hamlib-1"));
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig("hamlib-2"));
        await _repo.UpsertByRadioIdAsync(CreateTciConfig());

        var hamlibConfigs = await _repo.GetByTypeAsync("hamlib");
        hamlibConfigs.Should().HaveCount(2);
        hamlibConfigs.All(c => c.RadioType == "hamlib").Should().BeTrue();
    }

    [Fact]
    public async Task GetByTypeAsync_NoMatch_ReturnsEmpty()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig());

        var result = await _repo.GetByTypeAsync("tci");
        result.Should().BeEmpty();
    }

    // =========================================================================
    // UpsertByRadioIdAsync
    // =========================================================================

    [Fact]
    public async Task UpsertByRadioIdAsync_Inserts_WhenNew()
    {
        var config = CreateHamlibConfig("hamlib-100");
        await _repo.UpsertByRadioIdAsync(config);

        var result = await _repo.GetByRadioIdAsync("hamlib-100");
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task UpsertByRadioIdAsync_Updates_WhenExists()
    {
        var config = CreateHamlibConfig("hamlib-361", "Original Name");
        await _repo.UpsertByRadioIdAsync(config);

        var updated = config with { DisplayName = "Updated Name" };
        await _repo.UpsertByRadioIdAsync(updated);

        var result = await _repo.GetByRadioIdAsync("hamlib-361");
        result!.DisplayName.Should().Be("Updated Name");

        var all = await _repo.GetAllAsync();
        all.Should().HaveCount(1); // No duplicates
    }

    // =========================================================================
    // DeleteByRadioIdAsync
    // =========================================================================

    [Fact]
    public async Task DeleteByRadioIdAsync_ExistingId_ReturnsTrue()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig("hamlib-to-delete"));

        var result = await _repo.DeleteByRadioIdAsync("hamlib-to-delete");
        result.Should().BeTrue();

        (await _repo.GetByRadioIdAsync("hamlib-to-delete")).Should().BeNull();
    }

    [Fact]
    public async Task DeleteByRadioIdAsync_NonExistentId_ReturnsFalse()
    {
        var result = await _repo.DeleteByRadioIdAsync("nonexistent");
        result.Should().BeFalse();
    }

    // =========================================================================
    // FixNullIdsAsync
    // =========================================================================

    [Fact]
    public async Task FixNullIdsAsync_WithValidIds_DoesNotAlterData()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig("hamlib-361"));
        var before = await _repo.GetAllAsync();

        await _repo.FixNullIdsAsync();

        var after = await _repo.GetAllAsync();
        after.Should().HaveCount(before.Count);
        after.All(c => !string.IsNullOrEmpty(c.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task FixNullIdsAsync_AssignsIdsToDocumentsWithNullIds()
    {
        // Insert a raw document with null _id to simulate the bug
        var rawCollection = _fixture.Context.Database.GetCollection("radio_configs");
        var doc = new BsonDocument
        {
            ["_id"] = BsonValue.Null,
            ["RadioId"] = "hamlib-buggy",
            ["RadioType"] = "hamlib",
            ["DisplayName"] = "Buggy Config"
        };
        rawCollection.Insert(doc);

        await _repo.FixNullIdsAsync();

        // The repaired document should now have a proper ID
        var result = await _repo.GetByRadioIdAsync("hamlib-buggy");
        result.Should().NotBeNull();
        result!.RadioId.Should().Be("hamlib-buggy");
    }

    // =========================================================================
    // MigrateOldHamlibConfigAsync
    // =========================================================================

    [Fact]
    public async Task MigrateOldHamlibConfigAsync_WhenNoOldConfig_ReturnsFalse()
    {
        var result = await _repo.MigrateOldHamlibConfigAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MigrateOldHamlibConfigAsync_WhenHamlibAlreadyExists_ReturnsFalse()
    {
        await _repo.UpsertByRadioIdAsync(CreateHamlibConfig());

        var result = await _repo.MigrateOldHamlibConfigAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MigrateOldHamlibConfigAsync_WithOldConfig_MigratesAndReturnsTrue()
    {
        // Insert old-style hamlib_config in settings collection
        var settingsCollection = _fixture.Context.Database.GetCollection("settings");
        var oldDoc = new BsonDocument
        {
            ["_id"] = "hamlib_config",
            ["ModelId"] = 361,
            ["ModelName"] = "Kenwood TS-590S",
            ["ConnectionType"] = "Serial",
            ["SerialPort"] = "/dev/ttyUSB0",
            ["BaudRate"] = 9600,
            ["DataBits"] = 8,
            ["StopBits"] = 1,
            ["FlowControl"] = "None",
            ["Parity"] = "None",
            ["NetworkPort"] = 4532,
            ["PttType"] = "Rig",
            ["GetFrequency"] = true,
            ["GetMode"] = true,
            ["GetVfo"] = true,
            ["GetPtt"] = true,
            ["PollIntervalMs"] = 250
        };
        settingsCollection.Insert(oldDoc);

        var result = await _repo.MigrateOldHamlibConfigAsync();
        result.Should().BeTrue();

        // Config should now be in radio_configs
        var migrated = await _repo.GetByTypeAsync("hamlib");
        migrated.Should().HaveCount(1);
        migrated[0].HamlibModelId.Should().Be(361);
        migrated[0].RadioId.Should().Be("hamlib-361");

        // Old config should be removed from settings
        var oldStillExists = settingsCollection.FindById("hamlib_config");
        oldStillExists.Should().BeNull();
    }

    [Fact]
    public async Task MigrateOldHamlibConfigAsync_WithOldConfigNoModelId_ReturnsFalse()
    {
        // Insert old-style config with ModelId = 0 (invalid)
        var settingsCollection = _fixture.Context.Database.GetCollection("settings");
        var oldDoc = new BsonDocument
        {
            ["_id"] = "hamlib_config",
            ["ModelId"] = 0
        };
        settingsCollection.Insert(oldDoc);

        var result = await _repo.MigrateOldHamlibConfigAsync();
        result.Should().BeFalse();
    }
}
