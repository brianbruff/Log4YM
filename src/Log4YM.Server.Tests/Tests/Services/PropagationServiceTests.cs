using FluentAssertions;
using Log4YM.Server.Services;
using Moq;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Services;

[Trait("Category", "Unit")]
public class PropagationServiceTests
{
    #region SSN from SFI

    [Theory]
    [InlineData(67, 0)]       // Minimum SFI -> SSN=0
    [InlineData(70, 3)]       // Low SFI
    [InlineData(150, 86)]     // Moderate SFI
    [InlineData(200, 137)]    // High SFI
    public void SsnFromSfi_ReturnsExpectedValue(int sfi, int expectedSsn)
    {
        PropagationService.SsnFromSfi(sfi).Should().Be(expectedSsn);
    }

    [Fact]
    public void SsnFromSfi_BelowMinimum_ReturnsZero()
    {
        PropagationService.SsnFromSfi(50).Should().Be(0);
    }

    #endregion

    #region Haversine Distance

    [Theory]
    [InlineData(40.7128, -74.0060, 51.5074, -0.1278, 5570, 100)]  // NYC to London
    [InlineData(35.6762, 139.6503, -33.8688, 151.2093, 7823, 100)] // Tokyo to Sydney
    [InlineData(0, 0, 0, 180, 20015, 100)]                          // Antipodal points (~half circumference)
    public void HaversineDistance_ReturnsExpectedDistance(
        double lat1, double lon1, double lat2, double lon2,
        double expectedKm, double toleranceKm)
    {
        var result = PropagationService.HaversineDistanceKm(lat1, lon1, lat2, lon2);
        result.Should().BeApproximately(expectedKm, toleranceKm);
    }

    [Fact]
    public void HaversineDistance_SamePoint_ReturnsZero()
    {
        PropagationService.HaversineDistanceKm(52.0, -8.0, 52.0, -8.0).Should().Be(0);
    }

    #endregion

    #region Bearing

    [Fact]
    public void Bearing_NorthPole_ReturnsZero()
    {
        var bearing = PropagationService.BearingDeg(0, 0, 90, 0);
        bearing.Should().BeApproximately(0, 1);
    }

    [Fact]
    public void Bearing_East_Returns90()
    {
        var bearing = PropagationService.BearingDeg(0, 0, 0, 90);
        bearing.Should().BeApproximately(90, 1);
    }

    #endregion

    #region Solar Zenith Angle

    [Fact]
    public void SolarZenithAngle_NoonAtEquator_NearEquinox_IsLow()
    {
        // March 20 is near equinox, noon UTC at lon=0 should have low zenith
        var equinox = new DateTime(2025, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        var zenith = PropagationService.SolarZenithAngle(0, 0, equinox);
        zenith.Should().BeLessThan(30);
    }

    [Fact]
    public void SolarZenithAngle_MidnightAtEquator_IsHigh()
    {
        var midnight = new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc);
        var zenith = PropagationService.SolarZenithAngle(0, 0, midnight);
        zenith.Should().BeGreaterThan(90);
    }

    #endregion

    #region MUF Calculations

    [Fact]
    public void Muf_DaytimeHigherThanNighttime()
    {
        int ssn = 80;
        double dist = 5000;
        double midLat = 45, midLon = 0;

        var daytime = new DateTime(2025, 6, 21, 12, 0, 0, DateTimeKind.Utc);
        var nighttime = new DateTime(2025, 6, 21, 0, 0, 0, DateTimeKind.Utc);

        var mufDay = PropagationService.CalculateMuf(ssn, dist, midLat, midLon, daytime);
        var mufNight = PropagationService.CalculateMuf(ssn, dist, midLat, midLon, nighttime);

        mufDay.Should().BeGreaterThan(mufNight);
    }

    [Fact]
    public void Muf_HigherSsn_ProducesHigherMuf()
    {
        double dist = 5000;
        double midLat = 45, midLon = 0;
        var noon = new DateTime(2025, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        var mufLow = PropagationService.CalculateMuf(10, dist, midLat, midLon, noon);
        var mufHigh = PropagationService.CalculateMuf(150, dist, midLat, midLon, noon);

        mufHigh.Should().BeGreaterThan(mufLow);
    }

    [Fact]
    public void Muf_NeverBelowMinimum()
    {
        var midnight = new DateTime(2025, 12, 21, 0, 0, 0, DateTimeKind.Utc);
        var muf = PropagationService.CalculateMuf(0, 1000, 70, 0, midnight);
        muf.Should().BeGreaterOrEqualTo(2.0);
    }

    #endregion

    #region LUF Calculations

    [Fact]
    public void Luf_DaytimeHigherThanNighttime()
    {
        int ssn = 80;
        double dist = 5000;
        double midLat = 45, midLon = 0;

        var daytime = new DateTime(2025, 6, 21, 12, 0, 0, DateTimeKind.Utc);
        var nighttime = new DateTime(2025, 6, 21, 0, 0, 0, DateTimeKind.Utc);

        var lufDay = PropagationService.CalculateLuf(ssn, dist, midLat, midLon, daytime, 2);
        var lufNight = PropagationService.CalculateLuf(ssn, dist, midLat, midLon, nighttime, 2);

        lufDay.Should().BeGreaterThan(lufNight);
    }

    [Fact]
    public void Luf_HighKIndex_IncreasesLuf()
    {
        double dist = 5000;
        double midLat = 45, midLon = 0;
        var noon = new DateTime(2025, 6, 21, 12, 0, 0, DateTimeKind.Utc);

        var lufCalm = PropagationService.CalculateLuf(80, dist, midLat, midLon, noon, 1);
        var lufStorm = PropagationService.CalculateLuf(80, dist, midLat, midLon, noon, 7);

        lufStorm.Should().BeGreaterThan(lufCalm);
    }

    #endregion

    #region Reliability Calculations

    [Fact]
    public void Reliability_AboveMuf_ReturnsZero()
    {
        var rel = PropagationService.CalculateReliability(30.0, 20.0, 5.0, 2, 1);
        rel.Should().Be(0);
    }

    [Fact]
    public void Reliability_BelowLuf_ReturnsZero()
    {
        var rel = PropagationService.CalculateReliability(3.0, 20.0, 5.0, 2, 1);
        rel.Should().Be(0);
    }

    [Fact]
    public void Reliability_AtFot_IsHighest()
    {
        double muf = 20.0;
        double luf = 5.0;
        double fot = muf * 0.85; // 17.0

        var relAtFot = PropagationService.CalculateReliability(fot, muf, luf, 2, 1);
        var relFarFromFot = PropagationService.CalculateReliability(8.0, muf, luf, 2, 1);

        relAtFot.Should().BeGreaterThan(relFarFromFot);
    }

    [Fact]
    public void Reliability_HighKIndex_ReducesReliability()
    {
        double freq = 14.0, muf = 20.0, luf = 5.0;

        var relCalm = PropagationService.CalculateReliability(freq, muf, luf, 1, 1);
        var relStorm = PropagationService.CalculateReliability(freq, muf, luf, 7, 1);

        relStorm.Should().BeLessThan(relCalm);
    }

    [Fact]
    public void Reliability_MultiHop_ReducesReliability()
    {
        double freq = 14.0, muf = 20.0, luf = 5.0;

        var relSingleHop = PropagationService.CalculateReliability(freq, muf, luf, 2, 1);
        var relMultiHop = PropagationService.CalculateReliability(freq, muf, luf, 2, 3);

        relMultiHop.Should().BeLessThan(relSingleHop);
    }

    [Fact]
    public void Reliability_NeverExceeds99()
    {
        // Test with ideal conditions
        double muf = 20.0;
        double fot = muf * 0.85;
        var rel = PropagationService.CalculateReliability(fot, muf, 3.0, 0, 1);
        rel.Should().BeLessOrEqualTo(99);
    }

    #endregion

    #region Full Prediction

    [Fact]
    public async Task PredictAsync_ReturnsCorrectDimensions()
    {
        var mockWeather = new Mock<ISpaceWeatherService>();
        mockWeather.Setup(s => s.GetCurrentAsync())
            .ReturnsAsync(new SpaceWeatherData(120, 2, 55, DateTime.UtcNow));

        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var mockLogger = new Mock<ILogger<PropagationService>>();

        var service = new PropagationService(mockWeather.Object, mockHttpFactory.Object, mockLogger.Object);
        var result = await service.PredictAsync(40.7, -74.0, 51.5, -0.1);

        result.HeatmapData.Should().HaveCount(9);
        result.HeatmapData[0].Should().HaveCount(24);
        result.BandNames.Should().HaveCount(9);
        result.CurrentBands.Should().HaveCount(9);
        result.MufMHz.Should().BeGreaterThan(0);
        result.LufMHz.Should().BeGreaterThan(0);
        result.DistanceKm.Should().BeApproximately(5570, 100);
        result.BearingDeg.Should().BeInRange(0, 360);
    }

    [Fact]
    public async Task PredictAsync_AllReliabilityValuesInRange()
    {
        var mockWeather = new Mock<ISpaceWeatherService>();
        mockWeather.Setup(s => s.GetCurrentAsync())
            .ReturnsAsync(new SpaceWeatherData(150, 3, 86, DateTime.UtcNow));

        var mockHttpFactory = new Mock<IHttpClientFactory>();
        mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient());

        var mockLogger = new Mock<ILogger<PropagationService>>();

        var service = new PropagationService(mockWeather.Object, mockHttpFactory.Object, mockLogger.Object);
        var result = await service.PredictAsync(52.67, -8.63, 35.68, 139.65);

        foreach (var bandData in result.HeatmapData)
        {
            foreach (var rel in bandData)
            {
                rel.Should().BeInRange(0, 99);
            }
        }

        foreach (var band in result.CurrentBands)
        {
            band.Reliability.Should().BeInRange(0, 99);
            band.Status.Should().BeOneOf("EXCELLENT", "GOOD", "FAIR", "POOR", "CLOSED");
        }
    }

    #endregion
}
