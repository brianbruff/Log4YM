using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public class QrzService : IQrzService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<QrzService> _logger;

    private const string QrzXmlApiUrl = "https://xmldata.qrz.com/xml/current/";
    private const string QrzLogbookApiUrl = "https://logbook.qrz.com/api";

    private string? _sessionKey;
    private DateTime? _sessionExpiry;

    public QrzService(
        ISettingsRepository settingsRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<QrzService> logger)
    {
        _settingsRepository = settingsRepository;
        _httpClient = httpClientFactory.CreateClient("QRZ");
        _logger = logger;
    }

    public async Task<QrzSubscriptionStatus> CheckSubscriptionAsync()
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var qrz = settings.Qrz;

        if (string.IsNullOrEmpty(qrz.Username) || string.IsNullOrEmpty(qrz.Password))
        {
            return new QrzSubscriptionStatus(false, false, null, "QRZ credentials not configured", null);
        }

        try
        {
            var sessionKey = await GetSessionKeyAsync(qrz.Username, qrz.Password);
            if (sessionKey == null)
            {
                return new QrzSubscriptionStatus(false, false, qrz.Username, "Failed to authenticate with QRZ", null);
            }

            // Session obtained successfully means valid subscription
            // Update cached status
            qrz.HasXmlSubscription = true;
            qrz.SubscriptionCheckedAt = DateTime.UtcNow;
            await _settingsRepository.UpsertAsync(settings);

            return new QrzSubscriptionStatus(true, true, qrz.Username, "XML subscription active", null);
        }
        catch (QrzSubscriptionRequiredException)
        {
            qrz.HasXmlSubscription = false;
            qrz.SubscriptionCheckedAt = DateTime.UtcNow;
            await _settingsRepository.UpsertAsync(settings);

            return new QrzSubscriptionStatus(true, false, qrz.Username, "XML subscription required for callsign lookups", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking QRZ subscription");
            return new QrzSubscriptionStatus(false, false, qrz.Username, $"Error: {ex.Message}", null);
        }
    }

    public async Task<QrzUploadResult> UploadQsoAsync(Qso qso)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var qrz = settings.Qrz;

        if (string.IsNullOrEmpty(qrz.ApiKey))
        {
            return new QrzUploadResult(false, null, "QRZ API key not configured", qso.Id);
        }

        try
        {
            var adif = ConvertQsoToAdif(qso);
            var result = await UploadAdifToQrzAsync(qrz.ApiKey, adif);
            return new QrzUploadResult(result.Success, result.LogId, result.Message, qso.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading QSO {QsoId} to QRZ", qso.Id);
            return new QrzUploadResult(false, null, $"Error: {ex.Message}", qso.Id);
        }
    }

    public async Task<QrzBatchUploadResult> UploadQsosAsync(IEnumerable<Qso> qsos)
    {
        var qsoList = qsos.ToList();
        var results = new List<QrzUploadResult>();
        var successCount = 0;
        var failedCount = 0;

        foreach (var qso in qsoList)
        {
            var result = await UploadQsoAsync(qso);
            results.Add(result);

            if (result.Success)
                successCount++;
            else
                failedCount++;

            // Small delay to avoid rate limiting
            await Task.Delay(100);
        }

        return new QrzBatchUploadResult(qsoList.Count, successCount, failedCount, results);
    }

    /// <summary>
    /// Upload QSOs in parallel with configurable concurrency and rate limiting.
    /// Much faster than sequential uploads while respecting API limits.
    /// </summary>
    public async Task<QrzBatchUploadResult> UploadQsosParallelAsync(
        IEnumerable<Qso> qsos,
        int maxConcurrency = 5,
        int delayBetweenBatchesMs = 200,
        IProgress<QrzUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var apiKey = settings.Qrz.ApiKey;

        if (string.IsNullOrEmpty(apiKey))
        {
            return new QrzBatchUploadResult(0, 0, 0, new[]
            {
                new QrzUploadResult(false, null, "QRZ API key not configured", null)
            });
        }

        var qsoList = qsos.ToList();
        if (qsoList.Count == 0)
        {
            return new QrzBatchUploadResult(0, 0, 0, Enumerable.Empty<QrzUploadResult>());
        }

        var results = new ConcurrentBag<QrzUploadResult>();
        var successCount = 0;
        var failedCount = 0;
        var completedCount = 0;

        // Use SemaphoreSlim for rate limiting
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        // Process in batches for progress reporting
        var batches = qsoList
            .Select((qso, index) => new { qso, index })
            .GroupBy(x => x.index / maxConcurrency)
            .Select(g => g.Select(x => x.qso).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tasks = batch.Select(async qso =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var adif = ConvertQsoToAdif(qso);
                    var result = await UploadAdifToQrzAsync(apiKey, adif);

                    var uploadResult = new QrzUploadResult(result.Success, result.LogId, result.Message, qso.Id);
                    results.Add(uploadResult);

                    if (result.Success)
                        Interlocked.Increment(ref successCount);
                    else
                        Interlocked.Increment(ref failedCount);

                    var completed = Interlocked.Increment(ref completedCount);
                    progress?.Report(new QrzUploadProgress(
                        qsoList.Count,
                        completed,
                        successCount,
                        failedCount,
                        qso.Callsign
                    ));

                    return uploadResult;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Small delay between batches to avoid overwhelming the API
            if (batch != batches.Last())
            {
                await Task.Delay(delayBetweenBatchesMs, cancellationToken);
            }
        }

        return new QrzBatchUploadResult(qsoList.Count, successCount, failedCount, results.ToList());
    }

    /// <summary>
    /// Generate batch ADIF for multiple QSOs (useful for export or future batch upload support)
    /// </summary>
    public string GenerateBatchAdif(IEnumerable<Qso> qsos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Generated by Log4YM");
        sb.AppendLine($"<ADIF_VER:5>3.1.4");
        sb.AppendLine($"<PROGRAMID:6>Log4YM");
        sb.AppendLine($"<PROGRAMVERSION:5>1.0.0");
        sb.AppendLine("<EOH>");
        sb.AppendLine();

        foreach (var qso in qsos)
        {
            sb.AppendLine(ConvertQsoToAdif(qso));
        }

        return sb.ToString();
    }

    public async Task<QrzCallsignInfo?> LookupCallsignAsync(string callsign)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var qrz = settings.Qrz;

        if (string.IsNullOrEmpty(qrz.Username) || string.IsNullOrEmpty(qrz.Password))
        {
            _logger.LogWarning("QRZ credentials not configured for callsign lookup");
            return null;
        }

        try
        {
            var sessionKey = await GetSessionKeyAsync(qrz.Username, qrz.Password);
            if (sessionKey == null)
            {
                _logger.LogWarning("Failed to get QRZ session for callsign lookup");
                return null;
            }

            var url = $"{QrzXmlApiUrl}?s={sessionKey}&callsign={Uri.EscapeDataString(callsign)}";
            var response = await _httpClient.GetStringAsync(url);
            var doc = XDocument.Parse(response);

            // Try with namespace first, then without
            var callsignElement = doc.Descendants(QrzNamespace + "Callsign").FirstOrDefault()
                ?? doc.Descendants("Callsign").FirstOrDefault();
            if (callsignElement == null)
            {
                _logger.LogWarning("QRZ response has no Callsign element for {Callsign}", callsign);
                return null;
            }

            return new QrzCallsignInfo(
                Callsign: GetElementValueNs(callsignElement, "call") ?? callsign,
                Name: GetElementValueNs(callsignElement, "name"),
                FirstName: GetElementValueNs(callsignElement, "fname"),
                Address: GetElementValueNs(callsignElement, "addr1"),
                City: GetElementValueNs(callsignElement, "addr2"),
                State: GetElementValueNs(callsignElement, "state"),
                Country: GetElementValueNs(callsignElement, "country"),
                Grid: GetElementValueNs(callsignElement, "grid"),
                Latitude: ParseDouble(GetElementValueNs(callsignElement, "lat")),
                Longitude: ParseDouble(GetElementValueNs(callsignElement, "lon")),
                Dxcc: ParseInt(GetElementValueNs(callsignElement, "dxcc")),
                CqZone: ParseInt(GetElementValueNs(callsignElement, "cqzone")),
                ItuZone: ParseInt(GetElementValueNs(callsignElement, "ituzone")),
                Email: GetElementValueNs(callsignElement, "email"),
                QslManager: GetElementValueNs(callsignElement, "qslmgr"),
                ImageUrl: GetElementValueNs(callsignElement, "image"),
                LicenseExpiration: ParseDate(GetElementValueNs(callsignElement, "expdate"))
            );
        }
        catch (QrzSubscriptionRequiredException)
        {
            _logger.LogWarning("QRZ XML subscription required for callsign lookup");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up callsign {Callsign} on QRZ", callsign);
            return null;
        }
    }

    // QRZ XML namespace
    private static readonly XNamespace QrzNamespace = "http://xmldata.qrz.com";

    private async Task<string?> GetSessionKeyAsync(string username, string password)
    {
        // Return cached session if valid
        if (_sessionKey != null && _sessionExpiry > DateTime.UtcNow)
        {
            _logger.LogDebug("Using cached QRZ session key");
            return _sessionKey;
        }

        _logger.LogDebug("Requesting new QRZ session for user: {Username}", username);
        var url = $"{QrzXmlApiUrl}?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}&agent=Log4YM";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            var doc = XDocument.Parse(response);

            // Try with namespace first, then without (for backwards compatibility)
            var session = doc.Descendants(QrzNamespace + "Session").FirstOrDefault()
                ?? doc.Descendants("Session").FirstOrDefault();
            if (session == null)
            {
                _logger.LogWarning("QRZ response has no Session element");
                return null;
            }

            var error = GetElementValueNs(session, "Error");
            if (!string.IsNullOrEmpty(error))
            {
                if (error.Contains("subscription", StringComparison.OrdinalIgnoreCase))
                {
                    throw new QrzSubscriptionRequiredException(error);
                }
                _logger.LogWarning("QRZ login error: {Error}", error);
                return null;
            }

            _sessionKey = GetElementValueNs(session, "Key");
            if (string.IsNullOrEmpty(_sessionKey))
            {
                _logger.LogWarning("QRZ response has no session key");
                return null;
            }

            // Sessions typically last 24 hours, but we'll refresh more often
            _sessionExpiry = DateTime.UtcNow.AddHours(1);
            _logger.LogInformation("QRZ session obtained successfully");

            return _sessionKey;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error connecting to QRZ API");
            return null;
        }
    }

    private async Task<(bool Success, string? LogId, string? Message)> UploadAdifToQrzAsync(string apiKey, string adif)
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("KEY", apiKey),
            new KeyValuePair<string, string>("ACTION", "INSERT"),
            new KeyValuePair<string, string>("ADIF", adif)
        });

        var response = await _httpClient.PostAsync(QrzLogbookApiUrl, content);
        var responseText = await response.Content.ReadAsStringAsync();

        _logger.LogDebug("QRZ logbook response: {Response}", responseText);

        // Parse response - format is: RESULT=OK&LOGID=12345 or RESULT=FAIL&REASON=message
        // QRZ can also return RESULT=REPLACE&LOGID=xxx for duplicates
        var parts = responseText.Split('&')
            .Select(p => p.Split('='))
            .Where(p => p.Length == 2)
            .ToDictionary(p => p[0], p => WebUtility.UrlDecode(p[1]));

        parts.TryGetValue("RESULT", out var result);
        parts.TryGetValue("LOGID", out var logId);
        parts.TryGetValue("REASON", out var reason);

        // OK = new record inserted, REPLACE = duplicate updated
        if (result == "OK" || result == "REPLACE")
        {
            var message = result == "REPLACE" ? "QSO already exists (updated)" : "QSO uploaded successfully";
            return (true, logId, message);
        }

        // Handle duplicate errors as success (QSO already exists in QRZ)
        if (reason != null && (
            reason.Contains("duplicate", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
            reason.Contains("dupe", StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogDebug("QRZ duplicate detected, treating as success: {Reason}", reason);
            return (true, logId, "QSO already exists in QRZ");
        }

        return (false, null, reason ?? "Unknown error");
    }

    private static string ConvertQsoToAdif(Qso qso)
    {
        var sb = new StringBuilder();

        // Required fields
        AppendAdifField(sb, "CALL", qso.Callsign);
        AppendAdifField(sb, "QSO_DATE", qso.QsoDate.ToString("yyyyMMdd"));
        AppendAdifField(sb, "TIME_ON", qso.TimeOn.Replace(":", ""));
        AppendAdifField(sb, "BAND", qso.Band);
        AppendAdifField(sb, "MODE", qso.Mode);

        // Optional fields
        if (qso.Frequency.HasValue)
            AppendAdifField(sb, "FREQ", qso.Frequency.Value.ToString("F6"));

        if (!string.IsNullOrEmpty(qso.TimeOff))
            AppendAdifField(sb, "TIME_OFF", qso.TimeOff.Replace(":", ""));

        if (!string.IsNullOrEmpty(qso.RstSent))
            AppendAdifField(sb, "RST_SENT", qso.RstSent);

        if (!string.IsNullOrEmpty(qso.RstRcvd))
            AppendAdifField(sb, "RST_RCVD", qso.RstRcvd);

        if (!string.IsNullOrEmpty(qso.Name) || !string.IsNullOrEmpty(qso.Station?.Name))
            AppendAdifField(sb, "NAME", qso.Name ?? qso.Station?.Name);

        if (!string.IsNullOrEmpty(qso.Grid) || !string.IsNullOrEmpty(qso.Station?.Grid))
            AppendAdifField(sb, "GRIDSQUARE", qso.Grid ?? qso.Station?.Grid);

        if (!string.IsNullOrEmpty(qso.Country) || !string.IsNullOrEmpty(qso.Station?.Country))
            AppendAdifField(sb, "COUNTRY", qso.Country ?? qso.Station?.Country);

        if (qso.Dxcc.HasValue || qso.Station?.Dxcc.HasValue == true)
            AppendAdifField(sb, "DXCC", (qso.Dxcc ?? qso.Station?.Dxcc)?.ToString());

        if (!string.IsNullOrEmpty(qso.Continent) || !string.IsNullOrEmpty(qso.Station?.Continent))
            AppendAdifField(sb, "CONT", qso.Continent ?? qso.Station?.Continent);

        if (!string.IsNullOrEmpty(qso.Comment))
            AppendAdifField(sb, "COMMENT", qso.Comment);

        if (!string.IsNullOrEmpty(qso.Notes))
            AppendAdifField(sb, "NOTES", qso.Notes);

        if (qso.Station?.CqZone.HasValue == true)
            AppendAdifField(sb, "CQZ", qso.Station.CqZone.ToString());

        if (qso.Station?.ItuZone.HasValue == true)
            AppendAdifField(sb, "ITUZ", qso.Station.ItuZone.ToString());

        if (qso.Station?.State != null)
            AppendAdifField(sb, "STATE", qso.Station.State);

        // Contest fields
        if (qso.Contest != null)
        {
            if (!string.IsNullOrEmpty(qso.Contest.ContestId))
                AppendAdifField(sb, "CONTEST_ID", qso.Contest.ContestId);
            if (!string.IsNullOrEmpty(qso.Contest.SerialSent))
                AppendAdifField(sb, "STX", qso.Contest.SerialSent);
            if (!string.IsNullOrEmpty(qso.Contest.SerialRcvd))
                AppendAdifField(sb, "SRX", qso.Contest.SerialRcvd);
            if (!string.IsNullOrEmpty(qso.Contest.Exchange))
                AppendAdifField(sb, "SRX_STRING", qso.Contest.Exchange);
        }

        sb.Append("<EOR>");
        return sb.ToString();
    }

    private static void AppendAdifField(StringBuilder sb, string fieldName, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append($"<{fieldName}:{value.Length}>{value}");
    }

    private static string? GetElementValue(XElement parent, string name)
    {
        return parent.Element(name)?.Value;
    }

    // Helper that tries both with namespace and without
    private static string? GetElementValueNs(XElement parent, string name)
    {
        return parent.Element(QrzNamespace + name)?.Value ?? parent.Element(name)?.Value;
    }

    private static double? ParseDouble(string? value)
    {
        return double.TryParse(value, out var result) ? result : null;
    }

    private static int? ParseInt(string? value)
    {
        return int.TryParse(value, out var result) ? result : null;
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return DateTime.TryParse(value, out var result) ? result : null;
    }
}

public class QrzSubscriptionRequiredException : Exception
{
    public QrzSubscriptionRequiredException(string message) : base(message) { }
}
