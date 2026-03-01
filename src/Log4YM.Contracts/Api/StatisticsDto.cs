namespace Log4YM.Contracts.Api;

public record DxccStatistics(
    int TotalEntitiesWorked,
    int TotalEntitiesConfirmed,
    int ChallengeScore,
    List<DxccEntityStatus> Entities,
    Dictionary<string, BandSummary> BandSummaries
);

public record DxccEntityStatus(
    int? DxccCode,
    string EntityName,
    string? Continent,
    Dictionary<string, BandStatus> BandStatus,
    DateTime? FirstWorked,
    DateTime? LastWorked,
    int TotalQsos
);

public record BandStatus(
    bool Worked,
    bool Confirmed,
    int QsoCount
);

public record BandSummary(
    int EntitiesWorked,
    int EntitiesConfirmed
);

public record StatisticsFilters(
    string? Band = null,
    string? Mode = null,
    string? Continent = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);
