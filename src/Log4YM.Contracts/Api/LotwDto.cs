namespace Log4YM.Contracts.Api;

public record LotwSettingsRequest(
    bool Enabled = true,
    string? TqslPath = null,
    string? StationLocation = null
);

public record LotwInstallationResponse(
    bool IsInstalled,
    string? TqslPath,
    string? Version,
    string? Message
);

public record LotwStationLocationsResponse(
    List<string> Locations
);

public record LotwUploadRequest(
    IEnumerable<string> QsoIds
);

public record LotwUploadResponse(
    int TotalCount,
    int UploadedCount,
    int FailedCount,
    bool Success,
    string? Message,
    List<string>? Errors
);
