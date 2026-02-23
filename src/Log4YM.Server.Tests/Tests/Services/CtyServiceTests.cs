using FluentAssertions;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class CtyServiceTests
{
    #region Parser

    [Fact]
    public void PrefixCount_LoadsSubstantialPrefixes()
    {
        CtyService.PrefixCount.Should().BeGreaterThan(1000);
    }

    [Fact]
    public void ParseCtyDat_SimpleEntity_ParsesCorrectly()
    {
        var content = """
            Monaco:                   14:  27:  EU:   43.73:    -7.40:    -1.0:  3A:
                3A;
            """;

        var map = CtyService.ParseCtyDat(content);

        map.Should().ContainKey("3A");
        map["3A"].Country.Should().Be("Monaco");
        map["3A"].Continent.Should().Be("EU");
        map["3A"].CqZone.Should().Be(14);
        map["3A"].ItuZone.Should().Be(27);
    }

    [Fact]
    public void ParseCtyDat_MultiplePrefixes_AllMapped()
    {
        var content = """
            Tunisia:                  33:  37:  AF:   35.40:    -9.32:    -1.0:  3V:
                3V,TS;
            """;

        var map = CtyService.ParseCtyDat(content);

        map.Should().ContainKey("3V");
        map.Should().ContainKey("TS");
        map["TS"].Country.Should().Be("Tunisia");
    }

    [Fact]
    public void ParseCtyDat_SkipsExactCallsignMatches()
    {
        var content = """
            Conway Reef:              32:  56:  OC:  -22.00:  -175.00:   -12.0:  3D2/c:
                =3D2CCC;
            """;

        var map = CtyService.ParseCtyDat(content);

        // Primary prefix is added but exact callsign match is skipped
        map.Should().ContainKey("3D2/c");
        map.Should().NotContainKey("3D2CCC");
    }

    [Fact]
    public void ParseCtyDat_StripsAnnotations()
    {
        var content = """
            China:                    24:  44:  AS:   36.00:  -102.00:    -8.0:  BY:
                3H0(23)[42],3H9(23)[43];
            """;

        var map = CtyService.ParseCtyDat(content);

        map.Should().ContainKey("3H0");
        map.Should().ContainKey("3H9");
        map["3H0"].Country.Should().Be("China");
    }

    [Fact]
    public void ParseCtyDat_StripsWaedcStar()
    {
        var content = """
            Vienna Intl Ctr:          15:  28:  EU:   48.20:   -16.30:    -1.0:  *4U1V:
                =4U0IARU;
            """;

        var map = CtyService.ParseCtyDat(content);

        map.Should().ContainKey("4U1V");
        map["4U1V"].Country.Should().Be("Vienna Intl Ctr");
    }

    #endregion

    #region GetCountryFromCallsign — known callsigns

    [Theory]
    [InlineData("W1AW", "United States")]
    [InlineData("K3LR", "United States")]
    [InlineData("N5DX", "United States")]
    [InlineData("VE3ABC", "Canada")]
    [InlineData("DL1ABC", "Fed. Rep. of Germany")]
    [InlineData("G4ABC", "England")]
    [InlineData("M0ABC", "England")]
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
    [InlineData("HL2ABC", "Republic of Korea")]
    [InlineData("CM8ABC", "Cuba")]
    [InlineData("OY1ABC", "Faroe Islands")]
    public void GetCountryFromCallsign_KnownCallsigns_ReturnsCorrectCountry(string callsign, string expectedCountry)
    {
        var (country, _) = CtyService.GetCountryFromCallsign(callsign);
        country.Should().Be(expectedCountry);
    }

    [Theory]
    [InlineData("W1AW", "NA")]
    [InlineData("VE3ABC", "NA")]
    [InlineData("DL1ABC", "EU")]
    [InlineData("JA1YXP", "AS")]
    [InlineData("VK2ABC", "OC")]
    [InlineData("PY2ABC", "SA")]
    [InlineData("ZS6ABC", "AF")]
    public void GetCountryFromCallsign_KnownCallsigns_ReturnsCorrectContinent(string callsign, string expectedContinent)
    {
        var (_, continent) = CtyService.GetCountryFromCallsign(callsign);
        continent.Should().Be(expectedContinent);
    }

    #endregion

    #region GetCountryFromCallsign — longest-prefix matching

    [Fact]
    public void GetCountryFromCallsign_EA8_ReturnsCanaryIslands_NotSpain()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign("EA8TJ");
        country.Should().Be("Canary Islands");
        continent.Should().Be("AF");
    }

    [Fact]
    public void GetCountryFromCallsign_EA1_ReturnsSpain()
    {
        var (country, _) = CtyService.GetCountryFromCallsign("EA1ABC");
        country.Should().Be("Spain");
    }

    [Fact]
    public void GetCountryFromCallsign_KH6_ReturnsHawaii()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign("KH6ABC");
        country.Should().Be("Hawaii");
        continent.Should().Be("OC");
    }

    [Fact]
    public void GetCountryFromCallsign_KL7_ReturnsAlaska()
    {
        var (country, _) = CtyService.GetCountryFromCallsign("KL7ABC");
        country.Should().Be("Alaska");
    }

    #endregion

    #region GetCountryFromCallsign — edge cases

    [Fact]
    public void GetCountryFromCallsign_Null_ReturnsNull()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign(null!);
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void GetCountryFromCallsign_Empty_ReturnsNull()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign("");
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void GetCountryFromCallsign_Unknown_ReturnsNull()
    {
        var (country, continent) = CtyService.GetCountryFromCallsign("QQ0ZZZ");
        country.Should().BeNull();
        continent.Should().BeNull();
    }

    [Fact]
    public void GetCountryFromCallsign_CaseInsensitive()
    {
        var (upper, _) = CtyService.GetCountryFromCallsign("W1AW");
        var (lower, _) = CtyService.GetCountryFromCallsign("w1aw");
        upper.Should().Be(lower);
    }

    #endregion

    #region GetContinentFromCountryName

    [Theory]
    [InlineData("United States", "NA")]
    [InlineData("England", "EU")]
    [InlineData("Japan", "AS")]
    [InlineData("Australia", "OC")]
    [InlineData("Brazil", "SA")]
    [InlineData("South Africa", "AF")]
    public void GetContinentFromCountryName_KnownCountries_ReturnsContinent(string country, string expectedContinent)
    {
        CtyService.GetContinentFromCountryName(country).Should().Be(expectedContinent);
    }

    [Fact]
    public void GetContinentFromCountryName_Null_ReturnsNull()
    {
        CtyService.GetContinentFromCountryName(null!).Should().BeNull();
    }

    [Fact]
    public void GetContinentFromCountryName_Empty_ReturnsNull()
    {
        CtyService.GetContinentFromCountryName("").Should().BeNull();
    }

    [Fact]
    public void GetContinentFromCountryName_Unknown_ReturnsNull()
    {
        CtyService.GetContinentFromCountryName("Narnia").Should().BeNull();
    }

    #endregion
}
