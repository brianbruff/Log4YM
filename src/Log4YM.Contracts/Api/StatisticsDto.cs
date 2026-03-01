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

public record VuccStatistics(
    int TotalUniqueGrids,
    Dictionary<string, GridBandSummary> BandSummaries,
    List<GridDetail> Grids
);

public record GridBandSummary(
    string Band,
    int UniqueGrids,
    int ConfirmedGrids,
    int AwardThreshold,
    int QsoCount
);

public record GridDetail(
    string Grid,
    string Band,
    int QsoCount,
    bool Confirmed,
    DateTime? FirstWorked,
    DateTime? LastWorked
);

// POTA Statistics
public record PotaStatistics(
    int UniqueParksActivated,
    int UniqueParksHunted,
    int TotalActivationQsos,
    int TotalHuntQsos,
    List<PotaParkDetail> Parks
);

public record PotaParkDetail(
    string ParkReference,
    string ActivityType,
    int QsoCount,
    DateTime? FirstQso,
    DateTime? LastQso
);

public record PotaFilters(
    string? ActivityType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

// IOTA Statistics
public record IotaStatistics(
    int TotalGroupsWorked,
    int TotalGroupsConfirmed,
    int TotalQsos,
    Dictionary<string, int> GroupsByContinent,
    List<IotaGroupDetail> Groups
);

public record IotaGroupDetail(
    string IotaReference,
    string Continent,
    int QsoCount,
    bool Confirmed,
    DateTime? FirstWorked,
    DateTime? LastWorked
);

public record IotaFilters(
    string? Continent = null,
    string? Status = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);
