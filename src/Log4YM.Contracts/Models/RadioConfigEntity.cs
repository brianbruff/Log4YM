using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Log4YM.Contracts.Models;

/// <summary>
/// Unified radio configuration entity stored in the radio_configs collection.
/// Supports both Hamlib and TCI radio types via nullable type-specific fields.
/// </summary>
public record RadioConfigEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [BsonIgnoreIfDefault]
    public string? Id { get; init; }

    /// <summary>
    /// Stable radio identifier: "hamlib-{modelId}" or "tci-{host}:{port}"
    /// </summary>
    public string RadioId { get; init; } = "";

    /// <summary>
    /// Discriminator: "hamlib" or "tci"
    /// </summary>
    public string RadioType { get; init; } = "";

    /// <summary>
    /// Display name shown in the UI
    /// </summary>
    public string DisplayName { get; init; } = "";

    // =========================================================================
    // Hamlib-specific fields (null when RadioType == "tci")
    // =========================================================================

    public int? HamlibModelId { get; init; }
    public string? HamlibModelName { get; init; }
    public string? ConnectionType { get; init; }

    // Serial params
    public string? SerialPort { get; init; }
    public int? BaudRate { get; init; }
    public int? DataBits { get; init; }
    public int? StopBits { get; init; }
    public string? FlowControl { get; init; }
    public string? Parity { get; init; }

    // Network params
    public string? Hostname { get; init; }
    public int? NetworkPort { get; init; }

    // PTT
    public string? PttType { get; init; }
    public string? PttPort { get; init; }

    // Polling flags
    public bool? GetFrequency { get; init; }
    public bool? GetMode { get; init; }
    public bool? GetVfo { get; init; }
    public bool? GetPtt { get; init; }
    public bool? GetPower { get; init; }
    public bool? GetRit { get; init; }
    public bool? GetXit { get; init; }
    public bool? GetKeySpeed { get; init; }
    public int? PollIntervalMs { get; init; }

    // =========================================================================
    // TCI-specific fields (null when RadioType == "hamlib")
    // =========================================================================

    public string? TciHost { get; init; }
    public int? TciPort { get; init; }
    public string? TciName { get; init; }

    // =========================================================================
    // Timestamps
    // =========================================================================

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
