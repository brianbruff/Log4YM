using Log4YM.Contracts.Models;

namespace Log4YM.Server.Services;

public interface ILotwService
{
    /// <summary>
    /// Check if TQSL is installed and available
    /// </summary>
    Task<LotwInstallationStatus> CheckTqslInstallationAsync();

    /// <summary>
    /// Get list of configured station locations from TQSL
    /// </summary>
    Task<IEnumerable<string>> GetStationLocationsAsync();

    /// <summary>
    /// Sign and upload QSOs to LOTW using TQSL
    /// </summary>
    Task<LotwUploadResult> SignAndUploadAsync(
        IEnumerable<Qso> qsos,
        string stationLocation,
        IProgress<LotwUploadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate ADIF file for LOTW upload
    /// </summary>
    string GenerateLotwAdif(IEnumerable<Qso> qsos);
}

public record LotwInstallationStatus(
    bool IsInstalled,
    string? TqslPath,
    string? Version,
    string? Message
);

public record LotwUploadResult(
    bool Success,
    int TotalCount,
    int UploadedCount,
    int FailedCount,
    string? Message,
    IEnumerable<string>? Errors
);

public record LotwUploadProgress(
    int TotalCount,
    int CompletedCount,
    string? CurrentCallsign,
    string? Status
);
