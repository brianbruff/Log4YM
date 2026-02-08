using Xunit;

namespace Log4YM.Server.Tests.Fixtures;

/// <summary>
/// Shared fixture for QRZ API tests. Makes ONE real API call if QRZ_LIVE_TESTS=true,
/// then shares the result across all tests in the collection.
///
/// Usage:
///   [Collection("QrzLive")]
///   public class QrzLiveTests : IClassFixture&lt;QrzSharedFixture&gt; { ... }
///
/// Environment variables required for live tests:
///   QRZ_LIVE_TESTS=true
///   QRZ_USERNAME=your_username
///   QRZ_PASSWORD=your_password
///   QRZ_API_KEY=your_api_key (optional, for logbook upload tests)
/// </summary>
public class QrzSharedFixture : IAsyncLifetime
{
    /// <summary>Whether live QRZ tests are enabled</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>QRZ session key obtained during initialization</summary>
    public string? SessionKey { get; private set; }

    /// <summary>Raw XML response from a test callsign lookup (W1AW)</summary>
    public string? TestLookupXml { get; private set; }

    /// <summary>QRZ username from env var</summary>
    public string? Username { get; private set; }

    /// <summary>QRZ password from env var</summary>
    public string? Password { get; private set; }

    /// <summary>QRZ API key from env var (for logbook uploads)</summary>
    public string? ApiKey { get; private set; }

    /// <summary>Error message if initialization failed</summary>
    public string? InitError { get; private set; }

    private const string QrzXmlApiUrl = "https://xmldata.qrz.com/xml/current/";
    private const string TestCallsign = "W1AW"; // ARRL HQ - always available

    public async Task InitializeAsync()
    {
        var enabled = Environment.GetEnvironmentVariable("QRZ_LIVE_TESTS");
        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            IsEnabled = false;
            return;
        }

        Username = Environment.GetEnvironmentVariable("QRZ_USERNAME");
        Password = Environment.GetEnvironmentVariable("QRZ_PASSWORD");
        ApiKey = Environment.GetEnvironmentVariable("QRZ_API_KEY");

        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            IsEnabled = false;
            InitError = "QRZ_USERNAME and QRZ_PASSWORD environment variables required for live tests";
            return;
        }

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Log4YM-Tests/1.0");

            // Step 1: Obtain session key
            var loginUrl = $"{QrzXmlApiUrl}?username={Uri.EscapeDataString(Username)}&password={Uri.EscapeDataString(Password)}&agent=Log4YM-Tests";
            var loginResponse = await httpClient.GetStringAsync(loginUrl);

            var doc = System.Xml.Linq.XDocument.Parse(loginResponse);
            var ns = (System.Xml.Linq.XNamespace)"http://xmldata.qrz.com";

            var session = doc.Descendants(ns + "Session").FirstOrDefault()
                ?? doc.Descendants("Session").FirstOrDefault();

            var error = session?.Element(ns + "Error")?.Value ?? session?.Element("Error")?.Value;
            if (!string.IsNullOrEmpty(error))
            {
                IsEnabled = false;
                InitError = $"QRZ login error: {error}";
                return;
            }

            SessionKey = session?.Element(ns + "Key")?.Value ?? session?.Element("Key")?.Value;
            if (string.IsNullOrEmpty(SessionKey))
            {
                IsEnabled = false;
                InitError = "Failed to obtain QRZ session key";
                return;
            }

            // Step 2: Make one test lookup (W1AW) and cache the result
            var lookupUrl = $"{QrzXmlApiUrl}?s={SessionKey}&callsign={TestCallsign}";
            TestLookupXml = await httpClient.GetStringAsync(lookupUrl);

            IsEnabled = true;
        }
        catch (Exception ex)
        {
            IsEnabled = false;
            InitError = $"QRZ fixture initialization failed: {ex.Message}";
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Skip helper - call at the top of each live test method.
    /// Throws SkipException if live tests are not enabled.
    /// </summary>
    public void SkipIfNotEnabled()
    {
        if (!IsEnabled)
        {
            Skip.If(true, InitError ?? "QRZ_LIVE_TESTS environment variable not set to 'true'");
        }
    }
}

/// <summary>
/// Collection definition for QRZ live tests - ensures fixture is shared across test classes.
/// </summary>
[CollectionDefinition("QrzLive")]
public class QrzLiveCollection : ICollectionFixture<QrzSharedFixture>
{
}
