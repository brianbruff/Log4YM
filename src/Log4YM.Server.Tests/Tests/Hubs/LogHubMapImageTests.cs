using FluentAssertions;
using Xunit;

namespace Log4YM.Server.Tests.Tests.Hubs;

/// <summary>
/// Tests for the map image persistence guard logic in LogHub.
/// The guard determines which callsigns are saved to MongoDB for map overlay.
/// Since the guard is in a private method context, we replicate the logic here.
/// </summary>
[Trait("Category", "Unit")]
public class LogHubMapImageTests
{
    #region Save Guard Logic

    /// <summary>
    /// Determines whether a callsign should be saved to MongoDB for map overlay.
    /// Replicated from LogHub.FocusCallsign (line 182):
    ///   if (info.Latitude.HasValue && info.Longitude.HasValue)
    /// Previously also required: !string.IsNullOrEmpty(info.ImageUrl)
    /// </summary>
    private static bool ShouldSaveToMongo(double? latitude, double? longitude, string? imageUrl)
    {
        // Current logic: only requires lat/lon, NOT imageUrl
        return latitude.HasValue && longitude.HasValue;
    }

    [Fact]
    public void ShouldSave_WithImageAndLatLon_ReturnsTrue()
    {
        ShouldSaveToMongo(41.7128, -72.7060, "https://example.com/photo.jpg")
            .Should().BeTrue("callsigns with image and coordinates should be persisted");
    }

    [Fact]
    public void ShouldSave_WithoutImage_WithLatLon_ReturnsTrue()
    {
        ShouldSaveToMongo(35.6762, 139.6503, null)
            .Should().BeTrue("callsigns without image but with coordinates should now be persisted");
    }

    [Fact]
    public void ShouldSave_WithEmptyImageUrl_WithLatLon_ReturnsTrue()
    {
        ShouldSaveToMongo(52.6667, -8.6333, "")
            .Should().BeTrue("callsigns with empty imageUrl but with coordinates should be persisted");
    }

    [Fact]
    public void ShouldNotSave_WithoutLatitude_ReturnsFalse()
    {
        ShouldSaveToMongo(null, -72.7060, "https://example.com/photo.jpg")
            .Should().BeFalse("callsigns without latitude should not be persisted");
    }

    [Fact]
    public void ShouldNotSave_WithoutLongitude_ReturnsFalse()
    {
        ShouldSaveToMongo(41.7128, null, "https://example.com/photo.jpg")
            .Should().BeFalse("callsigns without longitude should not be persisted");
    }

    [Fact]
    public void ShouldNotSave_WithoutLatLon_ReturnsFalse()
    {
        ShouldSaveToMongo(null, null, null)
            .Should().BeFalse("callsigns without any coordinates should not be persisted");
    }

    [Fact]
    public void ShouldSave_WithZeroCoordinates_ReturnsTrue()
    {
        // Zero coordinates are valid (e.g., Gulf of Guinea)
        ShouldSaveToMongo(0.0, 0.0, null)
            .Should().BeTrue("zero coordinates are valid and should be persisted");
    }

    [Fact]
    public void ShouldSave_WithNegativeCoordinates_ReturnsTrue()
    {
        // Southern/Western hemisphere coordinates
        ShouldSaveToMongo(-33.8688, -151.2093, "https://example.com/vk2.jpg")
            .Should().BeTrue("negative coordinates are valid");
    }

    #endregion

    #region CallsignMapImage Model

    /// <summary>
    /// Test that the model allows null ImageUrl.
    /// Replicating the model shape since it's in a separate assembly.
    /// </summary>
    [Fact]
    public void CallsignMapImage_AllowsNullImageUrl()
    {
        var image = new TestCallsignMapImage
        {
            Callsign = "W1AW",
            ImageUrl = null,
            Latitude = 41.7128,
            Longitude = -72.7060,
        };

        image.ImageUrl.Should().BeNull("ImageUrl should be nullable for callsigns without QRZ pictures");
        image.Callsign.Should().Be("W1AW");
        image.Latitude.Should().Be(41.7128);
        image.Longitude.Should().Be(-72.7060);
    }

    [Fact]
    public void CallsignMapImage_AcceptsImageUrl()
    {
        var image = new TestCallsignMapImage
        {
            Callsign = "EI2KC",
            ImageUrl = "https://example.com/ei2kc.jpg",
            Latitude = 52.6667,
            Longitude = -8.6333,
        };

        image.ImageUrl.Should().Be("https://example.com/ei2kc.jpg");
    }

    [Fact]
    public void CallsignMapImage_DefaultSavedAtIsUtcNow()
    {
        var before = DateTime.UtcNow;
        var image = new TestCallsignMapImage
        {
            Callsign = "W1AW",
            Latitude = 41.7,
            Longitude = -72.7,
        };
        var after = DateTime.UtcNow;

        image.SavedAt.Should().BeOnOrAfter(before);
        image.SavedAt.Should().BeOnOrBefore(after);
    }

    #endregion

    #region MongoDB Update Builder Logic

    /// <summary>
    /// Tests the MongoDB update field logic for SaveCallsignMapImageAsync.
    /// Verifies that the update correctly handles null imageUrl.
    /// </summary>
    [Fact]
    public void UpdateFields_WithImage_SetsAllFields()
    {
        var info = new TestQrzInfo
        {
            Callsign = "W1AW",
            ImageUrl = "https://example.com/w1aw.jpg",
            Latitude = 41.7128,
            Longitude = -72.7060,
            FirstName = "ARRL",
            Name = "Headquarters",
            Country = "United States",
            Grid = "FN31pr",
        };

        var fields = BuildUpdateFields(info);
        fields.Callsign.Should().Be("W1AW");
        fields.ImageUrl.Should().Be("https://example.com/w1aw.jpg");
        fields.Latitude.Should().Be(41.7128);
        fields.Longitude.Should().Be(-72.7060);
        fields.Name.Should().Be("ARRL Headquarters");
        fields.Country.Should().Be("United States");
        fields.Grid.Should().Be("FN31pr");
    }

    [Fact]
    public void UpdateFields_WithoutImage_SetsNullImageUrl()
    {
        var info = new TestQrzInfo
        {
            Callsign = "JA1ABC",
            ImageUrl = null,
            Latitude = 35.6762,
            Longitude = 139.6503,
            FirstName = "Taro",
            Name = "Yamada",
            Country = "Japan",
            Grid = "PM95",
        };

        var fields = BuildUpdateFields(info);
        fields.Callsign.Should().Be("JA1ABC");
        fields.ImageUrl.Should().BeNull("ImageUrl should be null for callsigns without pictures");
        fields.Latitude.Should().Be(35.6762);
        fields.Longitude.Should().Be(139.6503);
        fields.Name.Should().Be("Taro Yamada");
    }

    [Fact]
    public void UpdateFields_WithEmptyImageUrl_SetsEmptyString()
    {
        var info = new TestQrzInfo
        {
            Callsign = "VK3ABC",
            ImageUrl = "",
            Latitude = -37.8,
            Longitude = 144.9,
        };

        var fields = BuildUpdateFields(info);
        fields.ImageUrl.Should().Be("");
    }

    #endregion

    #region Test helpers

    /// <summary>
    /// Simulates the fields that SaveCallsignMapImageAsync would set in the MongoDB update.
    /// Replicated from LogHub.cs SaveCallsignMapImageAsync (lines 280-297).
    /// </summary>
    private static TestCallsignMapImage BuildUpdateFields(TestQrzInfo info)
    {
        return new TestCallsignMapImage
        {
            Callsign = info.Callsign,
            ImageUrl = info.ImageUrl,
            Latitude = info.Latitude!.Value,
            Longitude = info.Longitude!.Value,
            Name = BuildFullName(info.FirstName, info.Name),
            Country = info.Country,
            Grid = info.Grid,
            SavedAt = DateTime.UtcNow,
        };
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

    private class TestCallsignMapImage
    {
        public string Callsign { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? Name { get; set; }
        public string? Country { get; set; }
        public string? Grid { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }

    private class TestQrzInfo
    {
        public string Callsign { get; set; } = null!;
        public string? ImageUrl { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? FirstName { get; set; }
        public string? Name { get; set; }
        public string? Country { get; set; }
        public string? Grid { get; set; }
    }

    #endregion
}
