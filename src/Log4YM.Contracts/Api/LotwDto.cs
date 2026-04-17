namespace Log4YM.Contracts.Api;

public record LotwUploadFilter(
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? StationCallsign = null,
    IEnumerable<string>? Bands = null,
    IEnumerable<string>? Modes = null,
    // Adds lotw_qsl_sent='I' (Ignored) to the eligible set. Default: skipped.
    bool IncludeIgnored = false,
    // Adds lotw_qsl_sent='N' (explicit No) to the eligible set. Default: skipped.
    bool IncludeNotSent = false
    // Always eligible: null (never sent), 'R' (re-send requested), 'Q' (queued).
);

public record LotwUploadResult(
    int QsoCount,
    bool Success,
    int TqslExitCode,
    string Message,
    int MarkedAsSent
);

public record LotwPreviewResponse(
    int Count,
    IEnumerable<LotwPreviewItem> Sample
);

public record LotwPreviewItem(
    string Id,
    string Callsign,
    DateTime QsoDate,
    string Band,
    string Mode,
    string? LotwSent
);

public record LotwTestTqslRequest(string Path);

public record LotwTestTqslResponse(bool Ok, string? Version, string? Error);
