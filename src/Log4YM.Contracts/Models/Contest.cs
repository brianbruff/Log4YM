namespace Log4YM.Contracts.Models;

public class Contest
{
    public string Name { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsLive { get; set; }
    public bool IsStartingSoon { get; set; }
    public string? TimeRemaining { get; set; }
}
