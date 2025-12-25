using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

/// <summary>
/// Sync status for external services (QRZ, LoTW, etc.)
/// Follows QLog's Y/N/M pattern for efficient incremental sync
/// </summary>
public enum SyncStatus
{
    /// <summary>Not yet synced to the service</summary>
    NotSynced = 0,
    /// <summary>Successfully synced, no changes since</summary>
    Synced = 1,
    /// <summary>Was synced but has been modified since - needs re-sync</summary>
    Modified = 2
}

public class Qso
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("call")]
    public string Callsign { get; set; } = null!;

    [BsonElement("qso_datetime")]
    public DateTime QsoDate { get; set; }

    [BsonElement("time_on")]
    public string TimeOn { get; set; } = null!;

    [BsonElement("time_off")]
    public string? TimeOff { get; set; }

    [BsonElement("band")]
    public string Band { get; set; } = null!;

    [BsonElement("mode")]
    public string Mode { get; set; } = null!;

    [BsonElement("freq")]
    public double? Frequency { get; set; }

    [BsonElement("rst_sent")]
    public string? RstSent { get; set; }

    [BsonElement("rst_rcvd")]
    public string? RstRcvd { get; set; }

    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("gridsquare")]
    public string? Grid { get; set; }

    [BsonElement("dxcc")]
    public int? Dxcc { get; set; }

    [BsonElement("cont")]
    public string? Continent { get; set; }

    [BsonElement("station")]
    public StationInfo? Station { get; set; }

    [BsonElement("qsl")]
    public QslStatus? Qsl { get; set; }

    [BsonElement("contest")]
    public ContestInfo? Contest { get; set; }

    [BsonElement("comment")]
    public string? Comment { get; set; }

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonExtraElements]
    public BsonDocument? AdifExtra { get; set; }

    [BsonElement("imported_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // QRZ sync tracking
    [BsonElement("qrzLogId")]
    public string? QrzLogId { get; set; }

    [BsonElement("qrzSyncedAt")]
    public DateTime? QrzSyncedAt { get; set; }

    [BsonElement("qrzSyncStatus")]
    [BsonRepresentation(BsonType.String)]
    public SyncStatus QrzSyncStatus { get; set; } = SyncStatus.NotSynced;
}

public class StationInfo
{
    [BsonElement("name")]
    public string? Name { get; set; }

    [BsonElement("qth")]
    public string? Qth { get; set; }

    [BsonElement("grid")]
    public string? Grid { get; set; }

    [BsonElement("country")]
    public string? Country { get; set; }

    [BsonElement("dxcc")]
    public int? Dxcc { get; set; }

    [BsonElement("cqZone")]
    public int? CqZone { get; set; }

    [BsonElement("ituZone")]
    public int? ItuZone { get; set; }

    [BsonElement("state")]
    public string? State { get; set; }

    [BsonElement("county")]
    public string? County { get; set; }

    [BsonElement("continent")]
    public string? Continent { get; set; }

    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }
}

public class QslStatus
{
    [BsonElement("sent")]
    public string? Sent { get; set; }

    [BsonElement("sentDate")]
    public DateTime? SentDate { get; set; }

    [BsonElement("rcvd")]
    public string? Rcvd { get; set; }

    [BsonElement("rcvdDate")]
    public DateTime? RcvdDate { get; set; }

    [BsonElement("lotw")]
    public LotwStatus? Lotw { get; set; }

    [BsonElement("eqsl")]
    public EqslStatus? Eqsl { get; set; }
}

public class LotwStatus
{
    [BsonElement("sent")]
    public string? Sent { get; set; }

    [BsonElement("rcvd")]
    public string? Rcvd { get; set; }
}

public class EqslStatus
{
    [BsonElement("sent")]
    public string? Sent { get; set; }

    [BsonElement("rcvd")]
    public string? Rcvd { get; set; }
}

public class ContestInfo
{
    [BsonElement("id")]
    public string? ContestId { get; set; }

    [BsonElement("serialSent")]
    public string? SerialSent { get; set; }

    [BsonElement("serialRcvd")]
    public string? SerialRcvd { get; set; }

    [BsonElement("exchange")]
    public string? Exchange { get; set; }
}
