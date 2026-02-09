using FluentAssertions;
using Log4YM.Server.Services;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class BandHelperTests
{
    [Theory]
    [InlineData(1_800_000, "160m")]
    [InlineData(1_900_000, "160m")]
    [InlineData(1_999_999, "160m")]
    [InlineData(3_500_000, "80m")]
    [InlineData(3_750_000, "80m")]
    [InlineData(3_999_999, "80m")]
    [InlineData(5_330_500, "60m")]
    [InlineData(5_400_000, "60m")]
    [InlineData(7_000_000, "40m")]
    [InlineData(7_150_000, "40m")]
    [InlineData(7_299_999, "40m")]
    [InlineData(10_100_000, "30m")]
    [InlineData(10_125_000, "30m")]
    [InlineData(10_150_000, "30m")]
    [InlineData(14_000_000, "20m")]
    [InlineData(14_175_000, "20m")]
    [InlineData(14_350_000, "20m")]
    [InlineData(18_068_000, "17m")]
    [InlineData(18_100_000, "17m")]
    [InlineData(18_168_000, "17m")]
    [InlineData(21_000_000, "15m")]
    [InlineData(21_225_000, "15m")]
    [InlineData(21_450_000, "15m")]
    [InlineData(24_890_000, "12m")]
    [InlineData(24_940_000, "12m")]
    [InlineData(24_990_000, "12m")]
    [InlineData(28_000_000, "10m")]
    [InlineData(28_500_000, "10m")]
    [InlineData(29_700_000, "10m")]
    [InlineData(50_000_000, "6m")]
    [InlineData(52_000_000, "6m")]
    [InlineData(54_000_000, "6m")]
    [InlineData(144_000_000, "2m")]
    [InlineData(146_000_000, "2m")]
    [InlineData(148_000_000, "2m")]
    [InlineData(420_000_000, "70cm")]
    [InlineData(435_000_000, "70cm")]
    [InlineData(450_000_000, "70cm")]
    public void GetBand_ReturnsCorrectBand(long frequencyHz, string expectedBand)
    {
        BandHelper.GetBand(frequencyHz).Should().Be(expectedBand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500_000)]
    [InlineData(2_500_000)]
    [InlineData(5_000_000)]
    [InlineData(100_000_000)]
    [InlineData(500_000_000)]
    [InlineData(1_000_000_000)]
    public void GetBand_ReturnsUnknown_ForOutOfBandFrequencies(long frequencyHz)
    {
        BandHelper.GetBand(frequencyHz).Should().Be("Unknown");
    }

    [Theory]
    [InlineData(1_800_001, true)]
    [InlineData(14_200_000, true)]
    [InlineData(0, false)]
    [InlineData(100_000_000, false)]
    public void IsAmateurBand_ReturnsExpected(long frequencyHz, bool expected)
    {
        BandHelper.IsAmateurBand(frequencyHz).Should().Be(expected);
    }

    [Theory]
    [InlineData(14.2, "20m")]
    [InlineData(7.1, "40m")]
    [InlineData(3.7, "80m")]
    [InlineData(21.3, "15m")]
    [InlineData(28.5, "10m")]
    [InlineData(144.3, "2m")]
    public void GetBandFromMhz_ReturnsCorrectBand(double frequencyMhz, string expectedBand)
    {
        BandHelper.GetBandFromMhz(frequencyMhz).Should().Be(expectedBand);
    }

    [Fact]
    public void GetBand_LowerBoundary_160m()
    {
        BandHelper.GetBand(1_800_000).Should().Be("160m");
    }

    [Fact]
    public void GetBand_UpperBoundary_160m()
    {
        BandHelper.GetBand(2_000_000).Should().Be("160m");
    }

    [Fact]
    public void GetBand_JustBelow160m_ReturnsUnknown()
    {
        BandHelper.GetBand(1_799_999).Should().Be("Unknown");
    }

    [Fact]
    public void GetBand_JustAbove160m_ReturnsUnknown()
    {
        BandHelper.GetBand(2_000_001).Should().Be("Unknown");
    }

    [Fact]
    public void GetBand_GapBetween80mAnd60m_ReturnsUnknown()
    {
        BandHelper.GetBand(5_000_000).Should().Be("Unknown");
    }

    [Fact]
    public void GetBand_NegativeFrequency_ReturnsUnknown()
    {
        BandHelper.GetBand(-14_000_000).Should().Be("Unknown");
    }
}
