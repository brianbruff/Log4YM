using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

public class UserSettings
{
    [BsonId]
    public string Id { get; set; } = "default";

    [BsonElement("station")]
    public StationSettings Station { get; set; } = new();

    [BsonElement("qrz")]
    public QrzSettings Qrz { get; set; } = new();

    [BsonElement("appearance")]
    public AppearanceSettings Appearance { get; set; } = new();

    [BsonElement("rotator")]
    public RotatorSettings Rotator { get; set; } = new();

    [BsonElement("radio")]
    public RadioSettings Radio { get; set; } = new();

    [BsonElement("map")]
    public MapSettings Map { get; set; } = new();

    [BsonElement("cluster")]
    public ClusterSettings Cluster { get; set; } = new();

    [BsonElement("layoutJson")]
    public string? LayoutJson { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class StationSettings
{
    [BsonElement("callsign")]
    public string Callsign { get; set; } = string.Empty;

    [BsonElement("operatorName")]
    public string OperatorName { get; set; } = string.Empty;

    [BsonElement("gridSquare")]
    public string GridSquare { get; set; } = string.Empty;

    [BsonElement("latitude")]
    public double? Latitude { get; set; }

    [BsonElement("longitude")]
    public double? Longitude { get; set; }

    [BsonElement("city")]
    public string City { get; set; } = string.Empty;

    [BsonElement("country")]
    public string Country { get; set; } = string.Empty;
}

public class QrzSettings
{
    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("password")]
    public string Password { get; set; } = string.Empty; // Stored obfuscated

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("apiKey")]
    public string ApiKey { get; set; } = string.Empty; // QRZ API key for logbook uploads

    [BsonElement("hasXmlSubscription")]
    public bool? HasXmlSubscription { get; set; } // Cached subscription status

    [BsonElement("subscriptionCheckedAt")]
    public DateTime? SubscriptionCheckedAt { get; set; }
}

public class AppearanceSettings
{
    [BsonElement("theme")]
    public string Theme { get; set; } = "dark";

    [BsonElement("compactMode")]
    public bool CompactMode { get; set; }
}

public class RotatorPreset
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("azimuth")]
    public int Azimuth { get; set; }
}

public class RotatorSettings
{
    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("ipAddress")]
    public string IpAddress { get; set; } = "127.0.0.1";

    [BsonElement("port")]
    public int Port { get; set; } = 4533;  // Default hamlib rotctld port

    [BsonElement("pollingIntervalMs")]
    public int PollingIntervalMs { get; set; } = 500;

    [BsonElement("rotatorId")]
    public string RotatorId { get; set; } = "default";

    [BsonElement("presets")]
    public List<RotatorPreset> Presets { get; set; } = new()
    {
        new RotatorPreset { Name = "N", Azimuth = 0 },
        new RotatorPreset { Name = "E", Azimuth = 90 },
        new RotatorPreset { Name = "S", Azimuth = 180 },
        new RotatorPreset { Name = "W", Azimuth = 270 },
    };
}

[BsonIgnoreExtraElements]
public class RadioSettings
{
    [BsonElement("followRadio")]
    public bool FollowRadio { get; set; } = true;

    [BsonElement("activeRigType")]
    public string? ActiveRigType { get; set; }  // "tci" | "hamlib" | null

    [BsonElement("autoReconnect")]
    public bool AutoReconnect { get; set; } = false;

    [BsonElement("autoConnectRigId")]
    public string? AutoConnectRigId { get; set; }

    [BsonElement("tci")]
    public TciSettings Tci { get; set; } = new();
}

public class TciSettings
{
    [BsonElement("host")]
    public string Host { get; set; } = "localhost";

    [BsonElement("port")]
    public int Port { get; set; } = 50001;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("autoConnect")]
    public bool AutoConnect { get; set; } = false;
}

public class MapSettings
{
    [BsonElement("tileLayer")]
    public string TileLayer { get; set; } = "dark";
}

public class ClusterSettings
{
    [BsonElement("connections")]
    public List<ClusterConnection> Connections { get; set; } = new();
}

public class ClusterConnection
{
    [BsonElement("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("host")]
    public string Host { get; set; } = string.Empty;

    [BsonElement("port")]
    public int Port { get; set; } = 23;

    [BsonElement("callsign")]
    public string? Callsign { get; set; }  // If null, uses station callsign

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;

    [BsonElement("autoReconnect")]
    public bool AutoReconnect { get; set; } = false;
}

public class PluginSettings
{
    [BsonId]
    public string Id { get; set; } = null!;  // Plugin ID

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;

    [BsonExtraElements]
    public BsonDocument? Settings { get; set; }
}

public class Layout
{
    [BsonId]
    public string Id { get; set; } = null!;

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("isDefault")]
    public bool IsDefault { get; set; }

    [BsonElement("layout")]
    public BsonDocument LayoutJson { get; set; } = null!;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
