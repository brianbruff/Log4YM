using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

/// <summary>
/// Settings for UDP-based integrations (WSJT-X, JTDX, MSHV, GridTracker, Hamlib, etc.)
/// </summary>
public class UdpProviderSettings
{
    [BsonElement("providers")]
    public List<UdpProviderConfig> Providers { get; set; } = new()
    {
        // WSJT-X/JTDX/MSHV (compatible protocol)
        new UdpProviderConfig
        {
            Id = "wsjtx",
            Name = "WSJT-X / JTDX / MSHV",
            Enabled = false,
            Port = 2237,
            SupportsMulticast = true,
            MulticastEnabled = false,
            MulticastAddress = "224.0.0.73",
            MulticastTtl = 1,
            ForwardingEnabled = false,
            ForwardingAddresses = new List<string>(),
            Description = "Digital mode applications (FT8, FT4, JT65, etc.)"
        }
    };
}

/// <summary>
/// Configuration for a single UDP provider
/// </summary>
public class UdpProviderConfig
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("port")]
    public int Port { get; set; }

    [BsonElement("supportsMulticast")]
    public bool SupportsMulticast { get; set; }

    [BsonElement("multicastEnabled")]
    public bool MulticastEnabled { get; set; }

    [BsonElement("multicastAddress")]
    public string MulticastAddress { get; set; } = "224.0.0.73";

    [BsonElement("multicastTtl")]
    public int MulticastTtl { get; set; } = 1;

    [BsonElement("forwardingEnabled")]
    public bool ForwardingEnabled { get; set; }

    [BsonElement("forwardingAddresses")]
    public List<string> ForwardingAddresses { get; set; } = new();

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("customSettings")]
    public Dictionary<string, string> CustomSettings { get; set; } = new();
}
