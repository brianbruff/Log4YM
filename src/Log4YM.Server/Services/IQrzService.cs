using Log4YM.Contracts.Models;

namespace Log4YM.Server.Services;

public interface IQrzService
{
    /// <summary>
    /// Check if the user has an active XML subscription
    /// </summary>
    Task<QrzSubscriptionStatus> CheckSubscriptionAsync();

    /// <summary>
    /// Upload a single QSO to QRZ logbook
    /// </summary>
    Task<QrzUploadResult> UploadQsoAsync(Qso qso);

    /// <summary>
    /// Upload multiple QSOs to QRZ logbook (sequential)
    /// </summary>
    Task<QrzBatchUploadResult> UploadQsosAsync(IEnumerable<Qso> qsos);

    /// <summary>
    /// Upload multiple QSOs in parallel with rate limiting (much faster)
    /// </summary>
    Task<QrzBatchUploadResult> UploadQsosParallelAsync(
        IEnumerable<Qso> qsos,
        int maxConcurrency = 5,
        int delayBetweenBatchesMs = 200,
        IProgress<QrzUploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate batch ADIF for multiple QSOs
    /// </summary>
    string GenerateBatchAdif(IEnumerable<Qso> qsos);

    /// <summary>
    /// Lookup callsign information from QRZ
    /// </summary>
    Task<QrzCallsignInfo?> LookupCallsignAsync(string callsign);
}

public record QrzSubscriptionStatus(
    bool IsValid,
    bool HasXmlSubscription,
    string? Username,
    string? Message,
    DateTime? ExpirationDate
);

public record QrzUploadResult(
    bool Success,
    string? LogId,
    string? Message,
    string? QsoId
);

public record QrzBatchUploadResult(
    int TotalCount,
    int SuccessCount,
    int FailedCount,
    IEnumerable<QrzUploadResult> Results
);

public record QrzCallsignInfo(
    string Callsign,
    string? Name,
    string? FirstName,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? Grid,
    double? Latitude,
    double? Longitude,
    int? Dxcc,
    int? CqZone,
    int? ItuZone,
    string? Email,
    string? QslManager,
    string? ImageUrl,
    DateTime? LicenseExpiration
);

public record QrzUploadProgress(
    int TotalCount,
    int CompletedCount,
    int SuccessCount,
    int FailedCount,
    string? CurrentCallsign
);
