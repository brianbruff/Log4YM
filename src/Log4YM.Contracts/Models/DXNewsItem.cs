namespace Log4YM.Contracts.Models;

public class DXNewsItem
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Link { get; set; } = null!;
    public DateTime PublishedDate { get; set; }
}
