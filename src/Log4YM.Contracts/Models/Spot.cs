namespace Log4YM.Contracts.Models;

public class Spot
{
    public string Id { get; set; } = null!;
    public string DxCall { get; set; } = null!;
    public string Spotter { get; set; } = null!;
    public double Frequency { get; set; }
    public string? Mode { get; set; }
    public string? Comment { get; set; }
    public string? Source { get; set; }
    public DateTime Timestamp { get; set; }
    public SpotStationInfo? DxStation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SpotStationInfo
{
    public string? Country { get; set; }
    public int? Dxcc { get; set; }
    public string? Grid { get; set; }
    public string? Continent { get; set; }
}
