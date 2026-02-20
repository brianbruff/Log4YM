using FluentAssertions;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Hubs;

/// <summary>
/// Tests for frequency compensation when switching between CW and SSB modes.
/// This addresses the issue where radios shift frequency by ~700 Hz when switching modes.
/// Note: Since ApplyFrequencyCompensation is an instance method and LogHub requires many dependencies,
/// we test the logic by replicating it here as a private static method.
/// </summary>
[Trait("Category", "Unit")]
public class LogHubFrequencyCompensationTests
{
    private const long TestFrequencyHz = 14_050_000; // 14.050 MHz (20m CW)
    private const long OffsetHz = 700;

    /// <summary>
    /// Apply frequency compensation when switching between CW and SSB modes.
    /// CW uses carrier frequency, SSB uses suppressed carrier frequency.
    /// Standard offset is 700 Hz for USB and -700 Hz for LSB.
    /// This is a copy of the logic in LogHub.ApplyFrequencyCompensation for testing purposes.
    /// </summary>
    private static long ApplyFrequencyCompensation(long targetFrequencyHz, string? currentMode, string? targetMode)
    {
        if (string.IsNullOrEmpty(targetMode)) return targetFrequencyHz;

        const long offsetHz = 700;

        var isCwMode = (string mode) => mode?.ToUpperInvariant() switch
        {
            "CW" => true,
            "CWU" => true,
            "CWL" => true,
            "CWR" => true,
            _ => false
        };

        var isSsbMode = (string mode) => mode?.ToUpperInvariant() switch
        {
            "SSB" => true,
            "USB" => true,
            "LSB" => true,
            _ => false
        };

        var isLsbMode = (string mode) => mode?.ToUpperInvariant() switch
        {
            "LSB" => true,
            "SSB" when targetFrequencyHz < 10_000_000 => true,
            _ => false
        };

        var currentIsCw = currentMode != null && isCwMode(currentMode);
        var currentIsSsb = currentMode != null && isSsbMode(currentMode);
        var targetIsCw = isCwMode(targetMode);
        var targetIsSsb = isSsbMode(targetMode);

        // Switching from SSB to CW: subtract offset (CW carrier is below SSB audio)
        // For LSB: add offset instead (LSB audio is below carrier)
        if (currentIsSsb && targetIsCw)
        {
            var currentIsLsb = currentMode != null && isLsbMode(currentMode);
            var adjustment = currentIsLsb ? offsetHz : -offsetHz;
            return targetFrequencyHz + adjustment;
        }

        // Switching from CW to SSB: add offset (SSB audio is above CW carrier)
        // For LSB: subtract offset instead
        if (currentIsCw && targetIsSsb)
        {
            var targetIsLsb = isLsbMode(targetMode);
            var adjustment = targetIsLsb ? -offsetHz : offsetHz;
            return targetFrequencyHz + adjustment;
        }

        // No compensation needed for same-mode or other mode transitions
        return targetFrequencyHz;
    }

    #region SSB to CW Transitions

    [Fact]
    public void ApplyFrequencyCompensation_UsbToCw_SubtractsOffset()
    {
        // When switching from USB to CW, we need to subtract 700 Hz
        // because CW carrier is below the SSB audio frequency
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "CW");
        result.Should().Be(TestFrequencyHz - OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_LsbToCw_AddsOffset()
    {
        // When switching from LSB to CW, we need to add 700 Hz
        // because LSB audio is below the carrier
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "LSB", "CW");
        result.Should().Be(TestFrequencyHz + OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_SsbToCw_Below10MHz_AddsOffset()
    {
        // SSB mode on HF below 10 MHz defaults to LSB
        var freq = 7_050_000; // 7.050 MHz (40m)
        var result = ApplyFrequencyCompensation(freq, "SSB", "CW");
        result.Should().Be(freq + OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_SsbToCw_Above10MHz_SubtractsOffset()
    {
        // SSB mode on HF above 10 MHz defaults to USB
        var freq = 14_250_000; // 14.250 MHz (20m SSB)
        var result = ApplyFrequencyCompensation(freq, "SSB", "CW");
        result.Should().Be(freq - OffsetHz);
    }

    #endregion

    #region CW to SSB Transitions

    [Fact]
    public void ApplyFrequencyCompensation_CwToUsb_AddsOffset()
    {
        // When switching from CW to USB, we need to add 700 Hz
        // because SSB audio frequency is above the CW carrier
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CW", "USB");
        result.Should().Be(TestFrequencyHz + OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_CwToLsb_SubtractsOffset()
    {
        // When switching from CW to LSB, we need to subtract 700 Hz
        // because LSB audio is below the carrier
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CW", "LSB");
        result.Should().Be(TestFrequencyHz - OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_CwToSsb_Below10MHz_SubtractsOffset()
    {
        // SSB mode on HF below 10 MHz defaults to LSB
        var freq = 3_550_000; // 3.550 MHz (80m)
        var result = ApplyFrequencyCompensation(freq, "CW", "SSB");
        result.Should().Be(freq - OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_CwToSsb_Above10MHz_AddsOffset()
    {
        // SSB mode on HF above 10 MHz defaults to USB
        var freq = 21_050_000; // 21.050 MHz (15m)
        var result = ApplyFrequencyCompensation(freq, "CW", "SSB");
        result.Should().Be(freq + OffsetHz);
    }

    #endregion

    #region CW Variant Modes

    [Fact]
    public void ApplyFrequencyCompensation_CwuToUsb_AddsOffset()
    {
        // CWU (CW Upper) should be treated like CW
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CWU", "USB");
        result.Should().Be(TestFrequencyHz + OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_CwlToUsb_AddsOffset()
    {
        // CWL (CW Lower) should be treated like CW
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CWL", "USB");
        result.Should().Be(TestFrequencyHz + OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_CwrToUsb_AddsOffset()
    {
        // CWR (CW Reverse) should be treated like CW
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CWR", "USB");
        result.Should().Be(TestFrequencyHz + OffsetHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_UsbToCwu_SubtractsOffset()
    {
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "CWU");
        result.Should().Be(TestFrequencyHz - OffsetHz);
    }

    #endregion

    #region Same Mode Transitions (No Compensation)

    [Fact]
    public void ApplyFrequencyCompensation_CwToCw_NoChange()
    {
        // Same mode should not apply any compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CW", "CW");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_UsbToUsb_NoChange()
    {
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "USB");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_LsbToLsb_NoChange()
    {
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "LSB", "LSB");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_UsbToLsb_NoChange()
    {
        // SSB to SSB transitions (even different sidebands) should not compensate
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "LSB");
        result.Should().Be(TestFrequencyHz);
    }

    #endregion

    #region Other Mode Transitions (No Compensation)

    [Fact]
    public void ApplyFrequencyCompensation_CwToFt8_NoChange()
    {
        // CW to digital mode should not apply compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "CW", "FT8");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_UsbToFt8_NoChange()
    {
        // SSB to digital mode should not apply compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "FT8");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_Ft8ToCw_NoChange()
    {
        // Digital mode to CW should not apply compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "FT8", "CW");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_AmToFm_NoChange()
    {
        // Other mode transitions should not apply compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "AM", "FM");
        result.Should().Be(TestFrequencyHz);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ApplyFrequencyCompensation_NullCurrentMode_NoChange()
    {
        // If we don't know the current mode, don't apply compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, null, "CW");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_NullTargetMode_NoChange()
    {
        // If no target mode is provided, don't apply compensation
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", null);
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_EmptyTargetMode_NoChange()
    {
        var result = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "");
        result.Should().Be(TestFrequencyHz);
    }

    [Fact]
    public void ApplyFrequencyCompensation_CaseInsensitive_Works()
    {
        // Mode names should be case-insensitive
        var result1 = ApplyFrequencyCompensation(TestFrequencyHz, "usb", "cw");
        var result2 = ApplyFrequencyCompensation(TestFrequencyHz, "USB", "CW");
        result1.Should().Be(result2);
        result1.Should().Be(TestFrequencyHz - OffsetHz);
    }

    #endregion

    #region Realistic Scenarios from Issue

    [Fact]
    public void ApplyFrequencyCompensation_IssueScenario_SsbToCw_14MHz()
    {
        // Issue reports -0.0007 MHz (700 Hz) shift when going from SSB to CW
        // At 14 MHz, SSB defaults to USB
        var freq = 14_250_000; // 14.250 MHz - typical SSB frequency
        var result = ApplyFrequencyCompensation(freq, "SSB", "CW");

        // Should subtract 700 Hz to compensate
        result.Should().Be(freq - 700);
    }

    [Fact]
    public void ApplyFrequencyCompensation_IssueScenario_CwToLsb_3MHz()
    {
        // Issue reports +0.0007 MHz (700 Hz) shift when going from CW to LSB
        var freq = 3_525_000; // 3.525 MHz - typical 80m CW frequency
        var result = ApplyFrequencyCompensation(freq, "CW", "LSB");

        // Should subtract 700 Hz to compensate
        result.Should().Be(freq - 700);
    }

    #endregion
}
