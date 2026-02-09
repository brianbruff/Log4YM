using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

public class RbnSpot
{
    [BsonElement("callsign")]
    public string Callsign { get; set; } = null!;

    [BsonElement("dx")]
    public string Dx { get; set; } = null!;

    [BsonElement("frequency")]
    public double Frequency { get; set; }

    [BsonElement("band")]
    public string Band { get; set; } = null!;

    [BsonElement("mode")]
    public string Mode { get; set; } = null!;

    [BsonElement("snr")]
    public int? Snr { get; set; }

    [BsonElement("speed")]
    public int? Speed { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    [BsonElement("grid")]
    public string? Grid { get; set; }

    [BsonElement("skimmerLat")]
    public double? SkimmerLat { get; set; }

    [BsonElement("skimmerLon")]
    public double? SkimmerLon { get; set; }

    [BsonElement("skimmerCountry")]
    public string? SkimmerCountry { get; set; }
}
