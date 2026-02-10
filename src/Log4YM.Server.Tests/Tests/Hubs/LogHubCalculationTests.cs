using FluentAssertions;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Hubs;

/// <summary>
/// Tests for the bearing and distance calculation methods in LogHub.
/// These are static methods that implement the Haversine formula.
/// Since they are private, we replicate the logic here for testing.
/// </summary>
[Trait("Category", "Unit")]
public class LogHubCalculationTests
{
    #region Bearing Calculations

    [Fact]
    public void CalculateBearing_DueNorth_Returns0()
    {
        // From equator to north pole
        var bearing = CalculateBearing(0, 0, 90, 0);
        bearing.Should().BeApproximately(0, 0.1);
    }

    [Fact]
    public void CalculateBearing_DueSouth_Returns180()
    {
        // From north to south along same meridian
        var bearing = CalculateBearing(45, 0, -45, 0);
        bearing.Should().BeApproximately(180, 0.1);
    }

    [Fact]
    public void CalculateBearing_DueEast_Returns90()
    {
        // Due east along equator
        var bearing = CalculateBearing(0, 0, 0, 90);
        bearing.Should().BeApproximately(90, 0.1);
    }

    [Fact]
    public void CalculateBearing_DueWest_Returns270()
    {
        // Due west along equator
        var bearing = CalculateBearing(0, 0, 0, -90);
        bearing.Should().BeApproximately(270, 0.1);
    }

    [Fact]
    public void CalculateBearing_NewYorkToLondon()
    {
        // NYC (40.7128, -74.0060) to London (51.5074, -0.1278)
        var bearing = CalculateBearing(40.7128, -74.0060, 51.5074, -0.1278);
        // Expected: roughly 51 degrees (northeast)
        bearing.Should().BeInRange(48, 55);
    }

    [Fact]
    public void CalculateBearing_LondonToNewYork()
    {
        // London to NYC - roughly 288 degrees (west-northwest)
        var bearing = CalculateBearing(51.5074, -0.1278, 40.7128, -74.0060);
        bearing.Should().BeInRange(285, 295);
    }

    [Fact]
    public void CalculateBearing_TokyoToSydney()
    {
        // Tokyo (35.6762, 139.6503) to Sydney (-33.8688, 151.2093)
        var bearing = CalculateBearing(35.6762, 139.6503, -33.8688, 151.2093);
        // Expected: roughly 170-180 degrees (south-southeast)
        bearing.Should().BeInRange(165, 185);
    }

    [Fact]
    public void CalculateBearing_SamePoint_HandlesGracefully()
    {
        // Same point should return some bearing (0 or undefined, but not crash)
        var bearing = CalculateBearing(40.0, -74.0, 40.0, -74.0);
        bearing.Should().BeInRange(0, 360);
    }

    [Fact]
    public void CalculateBearing_AlwaysReturns0To360()
    {
        // Test many random-ish points to confirm range
        var testCases = new[]
        {
            (0.0, 0.0, 90.0, 180.0),
            (-45.0, -170.0, 45.0, 170.0),
            (89.0, -179.0, -89.0, 179.0),
            (51.5, -0.1, -33.9, 151.2),
        };

        foreach (var (lat1, lon1, lat2, lon2) in testCases)
        {
            var bearing = CalculateBearing(lat1, lon1, lat2, lon2);
            bearing.Should().BeGreaterOrEqualTo(0);
            bearing.Should().BeLessThan(360);
        }
    }

    #endregion

    #region Distance Calculations

    [Fact]
    public void CalculateDistance_SamePoint_ReturnsZero()
    {
        var distance = CalculateDistance(40.0, -74.0, 40.0, -74.0);
        distance.Should().BeApproximately(0, 0.01);
    }

    [Fact]
    public void CalculateDistance_NewYorkToLondon()
    {
        // NYC to London is approximately 5570 km
        var distance = CalculateDistance(40.7128, -74.0060, 51.5074, -0.1278);
        distance.Should().BeInRange(5500, 5600);
    }

    [Fact]
    public void CalculateDistance_Antipodal_ReturnsHalfCircumference()
    {
        // Two opposite points on earth should be ~20,000 km apart (half circumference)
        var distance = CalculateDistance(0, 0, 0, 180);
        distance.Should().BeInRange(19800, 20100);
    }

    [Fact]
    public void CalculateDistance_EquatorOneDegree()
    {
        // One degree of longitude at equator is approximately 111 km
        var distance = CalculateDistance(0, 0, 0, 1);
        distance.Should().BeInRange(110, 112);
    }

    [Fact]
    public void CalculateDistance_SydneyToTokyo()
    {
        // Sydney to Tokyo is approximately 7800 km
        var distance = CalculateDistance(-33.8688, 151.2093, 35.6762, 139.6503);
        distance.Should().BeInRange(7750, 7850);
    }

    [Fact]
    public void CalculateDistance_IsAlwaysPositive()
    {
        // Distance is always >= 0
        var distance = CalculateDistance(45, -120, -30, 80);
        distance.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void CalculateDistance_IsSymmetric()
    {
        // Distance from A to B should equal distance from B to A
        var d1 = CalculateDistance(40.7128, -74.0060, 51.5074, -0.1278);
        var d2 = CalculateDistance(51.5074, -0.1278, 40.7128, -74.0060);
        d1.Should().BeApproximately(d2, 0.001);
    }

    #endregion

    #region Coordinate Normalization

    [Theory]
    [InlineData(40.7128, true, 40.7128)]    // Normal latitude
    [InlineData(-33.8688, true, -33.8688)]   // Negative latitude
    [InlineData(0.0, true, 0.0)]             // Zero
    [InlineData(90.0, true, 90.0)]           // Max latitude
    [InlineData(-90.0, true, -90.0)]         // Min latitude
    public void NormalizeCoordinate_ValidLatitude_ReturnsAsIs(double value, bool isLatitude, double expected)
    {
        var result = NormalizeCoordinate(value, isLatitude);
        result.Should().BeApproximately(expected, 0.0001);
    }

    [Theory]
    [InlineData(180.0, false, 180.0)]        // Max longitude
    [InlineData(-180.0, false, -180.0)]      // Min longitude
    [InlineData(139.6503, false, 139.6503)]  // Normal longitude
    public void NormalizeCoordinate_ValidLongitude_ReturnsAsIs(double value, bool isLatitude, double expected)
    {
        var result = NormalizeCoordinate(value, isLatitude);
        result.Should().BeApproximately(expected, 0.0001);
    }

    [Theory]
    [InlineData(40712800.0, true, 40.7128)]         // Microdegree latitude
    [InlineData(-33868800.0, true, -33.8688)]        // Negative microdegree latitude
    [InlineData(139650300.0, false, 139.6503)]       // Microdegree longitude
    public void NormalizeCoordinate_Microdegrees_ConvertsCorrectly(double value, bool isLatitude, double expected)
    {
        var result = NormalizeCoordinate(value, isLatitude);
        result.Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public void NormalizeCoordinate_InvalidLargeValue_ReturnsNull()
    {
        // A value that's too large even for microdegrees
        var result = NormalizeCoordinate(999_999_999_999.0, true);
        result.Should().BeNull();
    }

    #endregion

    #region BuildFullName Helper

    [Fact]
    public void BuildFullName_BothNames_CombinesThem()
    {
        BuildFullName("John", "Smith").Should().Be("John Smith");
    }

    [Fact]
    public void BuildFullName_FirstNameOnly()
    {
        BuildFullName("John", null).Should().Be("John");
    }

    [Fact]
    public void BuildFullName_LastNameOnly()
    {
        BuildFullName(null, "Smith").Should().Be("Smith");
    }

    [Fact]
    public void BuildFullName_NeitherName_ReturnsNull()
    {
        BuildFullName(null, null).Should().BeNull();
    }

    [Fact]
    public void BuildFullName_EmptyStrings_ReturnsNull()
    {
        BuildFullName("", "").Should().BeNull();
    }

    [Fact]
    public void BuildFullName_WhitespaceStrings_ReturnsNull()
    {
        BuildFullName("  ", "  ").Should().BeNull();
    }

    #endregion

    #region Replicated static methods from LogHub

    private static double CalculateBearing(double lat1, double lon1, double lat2, double lon2)
    {
        var dLon = ToRadians(lon2 - lon1);
        var lat1Rad = ToRadians(lat1);
        var lat2Rad = ToRadians(lat2);

        var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) - Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

        var bearing = Math.Atan2(y, x);
        return (ToDegrees(bearing) + 360) % 360;
    }

    private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double? NormalizeCoordinate(double value, bool isLatitude)
    {
        var maxValid = isLatitude ? 90.0 : 180.0;

        if (Math.Abs(value) <= maxValid)
        {
            return value;
        }

        var normalized = value / 1_000_000.0;
        if (Math.Abs(normalized) <= maxValid)
        {
            return normalized;
        }

        return null;
    }

    private static string? BuildFullName(string? firstName, string? lastName)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(firstName);
        var hasLast = !string.IsNullOrWhiteSpace(lastName);

        if (hasFirst && hasLast)
            return $"{firstName} {lastName}";
        if (hasFirst)
            return firstName;
        if (hasLast)
            return lastName;
        return null;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
    private static double ToDegrees(double radians) => radians * 180 / Math.PI;

    #endregion
}
