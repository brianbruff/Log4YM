using System.ServiceModel.Syndication;
using System.Xml;
using Log4YM.Contracts.Models;

namespace Log4YM.Server.Services;

public interface IDXNewsService
{
    Task<List<DXNewsItem>> GetNewsAsync();
}

public class DXNewsService : IDXNewsService
{
    private readonly ILogger<DXNewsService> _logger;
    private readonly HttpClient _httpClient;
    private List<DXNewsItem>? _cachedNews;
    private DateTime _lastFetch = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private const string DXNewsRssUrl = "https://www.dxnews.com/feed/";

    public DXNewsService(ILogger<DXNewsService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<List<DXNewsItem>> GetNewsAsync()
    {
        try
        {
            // Return cached data if still valid
            if (_cachedNews != null && DateTime.UtcNow - _lastFetch < CacheDuration)
            {
                _logger.LogDebug("Returning cached DX news data");
                return _cachedNews;
            }

            _logger.LogInformation("Fetching DX news from {Url}", DXNewsRssUrl);

            using var stream = await _httpClient.GetStreamAsync(DXNewsRssUrl);
            using var xmlReader = XmlReader.Create(stream);

            var feed = SyndicationFeed.Load(xmlReader);
            var news = new List<DXNewsItem>();

            foreach (var item in feed.Items.Take(20)) // Limit to 20 most recent items
            {
                var newsItem = new DXNewsItem
                {
                    Title = item.Title?.Text ?? "No Title",
                    Description = item.Summary?.Text ?? "",
                    Link = item.Links.FirstOrDefault()?.Uri.ToString() ?? "",
                    PublishedDate = item.PublishDate.UtcDateTime
                };
                news.Add(newsItem);
            }

            _cachedNews = news;
            _lastFetch = DateTime.UtcNow;

            _logger.LogInformation("Successfully fetched {Count} DX news items", news.Count);
            return news;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching DX news from {Url}", DXNewsRssUrl);

            // Return cached data if available, even if expired
            if (_cachedNews != null)
            {
                _logger.LogWarning("Returning stale cached DX news due to fetch error");
                return _cachedNews;
            }

            // Return empty list if no cache available
            return new List<DXNewsItem>();
        }
    }
}
