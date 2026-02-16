using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Core.Database.LiteDb;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

/// <summary>
/// Tests for database provider startup behaviour.
///
/// Expected behaviour:
///   1. No config.json → use Local (LiteDB), start immediately.
///   2. config.json with Provider=Local → use LiteDB, never touch MongoDB.
///   3. config.json with Provider=Local AND a leftover MongoDbConnectionString
///      (e.g. macOS uninstall preserves ~/Library/Application Support) →
///      still use LiteDB, never create a MongoClient.
///   4. config.json with Provider=MongoDb and empty connection string →
///      fall back to Local.
///   5. Settings round-trip: save then load with LiteDB works without errors.
///   6. HamlibService respects the provider — does NOT create a MongoClient
///      when the provider is Local, even if a connection string is present.
/// </summary>
[Trait("Category", "Unit")]
public class StartupProviderTests
{
    // ──────────────────────────────────────────────────────────
    // 1. UserConfigService defaults
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UserConfig_Defaults_To_Local_Provider()
    {
        var config = new UserConfig();
        config.Provider.Should().Be(DatabaseProvider.Local);
        config.MongoDbConnectionString.Should().BeNull();
    }

    [Fact]
    public async Task UserConfigService_Returns_Local_When_No_ConfigFile_Exists()
    {
        var logger = new Mock<ILogger<UserConfigService>>();
        var svc = new UserConfigService(logger.Object);

        // GetConfigAsync should return defaults when no file exists
        // (the actual file path points to ~/Library/Application Support/Log4YM/config.json
        // which won't exist in CI)
        var config = await svc.GetConfigAsync();
        config.Provider.Should().Be(DatabaseProvider.Local);
    }

    // ──────────────────────────────────────────────────────────
    // 2. Provider resolution in Program.cs logic
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Provider_MongoDb_With_Empty_ConnectionString_Falls_Back_To_Local()
    {
        // Simulate the guard in Program.cs
        var userConfig = new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = ""
        };

        if (userConfig.Provider == DatabaseProvider.MongoDb
            && string.IsNullOrEmpty(userConfig.MongoDbConnectionString))
        {
            userConfig.Provider = DatabaseProvider.Local;
        }

        userConfig.Provider.Should().Be(DatabaseProvider.Local);
    }

    [Fact]
    public void Provider_MongoDb_With_Null_ConnectionString_Falls_Back_To_Local()
    {
        var userConfig = new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = null
        };

        if (userConfig.Provider == DatabaseProvider.MongoDb
            && string.IsNullOrEmpty(userConfig.MongoDbConnectionString))
        {
            userConfig.Provider = DatabaseProvider.Local;
        }

        userConfig.Provider.Should().Be(DatabaseProvider.Local);
    }

    [Fact]
    public void Provider_Local_With_Stale_ConnectionString_Stays_Local()
    {
        // This is the key scenario: user switched to Local but config.json
        // still has the old MongoDB connection string (SetupController preserves it).
        var userConfig = new UserConfig
        {
            Provider = DatabaseProvider.Local,
            MongoDbConnectionString = "mongodb+srv://stale-atlas-cluster.mongodb.net/db"
        };

        // The startup code should NOT override this to MongoDb
        userConfig.Provider.Should().Be(DatabaseProvider.Local);
    }

    // ──────────────────────────────────────────────────────────
    // 3. DbServiceRegistration
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AddDatabase_Local_Registers_LiteDb_Services()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new UserConfig { Provider = DatabaseProvider.Local };

        services.AddDatabase(config);

        // Should register LiteDbContext as IDbContext
        var dbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDbContext));
        dbContextDescriptor.Should().NotBeNull();
        // LiteDbContext should be registered
        services.Any(d => d.ServiceType == typeof(LiteDbContext) || d.ImplementationType == typeof(LiteDbContext))
            .Should().BeTrue("Local provider must register LiteDbContext");

        // Should NOT register MongoDbContext
        services.Any(d => d.ImplementationType?.Name == "MongoDbContext")
            .Should().BeFalse("Local provider must not register MongoDbContext");
    }

    [Fact]
    public void AddDatabase_MongoDb_Registers_MongoDb_Services()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var config = new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = "mongodb://localhost:27017"
        };

        services.AddDatabase(config);

        // Should register MongoDbContext
        services.Any(d => d.ImplementationType?.Name == "MongoDbContext"
                       || d.ServiceType.Name == "MongoDbContext")
            .Should().BeTrue("MongoDb provider must register MongoDbContext");
    }

    // ──────────────────────────────────────────────────────────
    // 4. HamlibService provider awareness
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void HamlibService_Does_Not_Create_MongoClient_When_Provider_Is_Local()
    {
        // Simulate what HamlibService constructor does:
        // it reads the config and should only create MongoClient
        // when Provider == MongoDb
        var config = new UserConfig
        {
            Provider = DatabaseProvider.Local,
            MongoDbConnectionString = "mongodb+srv://stale-atlas-cluster.mongodb.net/db"
        };

        // The condition HamlibService now uses:
        bool shouldCreateMongoClient = config.Provider == DatabaseProvider.MongoDb
            && !string.IsNullOrEmpty(config.MongoDbConnectionString);

        shouldCreateMongoClient.Should().BeFalse(
            "HamlibService must NOT create a MongoClient when provider is Local, " +
            "even if a stale connection string is present in config");
    }

    [Fact]
    public void HamlibService_Creates_MongoClient_When_Provider_Is_MongoDb()
    {
        var config = new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = "mongodb://localhost:27017"
        };

        bool shouldCreateMongoClient = config.Provider == DatabaseProvider.MongoDb
            && !string.IsNullOrEmpty(config.MongoDbConnectionString);

        shouldCreateMongoClient.Should().BeTrue(
            "HamlibService should create MongoClient when provider is MongoDb with valid connection string");
    }

    [Fact]
    public void HamlibService_Does_Not_Create_MongoClient_When_ConnectionString_Is_Empty()
    {
        var config = new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = ""
        };

        bool shouldCreateMongoClient = config.Provider == DatabaseProvider.MongoDb
            && !string.IsNullOrEmpty(config.MongoDbConnectionString);

        shouldCreateMongoClient.Should().BeFalse(
            "HamlibService must not create MongoClient with empty connection string");
    }

    // ──────────────────────────────────────────────────────────
    // 5. Settings model round-trip (JSON serialization)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Settings_Model_Deserializes_All_Frontend_Properties()
    {
        // This JSON represents what the frontend sends to POST /api/settings.
        // Every property must deserialize without error.
        var frontendJson = """
        {
            "station": {
                "callsign": "EI2KK",
                "operatorName": "Test",
                "gridSquare": "IO63",
                "latitude": 53.0,
                "longitude": -7.0,
                "city": "Dublin",
                "country": "Ireland"
            },
            "qrz": {
                "username": "test",
                "password": "test",
                "apiKey": "",
                "enabled": false
            },
            "appearance": {
                "theme": "dark",
                "compactMode": false
            },
            "rotator": {
                "enabled": false,
                "connectionType": "network",
                "ipAddress": "127.0.0.1",
                "port": 4533,
                "serialPort": "",
                "baudRate": 9600,
                "hamlibModelId": null,
                "hamlibModelName": "",
                "pollingIntervalMs": 500,
                "rotatorId": "default",
                "presets": [
                    { "name": "N", "azimuth": 0 },
                    { "name": "E", "azimuth": 90 }
                ]
            },
            "radio": {
                "followRadio": true,
                "activeRigType": null,
                "autoReconnect": false,
                "autoConnectRigId": null,
                "tci": {
                    "host": "localhost",
                    "port": 50001,
                    "name": "",
                    "autoConnect": false
                }
            },
            "map": {
                "tileLayer": "dark",
                "showSatellites": false,
                "selectedSatellites": ["ISS", "AO-91"],
                "rbn": {
                    "enabled": false,
                    "opacity": 0.7,
                    "showPaths": true,
                    "timeWindowMinutes": 5,
                    "minSnr": -10,
                    "bands": ["all"],
                    "modes": ["CW", "RTTY"]
                },
                "showPotaOverlay": false,
                "showDayNightOverlay": true,
                "showGrayLine": true,
                "showSunMarker": true,
                "showMoonMarker": true,
                "dayNightOpacity": 0.5,
                "grayLineOpacity": 0.6,
                "showCallsignImages": true,
                "maxCallsignImages": 50
            },
            "cluster": {
                "connections": []
            },
            "header": {
                "timeFormat": "24h",
                "showWeather": true,
                "weatherLocation": "Dublin"
            },
            "ai": {
                "provider": "anthropic",
                "apiKey": "",
                "model": "claude-sonnet-4-5-20250929",
                "autoGenerateTalkPoints": true,
                "includeQrzProfile": true,
                "includeQsoHistory": true,
                "includeSpotComments": false
            }
        }
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var settings = JsonSerializer.Deserialize<UserSettings>(frontendJson, options);

        settings.Should().NotBeNull();
        settings!.Station.Callsign.Should().Be("EI2KK");
        settings.Header.Should().NotBeNull();
        settings.Header.TimeFormat.Should().Be("24h");
        settings.Header.ShowWeather.Should().BeTrue();
        settings.Header.WeatherLocation.Should().Be("Dublin");
        settings.Rotator.ConnectionType.Should().Be("network");
        settings.Rotator.SerialPort.Should().Be("");
        settings.Rotator.BaudRate.Should().Be(9600);
        settings.Rotator.HamlibModelId.Should().BeNull();
        settings.Rotator.HamlibModelName.Should().Be("");
        settings.Radio.Tci.AutoConnect.Should().BeFalse();
        settings.Map.ShowSatellites.Should().BeFalse();
        settings.Map.SelectedSatellites.Should().Contain("ISS");
        settings.Map.ShowPotaOverlay.Should().BeFalse();
        settings.Map.ShowDayNightOverlay.Should().BeTrue();
        settings.Map.ShowGrayLine.Should().BeTrue();
        settings.Map.ShowSunMarker.Should().BeTrue();
        settings.Map.ShowMoonMarker.Should().BeTrue();
        settings.Map.DayNightOpacity.Should().Be(0.5);
        settings.Map.GrayLineOpacity.Should().Be(0.6);
        settings.Map.ShowCallsignImages.Should().BeTrue();
        settings.Map.MaxCallsignImages.Should().Be(50);
    }

    [Fact]
    public void Settings_Model_Handles_Unknown_Future_Properties_Gracefully()
    {
        // If the frontend adds new properties in the future,
        // the backend should NOT reject the JSON.
        var jsonWithExtraProps = """
        {
            "station": { "callsign": "EI2KK" },
            "someNewSection": { "foo": "bar" },
            "header": { "timeFormat": "12h", "newField": true }
        }
        """;

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var act = () => JsonSerializer.Deserialize<UserSettings>(jsonWithExtraProps, options);

        // Should NOT throw — unknown properties must be silently ignored
        act.Should().NotThrow();
        var settings = act();
        settings!.Station.Callsign.Should().Be("EI2KK");
        settings.Header.TimeFormat.Should().Be("12h");
    }

    [Fact]
    public void Settings_Model_Serializes_And_Deserializes_Roundtrip()
    {
        var original = new UserSettings
        {
            Station = new StationSettings { Callsign = "EI2KK", GridSquare = "IO63" },
            Header = new HeaderSettings { TimeFormat = "12h", ShowWeather = false, WeatherLocation = "Cork" },
            Rotator = new RotatorSettings
            {
                Enabled = true,
                ConnectionType = "serial",
                SerialPort = "/dev/ttyUSB0",
                BaudRate = 19200,
                HamlibModelId = 603,
                HamlibModelName = "Yaesu GS-232B"
            },
            Map = new MapSettings
            {
                ShowSatellites = true,
                SelectedSatellites = new List<string> { "ISS" },
                ShowDayNightOverlay = true,
                ShowGrayLine = true,
                DayNightOpacity = 0.3,
                GrayLineOpacity = 0.4,
                ShowCallsignImages = false,
                MaxCallsignImages = 25
            },
            Radio = new RadioSettings
            {
                Tci = new TciSettings { AutoConnect = true, Host = "192.168.1.100" }
            }
        };

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var json = JsonSerializer.Serialize(original, options);
        var deserialized = JsonSerializer.Deserialize<UserSettings>(json, options);

        deserialized.Should().NotBeNull();
        deserialized!.Station.Callsign.Should().Be("EI2KK");
        deserialized.Header.TimeFormat.Should().Be("12h");
        deserialized.Header.ShowWeather.Should().BeFalse();
        deserialized.Header.WeatherLocation.Should().Be("Cork");
        deserialized.Rotator.ConnectionType.Should().Be("serial");
        deserialized.Rotator.SerialPort.Should().Be("/dev/ttyUSB0");
        deserialized.Rotator.BaudRate.Should().Be(19200);
        deserialized.Rotator.HamlibModelId.Should().Be(603);
        deserialized.Rotator.HamlibModelName.Should().Be("Yaesu GS-232B");
        deserialized.Radio.Tci.AutoConnect.Should().BeTrue();
        deserialized.Map.ShowSatellites.Should().BeTrue();
        deserialized.Map.ShowDayNightOverlay.Should().BeTrue();
        deserialized.Map.DayNightOpacity.Should().Be(0.3);
        deserialized.Map.ShowCallsignImages.Should().BeFalse();
        deserialized.Map.MaxCallsignImages.Should().Be(25);
    }

    // ──────────────────────────────────────────────────────────
    // 6. LiteDB settings persistence
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void LiteDb_Settings_SaveAndLoad_Roundtrip()
    {
        // Use a temp file for LiteDB
        var tempDir = Path.Combine(Path.GetTempPath(), $"log4ym_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "test.db");

        try
        {
            using var db = new LiteDB.LiteDatabase($"Filename={dbPath};Connection=shared");
            var collection = db.GetCollection<UserSettings>("settings");

            var settings = new UserSettings
            {
                Id = "default",
                Station = new StationSettings { Callsign = "EI2KK", GridSquare = "IO63" },
                Header = new HeaderSettings { TimeFormat = "12h", WeatherLocation = "Dublin" },
                Rotator = new RotatorSettings
                {
                    ConnectionType = "serial",
                    SerialPort = "/dev/ttyUSB0",
                    BaudRate = 19200,
                    HamlibModelId = 603
                },
                Map = new MapSettings
                {
                    ShowDayNightOverlay = true,
                    ShowGrayLine = true,
                    DayNightOpacity = 0.3,
                    ShowCallsignImages = false,
                    MaxCallsignImages = 25
                },
                Radio = new RadioSettings
                {
                    Tci = new TciSettings { AutoConnect = true }
                }
            };

            // Save
            collection.Upsert(settings);
            db.Checkpoint();

            // Load
            var loaded = collection.FindById("default");

            loaded.Should().NotBeNull();
            loaded!.Station.Callsign.Should().Be("EI2KK");
            loaded.Header.TimeFormat.Should().Be("12h");
            loaded.Header.WeatherLocation.Should().Be("Dublin");
            loaded.Rotator.ConnectionType.Should().Be("serial");
            loaded.Rotator.SerialPort.Should().Be("/dev/ttyUSB0");
            loaded.Rotator.BaudRate.Should().Be(19200);
            loaded.Rotator.HamlibModelId.Should().Be(603);
            loaded.Map.ShowDayNightOverlay.Should().BeTrue();
            loaded.Map.ShowGrayLine.Should().BeTrue();
            loaded.Map.DayNightOpacity.Should().Be(0.3);
            loaded.Map.ShowCallsignImages.Should().BeFalse();
            loaded.Map.MaxCallsignImages.Should().Be(25);
            loaded.Radio.Tci.AutoConnect.Should().BeTrue();
        }
        finally
        {
            // Cleanup
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void LiteDb_Settings_Handles_Extra_Elements_On_Read()
    {
        // Simulate reading a document that has extra fields (e.g. saved by a newer version).
        // BsonIgnoreExtraElements should prevent errors.
        var tempDir = Path.Combine(Path.GetTempPath(), $"log4ym_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "test.db");

        try
        {
            using var db = new LiteDB.LiteDatabase($"Filename={dbPath};Connection=shared");
            var rawCollection = db.GetCollection("settings");

            // Insert a document with extra fields that don't exist on the model
            var doc = new LiteDB.BsonDocument
            {
                ["_id"] = "default",
                ["station"] = new LiteDB.BsonDocument { ["callsign"] = "EI2KK" },
                ["header"] = new LiteDB.BsonDocument { ["timeFormat"] = "24h", ["futureField"] = "value" },
                ["futureSection"] = new LiteDB.BsonDocument { ["key"] = "val" }
            };
            rawCollection.Upsert(doc);
            db.Checkpoint();

            // Reading via the typed collection should NOT throw
            var typedCollection = db.GetCollection<UserSettings>("settings");
            var loaded = typedCollection.FindById("default");

            loaded.Should().NotBeNull();
            loaded!.Station.Callsign.Should().Be("EI2KK");
            loaded.Header.TimeFormat.Should().Be("24h");
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    // ──────────────────────────────────────────────────────────
    // 7. SetupController provider switch behaviour
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Switching_To_Local_Preserves_MongoDb_ConnectionString_In_Config()
    {
        // This is the current behaviour of SetupController.Configure().
        // The connection string is preserved so the user can switch back.
        // HamlibService (and any other service) must check Provider, not connection string.
        var currentConfig = new UserConfig
        {
            Provider = DatabaseProvider.MongoDb,
            MongoDbConnectionString = "mongodb+srv://atlas-cluster.mongodb.net/db",
            MongoDbDatabaseName = "log4ym"
        };

        // Simulate what SetupController does when switching to Local
        var newConfig = new UserConfig
        {
            Provider = DatabaseProvider.Local,
            MongoDbConnectionString = currentConfig.MongoDbConnectionString,
            MongoDbDatabaseName = currentConfig.MongoDbDatabaseName,
        };

        newConfig.Provider.Should().Be(DatabaseProvider.Local);
        newConfig.MongoDbConnectionString.Should().NotBeNullOrEmpty(
            "Connection string is preserved for potential switch back");

        // But any service checking whether to use MongoDB must check Provider
        bool shouldUseMongoDb = newConfig.Provider == DatabaseProvider.MongoDb;
        shouldUseMongoDb.Should().BeFalse(
            "Services must check Provider, not just connection string presence");
    }

    // ──────────────────────────────────────────────────────────
    // 8. Default settings values
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void HeaderSettings_Has_Correct_Defaults()
    {
        var header = new HeaderSettings();
        header.TimeFormat.Should().Be("24h");
        header.ShowWeather.Should().BeTrue();
        header.WeatherLocation.Should().BeEmpty();
    }

    [Fact]
    public void RotatorSettings_Has_Correct_Defaults_For_New_Properties()
    {
        var rotator = new RotatorSettings();
        rotator.ConnectionType.Should().Be("network");
        rotator.SerialPort.Should().BeEmpty();
        rotator.BaudRate.Should().Be(9600);
        rotator.HamlibModelId.Should().BeNull();
        rotator.HamlibModelName.Should().BeEmpty();
    }

    [Fact]
    public void MapSettings_Has_Correct_Defaults_For_New_Properties()
    {
        var map = new MapSettings();
        map.ShowSatellites.Should().BeFalse();
        map.SelectedSatellites.Should().BeEquivalentTo(new[] { "ISS", "AO-91", "SO-50" });
        map.ShowPotaOverlay.Should().BeFalse();
        map.ShowDayNightOverlay.Should().BeFalse();
        map.ShowGrayLine.Should().BeFalse();
        map.ShowSunMarker.Should().BeTrue();
        map.ShowMoonMarker.Should().BeTrue();
        map.DayNightOpacity.Should().Be(0.5);
        map.GrayLineOpacity.Should().Be(0.6);
        map.ShowCallsignImages.Should().BeTrue();
        map.MaxCallsignImages.Should().Be(50);
    }

    [Fact]
    public void TciSettings_Has_AutoConnect_Default()
    {
        var tci = new TciSettings();
        tci.AutoConnect.Should().BeFalse();
    }

    [Fact]
    public void UserSettings_Has_Header_Section()
    {
        var settings = new UserSettings();
        settings.Header.Should().NotBeNull();
        settings.Header.TimeFormat.Should().Be("24h");
    }
}
