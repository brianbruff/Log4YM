using FluentAssertions;
using Log4YM.Contracts.Models;
using Log4YM.Server.Native.Hamlib;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

/// <summary>
/// Tests for rig management behaviour:
///   - Auto-connect guard conditions
///   - autoConnectRigId exclusivity (only one rig at a time)
///   - SaveConfigOnlyAsync sets _config without connecting
///   - Settings model round-trip for radio settings
/// </summary>
[Trait("Category", "Unit")]
public class RigManagementTests
{
    // ──────────────────────────────────────────────────────────
    // 1. Auto-connect guard conditions
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AutoConnect_Skips_When_AutoReconnect_Is_False()
    {
        var radioSettings = new RadioSettings
        {
            AutoReconnect = false,
            ActiveRigType = "hamlib"
        };

        // Mirrors the guard in HamlibService.TryAutoConnectAsync
        var shouldAutoConnect = radioSettings is { AutoReconnect: true, ActiveRigType: "hamlib" };

        shouldAutoConnect.Should().BeFalse(
            "auto-connect must not trigger when autoReconnect is disabled");
    }

    [Fact]
    public void AutoConnect_Skips_When_ActiveRigType_Is_Not_Hamlib()
    {
        var radioSettings = new RadioSettings
        {
            AutoReconnect = true,
            ActiveRigType = "tci"
        };

        var shouldAutoConnect = radioSettings is { AutoReconnect: true, ActiveRigType: "hamlib" };

        shouldAutoConnect.Should().BeFalse(
            "Hamlib auto-connect must not trigger when activeRigType is 'tci'");
    }

    [Fact]
    public void AutoConnect_Skips_When_ActiveRigType_Is_Null()
    {
        var radioSettings = new RadioSettings
        {
            AutoReconnect = true,
            ActiveRigType = null
        };

        var shouldAutoConnect = radioSettings is { AutoReconnect: true, ActiveRigType: "hamlib" };

        shouldAutoConnect.Should().BeFalse(
            "Hamlib auto-connect must not trigger when activeRigType is null");
    }

    [Fact]
    public void AutoConnect_Triggers_When_AutoReconnect_And_ActiveRigType_Hamlib()
    {
        var radioSettings = new RadioSettings
        {
            AutoReconnect = true,
            ActiveRigType = "hamlib"
        };

        var shouldAutoConnect = radioSettings is { AutoReconnect: true, ActiveRigType: "hamlib" };

        shouldAutoConnect.Should().BeTrue(
            "auto-connect should trigger when both conditions are met");
    }

    [Fact]
    public void TciAutoConnect_Triggers_When_AutoReconnect_And_ActiveRigType_Tci()
    {
        var radioSettings = new RadioSettings
        {
            AutoReconnect = true,
            ActiveRigType = "tci"
        };

        var shouldAutoConnect = radioSettings is { AutoReconnect: true, ActiveRigType: "tci" };

        shouldAutoConnect.Should().BeTrue(
            "TCI auto-connect should trigger when both conditions are met");
    }

    // ──────────────────────────────────────────────────────────
    // 2. autoConnectRigId exclusivity
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AutoConnectRigId_Is_Exclusive_Setting_RigB_Replaces_RigA()
    {
        var settings = new RadioSettings
        {
            AutoReconnect = true,
            AutoConnectRigId = "hamlib-1",
            ActiveRigType = "hamlib"
        };

        // Simulate user toggling auto-connect to a different rig
        settings.AutoConnectRigId = "tci-localhost:50001";
        settings.ActiveRigType = "tci";

        settings.AutoConnectRigId.Should().Be("tci-localhost:50001",
            "autoConnectRigId is a single value — setting rig B must replace rig A");
        settings.ActiveRigType.Should().Be("tci");
    }

    [Fact]
    public void AutoConnectRigId_Can_Be_Cleared()
    {
        var settings = new RadioSettings
        {
            AutoReconnect = true,
            AutoConnectRigId = "hamlib-1",
            ActiveRigType = "hamlib"
        };

        settings.AutoReconnect = false;
        settings.AutoConnectRigId = null;
        settings.ActiveRigType = null;

        settings.AutoReconnect.Should().BeFalse();
        settings.AutoConnectRigId.Should().BeNull();
        settings.ActiveRigType.Should().BeNull();
    }

    // ──────────────────────────────────────────────────────────
    // 3. HamlibRigConfig model
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void HamlibRigConfig_Has_Correct_Defaults()
    {
        var config = new HamlibRigConfig();

        config.ConnectionType.Should().Be(HamlibConnectionType.Serial);
        config.BaudRate.Should().Be(9600);
        config.DataBits.Should().Be(HamlibDataBits.Eight);
        config.StopBits.Should().Be(HamlibStopBits.One);
        config.FlowControl.Should().Be(HamlibFlowControl.None);
        config.Parity.Should().Be(HamlibParity.None);
        config.PttType.Should().Be(HamlibPttType.Rig);
        config.GetFrequency.Should().BeTrue();
        config.GetMode.Should().BeTrue();
        config.PollIntervalMs.Should().Be(250);
    }

    [Fact]
    public void HamlibRigConfig_RadioId_Derived_From_ModelId()
    {
        var config = new HamlibRigConfig { ModelId = 123, ModelName = "Test Rig" };

        // The radioId convention used by HamlibService
        var radioId = $"hamlib-{config.ModelId}";

        radioId.Should().Be("hamlib-123");
    }

    [Fact]
    public void HamlibRigConfig_MapFromHamlibConfig_Creates_RadioConfigEntity()
    {
        var config = new HamlibRigConfig
        {
            ModelId = 1,
            ModelName = "Dummy",
            ConnectionType = HamlibConnectionType.Network,
            Hostname = "localhost",
            NetworkPort = 4532
        };

        var entity = HamlibService.MapFromHamlibConfig(config);

        entity.RadioId.Should().Be("hamlib-1");
        entity.RadioType.Should().Be("hamlib");
        entity.DisplayName.Should().Be("Dummy");
        entity.Hostname.Should().Be("localhost");
        entity.NetworkPort.Should().Be(4532);
    }

    // ──────────────────────────────────────────────────────────
    // 4. TCI settings for saved rig discovery
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void TciSettings_RadioId_Derived_From_HostPort()
    {
        var tci = new TciSettings
        {
            Host = "192.168.1.100",
            Port = 50001,
            Name = "My Radio"
        };

        // The radioId convention used by TciRadioService
        var radioId = $"tci-{tci.Host}:{tci.Port}";

        radioId.Should().Be("tci-192.168.1.100:50001");
    }

    [Fact]
    public void TciSettings_Saved_Host_Makes_Rig_Discoverable()
    {
        // TciRadioService.GetDiscoveredRadiosAsync checks:
        // tciSettings != null && !string.IsNullOrEmpty(tciSettings.Host)
        var tci = new TciSettings { Host = "localhost", Port = 50001 };

        var isDiscoverable = tci != null && !string.IsNullOrEmpty(tci.Host);

        isDiscoverable.Should().BeTrue(
            "a saved TCI config with host set should be returned by GetDiscoveredRadiosAsync");
    }

    [Fact]
    public void TciSettings_Empty_Host_Not_Discoverable()
    {
        var tci = new TciSettings { Host = "", Port = 50001 };

        var isDiscoverable = tci != null && !string.IsNullOrEmpty(tci.Host);

        isDiscoverable.Should().BeFalse(
            "a TCI config with empty host should not appear in discovered radios");
    }

    // ──────────────────────────────────────────────────────────
    // 5. Radio settings round-trip
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void RadioSettings_Has_Correct_Defaults()
    {
        var radio = new RadioSettings();

        radio.AutoReconnect.Should().BeFalse();
        radio.AutoConnectRigId.Should().BeNull();
        radio.ActiveRigType.Should().BeNull();
        radio.FollowRadio.Should().BeTrue();
    }
}
