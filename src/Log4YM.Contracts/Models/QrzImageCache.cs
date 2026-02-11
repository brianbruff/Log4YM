using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

/// <summary>
/// Cached QRZ profile image for a callsign
/// Used to avoid repeated API requests to qrz.com
/// </summary>
public class QrzImageCache
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    /// <summary>
    /// Callsign (uppercase, normalized)
    /// </summary>
    [BsonElement("callsign")]
    public string Callsign { get; set; } = null!;

    /// <summary>
    /// URL to the QRZ profile image
    /// </summary>
    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// When this image was fetched from QRZ
    /// </summary>
    [BsonElement("fetchedAt")]
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this cache entry was last accessed
    /// Used for LRU eviction
    /// </summary>
    [BsonElement("lastAccessedAt")]
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Full name from QRZ (for display on map)
    /// </summary>
    [BsonElement("name")]
    public string? Name { get; set; }

    /// <summary>
    /// Latitude from QRZ (for map display)
    /// </summary>
    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude from QRZ (for map display)
    /// </summary>
    [BsonElement("longitude")]
    public double? Longitude { get; set; }

    /// <summary>
    /// Grid square from QRZ
    /// </summary>
    [BsonElement("grid")]
    public string? Grid { get; set; }
}
