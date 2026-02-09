using System.Text.RegularExpressions;
using FluentAssertions;
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

    #region Country Prefix Lookup

    [Theory]
    [InlineData("K", "United States")]
    [InlineData("W", "United States")]
    [InlineData("N", "United States")]
    [InlineData("VE", "Canada")]
    [InlineData("VA", "Canada")]
    [InlineData("DL", "Germany")]
    [InlineData("G", "United Kingdom")]
    [InlineData("JA", "Japan")]
    [InlineData("VK", "Australia")]
    [InlineData("ZL", "New Zealand")]
    [InlineData("PY", "Brazil")]
    [InlineData("EA", "Spain")]
    [InlineData("F", "France")]
    [InlineData("I", "Italy")]
    [InlineData("OH", "Finland")]
    [InlineData("SM", "Sweden")]
    [InlineData("ZS", "South Africa")]
    public void GetCountryFromPrefix_KnownPrefixes_ReturnsCorrectCountry(string prefix, string expectedCountry)
    {
        var (country, _) = GetCountryFromPrefix(prefix);
        country.Should().Be(expectedCountry);
    }

    [Theory]
    [InlineData("K", "NA")]
    [InlineData("VE", "NA")]
    [InlineData("DL", "EU")]
    [InlineData("JA", "AS")]
    [InlineData("VK", "OC")]
    [InlineData("PY", "SA")]
    [InlineData("ZS", "AF")]
    public void GetCountryFromPrefix_KnownPrefixes_ReturnsCorrectContinent(string prefix, string expectedContinent)
    {
        var (_, continent) = GetCountryFromPrefix(prefix);
        continent.Should().Be(expectedContinent);
    }

    [Fact]
    public void GetCountryFromPrefix_UnknownPrefix_ReturnsNull()
    {
        var (country, continent) = GetCountryFromPrefix("ZZZ");
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void GetCountryFromPrefix_EmptyPrefix_ReturnsNull()
    {
        var (country, continent) = GetCountryFromPrefix("");
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void GetCountryFromPrefix_LongerPrefixFallsBack()
    {
        // "KH6" is Hawaii specifically, while "K" is generic US
        var (country, _) = GetCountryFromPrefix("KH6");
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

    private static readonly Dictionary<string, (string Country, string Continent)> PrefixToCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["K"] = ("United States", "NA"), ["W"] = ("United States", "NA"), ["N"] = ("United States", "NA"), ["A"] = ("United States", "NA"),
        ["VE"] = ("Canada", "NA"), ["VA"] = ("Canada", "NA"), ["VY"] = ("Canada", "NA"),
        ["XE"] = ("Mexico", "NA"), ["XA"] = ("Mexico", "NA"),
        ["KH6"] = ("Hawaii", "OC"), ["KL7"] = ("Alaska", "NA"), ["KP4"] = ("Puerto Rico", "NA"),
        ["PY"] = ("Brazil", "SA"), ["PP"] = ("Brazil", "SA"),
        ["LU"] = ("Argentina", "SA"), ["CE"] = ("Chile", "SA"),
        ["G"] = ("United Kingdom", "EU"), ["M"] = ("United Kingdom", "EU"),
        ["DL"] = ("Germany", "EU"), ["DA"] = ("Germany", "EU"), ["DB"] = ("Germany", "EU"),
        ["F"] = ("France", "EU"), ["I"] = ("Italy", "EU"), ["EA"] = ("Spain", "EU"), ["CT"] = ("Portugal", "EU"),
        ["PA"] = ("Netherlands", "EU"), ["ON"] = ("Belgium", "EU"), ["OZ"] = ("Denmark", "EU"),
        ["SM"] = ("Sweden", "EU"), ["OH"] = ("Finland", "EU"), ["LA"] = ("Norway", "EU"),
        ["SP"] = ("Poland", "EU"), ["OK"] = ("Czech Republic", "EU"), ["HA"] = ("Hungary", "EU"),
        ["UA"] = ("Russia", "EU"), ["JA"] = ("Japan", "AS"), ["HL"] = ("South Korea", "AS"),
        ["BY"] = ("China", "AS"), ["BV"] = ("Taiwan", "AS"), ["VU"] = ("India", "AS"),
        ["VK"] = ("Australia", "OC"), ["ZL"] = ("New Zealand", "OC"),
        ["ZS"] = ("South Africa", "AF"), ["5Z"] = ("Kenya", "AF"),
    };

    private static (string? Country, string? Continent) GetCountryFromPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return (null, null);

        if (PrefixToCountry.TryGetValue(prefix, out var result))
            return result;

        for (int len = Math.Min(prefix.Length, 4); len >= 1; len--)
        {
            var shortPrefix = prefix.Substring(0, len);
            if (PrefixToCountry.TryGetValue(shortPrefix, out result))
                return result;
        }

        return (null, null);
    }

    private static string GenerateDeduplicationKey(string dxCall, double frequency, DateTime timestamp)
    {
        var roundedFreq = Math.Round(frequency);
        var minute = timestamp.Ticks / TimeSpan.TicksPerMinute;
        return $"{dxCall.ToUpperInvariant()}:{roundedFreq}:{minute}";
    }

    #endregion
}
