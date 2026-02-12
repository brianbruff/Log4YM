using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

public class CallsignMapImage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("callsign")]
    public string Callsign { get; set; } = null!;

    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = null!;

    [BsonElement("latitude")]
    public double Latitude { get; set; }

    [BsonElement("longitude")]
    public double Longitude { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("grid")]
    public string? Grid { get; set; }

    [BsonElement("savedAt")]
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
