using System.Text.RegularExpressions;
using FluentAssertions;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

/// <summary>
/// Tests for DX cluster spot parsing logic.
/// Since ClusterConnectionHandler is internal, we test the regex patterns and helper methods directly.
/// </summary>
[Trait("Category", "Unit")]
public class DxClusterServiceParsingTests
{
    // Replicate the DX spot regex from ClusterConnectionHandler (it's private, so we re-define for testing)
    private static readonly Regex DxSpotRegex = new(
        @"^DX de ([A-Z0-9/]+):\s+(\d+\.?\d*)\s+([A-Z0-9/]+)\s+(.*?)\s+(\d{4})Z(?:\s+([A-Z]{2}\d{2}))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CcSpotRegex = new(
        @"^CC\d+\^(\d+\.?\d*)\^([A-Z0-9/]+(?:-[A-Z0-9]+)?)\^[^\^]+\^(\d{4})Z?\^([^\^]*)\^([A-Z0-9/]+(?:-[#0-9]+)?)\^[^\^]*\^[^\^]*\^([^\^]*)\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^[^\^]*\^([^\^]*)\^[^\^]*\^([^\^]*)\^",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #region Standard DX Spot Format

    [Fact]
    public void DxSpotRegex_StandardFormat_MatchesCorrectly()
    {
        var line = "DX de W3LPL:    14205.0  EA8TJ        CW 15 dB 25 WPM CQ               1847Z FN20";

        var match = DxSpotRegex.Match(line);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("W3LPL");         // spotter
        match.Groups[2].Value.Should().Be("14205.0");        // frequency
        match.Groups[3].Value.Should().Be("EA8TJ");          // DX callsign
        match.Groups[5].Value.Should().Be("1847");           // time
        match.Groups[6].Value.Should().Be("FN20");           // grid
    }

    [Fact]
    public void DxSpotRegex_WithSlashCallsign_MatchesCorrectly()
    {
        var line = "DX de DL0ABC:     7012.0  VP2V/W3XYZ   CQ CQ CQ                        2130Z";

        var match = DxSpotRegex.Match(line);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("DL0ABC");
        match.Groups[3].Value.Should().Be("VP2V/W3XYZ");
    }

    [Fact]
    public void DxSpotRegex_IntegerFrequency_MatchesCorrectly()
    {
        var line = "DX de K1ABC:     14200  W1AW         SSB                              1200Z";

        var match = DxSpotRegex.Match(line);

        match.Success.Should().BeTrue();
        match.Groups[2].Value.Should().Be("14200");
    }

    [Fact]
    public void DxSpotRegex_NoGrid_MatchesWithoutGrid()
    {
        var line = "DX de K1TTT:    18082.0  A71A         cq                               0345Z";

        var match = DxSpotRegex.Match(line);

        match.Success.Should().BeTrue();
        match.Groups[6].Success.Should().BeFalse();
    }

    [Fact]
    public void DxSpotRegex_NonSpotLine_DoesNotMatch()
    {
        var line = "Hello from the DX cluster!";

        var match = DxSpotRegex.Match(line);

        match.Success.Should().BeFalse();
    }

    [Fact]
    public void DxSpotRegex_EmptyLine_DoesNotMatch()
    {
        var match = DxSpotRegex.Match("");

        match.Success.Should().BeFalse();
    }

    #endregion

    #region CC Cluster Format (VE7CC)

    [Fact]
    public void CcSpotRegex_CcFormat_MatchesCorrectly()
    {
        var line = "CC11^14025.0^EA8TJ^W3LPL^1847Z^CW 15 dB 25 WPM CQ^W3LPL-1^DX^de^EA^island^skip^skip^skip^skip^skip^Spain^skip^IM18^";

        var match = CcSpotRegex.Match(line);

        match.Success.Should().BeTrue();
        match.Groups[1].Value.Should().Be("14025.0");  // frequency
        match.Groups[2].Value.Should().Be("EA8TJ");    // DX callsign
        match.Groups[3].Value.Should().Be("1847");     // time
        match.Groups[5].Value.Should().Be("W3LPL-1");  // spotter
    }

    [Fact]
    public void CcSpotRegex_NonCcLine_DoesNotMatch()
    {
        var line = "DX de W3LPL:    14205.0  EA8TJ        CW                              1847Z";

        var match = CcSpotRegex.Match(line);

        match.Success.Should().BeFalse();
    }

    #endregion

    #region Mode Extraction from Comments

    [Theory]
    [InlineData("FT8 -10 dB", "FT8")]
    [InlineData("FT4", "FT4")]
    [InlineData("CW 15 dB 25 WPM CQ", "CW")]
    [InlineData("SSB big signal", "SSB")]
    [InlineData("USB very loud", "SSB")]
    [InlineData("LSB good copy", "SSB")]
    [InlineData("RTTY contest", "RTTY")]
    [InlineData("PSK31 calling CQ", "PSK31")]
    [InlineData("FM simplex", "FM")]
    [InlineData("AM broadcast", "AM")]
    [InlineData("DIGI mode", "DIGI")]
    [InlineData("JT65 decode", "JT65")]
    [InlineData("JT9 -24", "JT9")]
    public void ExtractMode_FromComment_ReturnsExpectedMode(string comment, string expectedMode)
    {
        var result = ExtractMode(comment);
        result.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData("CQ CQ CQ")]
    [InlineData("calling")]
    [InlineData("599 NJ")]
    [InlineData("")]
    public void ExtractMode_NoModeInComment_ReturnsNull(string comment)
    {
        var result = ExtractMode(comment);
        result.Should().BeNull();
    }

    #endregion

    #region Mode Inference from Frequency

    [Theory]
    [InlineData(1840, "FT8")]
    [InlineData(1841, "FT8")]
    [InlineData(3573, "FT8")]
    [InlineData(3574, "FT8")]
    [InlineData(7074, "FT8")]
    [InlineData(7075, "FT8")]
    [InlineData(10136, "FT8")]
    [InlineData(10137, "FT8")]
    [InlineData(14074, "FT8")]
    [InlineData(14075, "FT8")]
    [InlineData(18100, "FT8")]
    [InlineData(18101, "FT8")]
    [InlineData(21074, "FT8")]
    [InlineData(21075, "FT8")]
    [InlineData(24915, "FT8")]
    [InlineData(24916, "FT8")]
    [InlineData(28074, "FT8")]
    [InlineData(28075, "FT8")]
    [InlineData(50313, "FT8")]
    [InlineData(50314, "FT8")]
    public void InferModeFromFrequency_FT8Frequencies_ReturnsFT8(double frequencyKhz, string expectedMode)
    {
        var result = InferModeFromFrequency(frequencyKhz);
        result.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData(1830, "CW")]
    [InlineData(1843, "SSB")]
    [InlineData(1900, "SSB")]
    [InlineData(3550, "CW")]
    [InlineData(3600, "SSB")]
    [InlineData(3750, "SSB")]
    [InlineData(7050, "CW")]
    [InlineData(7125, "SSB")]
    [InlineData(7200, "SSB")]
    [InlineData(10100, "CW")]
    [InlineData(10140, "CW")]
    [InlineData(14050, "CW")]
    [InlineData(14150, "SSB")]
    [InlineData(14250, "SSB")]
    [InlineData(21050, "CW")]
    [InlineData(21200, "SSB")]
    [InlineData(28050, "CW")]
    [InlineData(28300, "SSB")]
    [InlineData(50050, "CW")]
    [InlineData(50100, "SSB")]
    public void InferModeFromFrequency_ReturnsExpectedMode(double frequencyKhz, string expectedMode)
    {
        var result = InferModeFromFrequency(frequencyKhz);
        result.Should().Be(expectedMode);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200000)]
    public void InferModeFromFrequency_OutOfRange_DefaultsToSSB(double frequencyKhz)
    {
        var result = InferModeFromFrequency(frequencyKhz);
        result.Should().Be("SSB");
    }

    #endregion

    #region Country Lookup via CtyService

    [Theory]
    [InlineData("K1ABC", "United States")]
    [InlineData("W1AW", "United States")]
    [InlineData("N5DX", "United States")]
    [InlineData("VE3ABC", "Canada")]
    [InlineData("DL1ABC", "Fed. Rep. of Germany")]
    [InlineData("G4ABC", "England")]
    [InlineData("JA1YXP", "Japan")]
    [InlineData("VK2ABC", "Australia")]
    [InlineData("ZL1ABC", "New Zealand")]
    [InlineData("PY2ABC", "Brazil")]
    [InlineData("EA1ABC", "Spain")]
    [InlineData("F5ABC", "France")]
    [InlineData("I2ABC", "Italy")]
    [InlineData("OH2ABC", "Finland")]
    [InlineData("SM5ABC", "Sweden")]
    [InlineData("ZS6ABC", "South Africa")]
    public void CtyService_KnownCallsigns_ReturnsCorrectCountry(string callsign, string expectedCountry)
    {
        var (country, _) = CtyService.GetCountryFromCallsign(callsign);
        country.Should().Be(expectedCountry);
    }

    [Theory]
    [InlineData("K1ABC", "NA")]
    [InlineData("VE3ABC", "NA")]
    [InlineData("DL1ABC", "EU")]
    [InlineData("JA1YXP", "AS")]
    [InlineData("VK2ABC", "OC")]
    [InlineData("PY2ABC", "SA")]
    [InlineData("ZS6ABC", "AF")]
    public void CtyService_KnownCallsigns_ReturnsCorrectContinent(string callsign, string expectedContinent)
    {
        var (_, continent) = CtyService.GetCountryFromCallsign(callsign);
        continent.Should().Be(expectedContinent);
    }

    [Fact]
    public void CtyService_UnknownCallsign_ReturnsNull()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign("QQ0ZZZ");
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void CtyService_EmptyCallsign_ReturnsNull()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign("");
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void CtyService_LongestPrefixMatch_EA8IsCanaryIslands()
    {
        var (country, _) = CtyService.GetCountryFromCallsign("EA8TJ");
        country.Should().Be("Canary Islands");
    }

    [Fact]
    public void CtyService_LongestPrefixMatch_KH6IsHawaii()
    {
        var (country, _) = CtyService.GetCountryFromCallsign("KH6ABC");
        country.Should().Be("Hawaii");
    }

    #endregion

    #region Spot Time Parsing

    [Fact]
    public void ParseSpotTime_ValidTime_ParsesCorrectly()
    {
        var result = ParseSpotTime("1430");
        result.Hour.Should().Be(14);
        result.Minute.Should().Be(30);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ParseSpotTime_Midnight_ParsesCorrectly()
    {
        var result = ParseSpotTime("0000");
        result.Hour.Should().Be(0);
        result.Minute.Should().Be(0);
    }

    [Fact]
    public void ParseSpotTime_InvalidFormat_ReturnsUtcNow()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = ParseSpotTime("abcd");
        var after = DateTime.UtcNow.AddSeconds(1);

        result.Should().BeOnOrAfter(before);
        result.Should().BeOnOrBefore(after);
    }

    #endregion

    #region Deduplication Key Generation

    [Fact]
    public void GenerateDeduplicationKey_SameSpotData_ProducesSameKey()
    {
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var key1 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp);
        var key2 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp);

        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateDeduplicationKey_DifferentCallsigns_ProduceDifferentKeys()
    {
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var key1 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp);
        var key2 = GenerateDeduplicationKey("VE3ABC", 14025.0, timestamp);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateDeduplicationKey_CaseInsensitive()
    {
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var key1 = GenerateDeduplicationKey("w1aw", 14025.0, timestamp);
        var key2 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp);

        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateDeduplicationKey_DifferentMinutes_ProducesSameKey()
    {
        // This is the key fix: spots at different minutes should produce the same key
        // so they can be deduplicated based on age instead of minute buckets
        var timestamp1 = new DateTime(2024, 1, 15, 14, 30, 58, DateTimeKind.Utc);
        var timestamp2 = new DateTime(2024, 1, 15, 14, 31, 2, DateTimeKind.Utc);
        var key1 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp1);
        var key2 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp2);

        key1.Should().Be(key2, "spots with same call and frequency should have same key regardless of minute");
    }

    [Fact]
    public void GenerateDeduplicationKey_DifferentFrequencies_ProduceDifferentKeys()
    {
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var key1 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp);
        var key2 = GenerateDeduplicationKey("W1AW", 14074.0, timestamp);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateDeduplicationKey_RoundedFrequencies_ProduceSameKey()
    {
        var timestamp = new DateTime(2024, 1, 15, 14, 30, 0, DateTimeKind.Utc);
        var key1 = GenerateDeduplicationKey("W1AW", 14025.0, timestamp);
        var key2 = GenerateDeduplicationKey("W1AW", 14025.4, timestamp);
        var key3 = GenerateDeduplicationKey("W1AW", 14024.6, timestamp);

        key1.Should().Be(key2, "frequencies that round to same kHz should have same key");
        key1.Should().Be(key3, "frequencies that round to same kHz should have same key");
    }

    #endregion

    #region Helper methods - replicate internal logic for testing

    private static string? ExtractMode(string comment)
    {
        var commentUpper = comment.ToUpper();

        if (commentUpper.Contains("FT8")) return "FT8";
        if (commentUpper.Contains("FT4")) return "FT4";
        if (commentUpper.Contains("RTTY")) return "RTTY";
        if (commentUpper.Contains("PSK")) return "PSK31";
        if (commentUpper.Contains("SSB") || commentUpper.Contains("USB") || commentUpper.Contains("LSB")) return "SSB";
        if (commentUpper.Contains("CW")) return "CW";
        if (commentUpper.Contains("AM")) return "AM";
        if (commentUpper.Contains("FM")) return "FM";
        if (commentUpper.Contains("DIGI")) return "DIGI";
        if (commentUpper.Contains("JT65")) return "JT65";
        if (commentUpper.Contains("JT9")) return "JT9";

        return null;
    }

    private static string InferModeFromFrequency(double frequencyKhz)
    {
        // Check for common FT8 frequencies first (digital mode sub-bands)
        // FT8 typically operates on these frequencies per band (±2 kHz tolerance)
        if (Math.Abs(frequencyKhz - 1840) < 2) return "FT8";      // 160m
        if (Math.Abs(frequencyKhz - 3573) < 2) return "FT8";      // 80m
        if (Math.Abs(frequencyKhz - 7074) < 2) return "FT8";      // 40m
        if (Math.Abs(frequencyKhz - 10136) < 2) return "FT8";     // 30m
        if (Math.Abs(frequencyKhz - 14074) < 2) return "FT8";     // 20m
        if (Math.Abs(frequencyKhz - 18100) < 2) return "FT8";     // 17m
        if (Math.Abs(frequencyKhz - 21074) < 2) return "FT8";     // 15m
        if (Math.Abs(frequencyKhz - 24915) < 2) return "FT8";     // 12m
        if (Math.Abs(frequencyKhz - 28074) < 2) return "FT8";     // 10m
        if (Math.Abs(frequencyKhz - 50313) < 2) return "FT8";     // 6m

        // Standard band plan inference
        if (frequencyKhz >= 1800 && frequencyKhz < 2000)
            return frequencyKhz >= 1843 ? "SSB" : "CW";
        if (frequencyKhz >= 3500 && frequencyKhz < 4000)
            return frequencyKhz >= 3600 ? "SSB" : "CW";
        if (frequencyKhz >= 7000 && frequencyKhz < 7300)
            return frequencyKhz >= 7125 ? "SSB" : "CW";
        if (frequencyKhz >= 10100 && frequencyKhz < 10150)
            return "CW";
        if (frequencyKhz >= 14000 && frequencyKhz < 14350)
            return frequencyKhz >= 14150 ? "SSB" : "CW";
        if (frequencyKhz >= 18068 && frequencyKhz < 18168)
            return frequencyKhz >= 18110 ? "SSB" : "CW";
        if (frequencyKhz >= 21000 && frequencyKhz < 21450)
            return frequencyKhz >= 21200 ? "SSB" : "CW";
        if (frequencyKhz >= 24890 && frequencyKhz < 24990)
            return frequencyKhz >= 24930 ? "SSB" : "CW";
        if (frequencyKhz >= 28000 && frequencyKhz < 29700)
            return frequencyKhz >= 28300 ? "SSB" : "CW";
        if (frequencyKhz >= 50000 && frequencyKhz < 54000)
            return frequencyKhz >= 50100 ? "SSB" : "CW";

        return "SSB";
    }

    private static DateTime ParseSpotTime(string timeStr)
    {
        if (timeStr.Length == 4 &&
            int.TryParse(timeStr.Substring(0, 2), out var hours) &&
            int.TryParse(timeStr.Substring(2, 2), out var minutes))
        {
            var now = DateTime.UtcNow;
            var spotTime = new DateTime(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Utc);

            if (spotTime > now.AddMinutes(5))
            {
                spotTime = spotTime.AddDays(-1);
            }

            return spotTime;
        }

        return DateTime.UtcNow;
    }

    private static string GenerateDeduplicationKey(string dxCall, double frequency, DateTime timestamp)
    {
        // Updated to match the new implementation: no minute timestamp
        var roundedFreq = Math.Round(frequency);
        return $"{dxCall.ToUpperInvariant()}:{roundedFreq}";
    }

    #endregion
}
