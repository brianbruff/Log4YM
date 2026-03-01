using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public interface IAwardsService
{
    Task<DxccStatistics> GetDxccStatisticsAsync(StatisticsFilters? filters = null);
    Task<VuccStatistics> GetVuccStatisticsAsync(StatisticsFilters? filters = null);
    Task<PotaStatistics> GetPotaStatisticsAsync(PotaFilters? filters = null);
    Task<IotaStatistics> GetIotaStatisticsAsync(IotaFilters? filters = null);
}

public class AwardsService : IAwardsService
{
    private readonly IQsoRepository _repository;

    public AwardsService(IQsoRepository repository)
    {
        _repository = repository;
    }

    public async Task<DxccStatistics> GetDxccStatisticsAsync(StatisticsFilters? filters = null)
    {
        var allQsos = await _repository.GetAllAsync();
        var qsos = allQsos.ToList();

        // Apply filters
        if (filters != null)
        {
            if (!string.IsNullOrEmpty(filters.Band))
                qsos = qsos.Where(q => string.Equals(q.Band, filters.Band, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrEmpty(filters.Mode))
                qsos = qsos.Where(q => string.Equals(q.Mode, filters.Mode, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrEmpty(filters.Continent))
                qsos = qsos.Where(q => string.Equals(q.Continent ?? q.Station?.Continent, filters.Continent, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filters.FromDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate >= filters.FromDate.Value).ToList();

            if (filters.ToDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate <= filters.ToDate.Value.AddDays(1)).ToList();
        }

        // Group by DXCC entity (country name as key for display, DXCC code when available)
        var entityGroups = qsos
            .Where(q => !string.IsNullOrEmpty(q.Country))
            .GroupBy(q => GetEntityKey(q))
            .ToList();

        var entityStatuses = new List<DxccEntityStatus>();

        foreach (var group in entityGroups)
        {
            var groupQsos = group.ToList();
            var representative = groupQsos.First();
            var entityName = representative.Country ?? "Unknown";
            var dxccCode = representative.Dxcc ?? representative.Station?.Dxcc;
            var continent = representative.Continent ?? representative.Station?.Continent;

            // Filter by continent (apply after grouping since continent is on the entity)
            if (filters != null && !string.IsNullOrEmpty(filters.Continent))
            {
                if (!string.Equals(continent, filters.Continent, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            // Build band status for this entity
            var bandStatus = groupQsos
                .GroupBy(q => q.Band)
                .ToDictionary(
                    bg => bg.Key,
                    bg =>
                    {
                        var bandQsos = bg.ToList();
                        var confirmed = bandQsos.Any(IsConfirmed);
                        return new BandStatus(
                            Worked: true,
                            Confirmed: confirmed,
                            QsoCount: bandQsos.Count
                        );
                    }
                );

            // Filter by status
            if (filters != null && !string.IsNullOrEmpty(filters.Status))
            {
                var skip = filters.Status switch
                {
                    "confirmed" => !bandStatus.Values.Any(b => b.Confirmed),
                    "workedNotConfirmed" => !bandStatus.Values.Any(b => b.Worked && !b.Confirmed),
                    "worked" => !bandStatus.Values.Any(b => b.Worked),
                    _ => false
                };
                if (skip) continue;
            }

            entityStatuses.Add(new DxccEntityStatus(
                DxccCode: dxccCode,
                EntityName: entityName,
                Continent: continent,
                BandStatus: bandStatus,
                FirstWorked: groupQsos.Min(q => q.QsoDate),
                LastWorked: groupQsos.Max(q => q.QsoDate),
                TotalQsos: groupQsos.Count
            ));
        }

        entityStatuses = entityStatuses.OrderBy(e => e.EntityName).ToList();

        // Build band summaries across all entities
        var allBands = entityStatuses.SelectMany(e => e.BandStatus.Keys).Distinct().OrderBy(b => GetBandOrder(b)).ToList();
        var bandSummaries = allBands.ToDictionary(
            band => band,
            band =>
            {
                var entitiesWithBand = entityStatuses.Where(e => e.BandStatus.ContainsKey(band)).ToList();
                return new BandSummary(
                    EntitiesWorked: entitiesWithBand.Count,
                    EntitiesConfirmed: entitiesWithBand.Count(e => e.BandStatus[band].Confirmed)
                );
            }
        );

        // Overall totals (without band filter for summary counts)
        var totalWorked = entityStatuses.Count;
        var totalConfirmed = entityStatuses.Count(e => e.BandStatus.Values.Any(b => b.Confirmed));
        var challengeScore = entityStatuses.Sum(e => e.BandStatus.Values.Count(b => b.Confirmed));

        return new DxccStatistics(
            TotalEntitiesWorked: totalWorked,
            TotalEntitiesConfirmed: totalConfirmed,
            ChallengeScore: challengeScore,
            Entities: entityStatuses,
            BandSummaries: bandSummaries
        );
    }

    private static readonly string[] VuccBands = ["6m", "2m", "70cm", "23cm"];

    private static int GetVuccThreshold(string band) => band.ToLowerInvariant() switch
    {
        "6m" => 100,
        "2m" => 100,
        "70cm" => 50,
        "23cm" => 25,
        _ => 25
    };

    public async Task<VuccStatistics> GetVuccStatisticsAsync(StatisticsFilters? filters = null)
    {
        var allQsos = await _repository.GetAllAsync();
        var qsos = allQsos
            .Where(q => !string.IsNullOrEmpty(q.Grid) && q.Grid.Length >= 4)
            .Where(q => VuccBands.Contains(q.Band, StringComparer.OrdinalIgnoreCase))
            .ToList();

        // Apply filters
        if (filters != null)
        {
            if (!string.IsNullOrEmpty(filters.Band))
                qsos = qsos.Where(q => string.Equals(q.Band, filters.Band, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrEmpty(filters.Mode))
                qsos = qsos.Where(q => string.Equals(q.Mode, filters.Mode, StringComparison.OrdinalIgnoreCase)).ToList();

            if (filters.FromDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate >= filters.FromDate.Value).ToList();

            if (filters.ToDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate <= filters.ToDate.Value.AddDays(1)).ToList();
        }

        // Group by 4-char grid prefix + band
        var gridGroups = qsos
            .GroupBy(q => new { Grid = q.Grid![..4].ToUpperInvariant(), Band = q.Band.ToLowerInvariant() })
            .ToList();

        var gridDetails = new List<GridDetail>();

        foreach (var group in gridGroups)
        {
            var groupQsos = group.ToList();
            var confirmed = groupQsos.Any(IsConfirmed);

            // Filter by status
            if (filters != null && !string.IsNullOrEmpty(filters.Status))
            {
                var skip = filters.Status switch
                {
                    "confirmed" => !confirmed,
                    "workedNotConfirmed" => confirmed,
                    "worked" => false,
                    _ => false
                };
                if (skip) continue;
            }

            gridDetails.Add(new GridDetail(
                Grid: group.Key.Grid,
                Band: group.Key.Band,
                QsoCount: groupQsos.Count,
                Confirmed: confirmed,
                FirstWorked: groupQsos.Min(q => q.QsoDate),
                LastWorked: groupQsos.Max(q => q.QsoDate)
            ));
        }

        gridDetails = gridDetails.OrderBy(g => g.Grid).ThenBy(g => GetBandOrder(g.Band)).ToList();

        // Build band summaries
        var bandSummaries = VuccBands.ToDictionary(
            band => band,
            band =>
            {
                var bandGrids = gridDetails.Where(g => string.Equals(g.Band, band, StringComparison.OrdinalIgnoreCase)).ToList();
                return new GridBandSummary(
                    Band: band,
                    UniqueGrids: bandGrids.Count,
                    ConfirmedGrids: bandGrids.Count(g => g.Confirmed),
                    AwardThreshold: GetVuccThreshold(band),
                    QsoCount: bandGrids.Sum(g => g.QsoCount)
                );
            }
        );

        var totalUniqueGrids = gridDetails.Select(g => g.Grid).Distinct().Count();

        return new VuccStatistics(
            TotalUniqueGrids: totalUniqueGrids,
            BandSummaries: bandSummaries,
            Grids: gridDetails
        );
    }

    public async Task<PotaStatistics> GetPotaStatisticsAsync(PotaFilters? filters = null)
    {
        var allQsos = await _repository.GetAllAsync();
        var qsos = allQsos.ToList();

        // Apply date filters
        if (filters != null)
        {
            if (filters.FromDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate >= filters.FromDate.Value).ToList();

            if (filters.ToDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate <= filters.ToDate.Value.AddDays(1)).ToList();
        }

        // Extract POTA park entries from QSOs
        // Each QSO can have pota_ref (hunting) and/or my_pota_ref (activating), comma-separated
        var parkEntries = new List<(string ParkRef, string ActivityType, Qso Qso)>();

        foreach (var qso in qsos)
        {
            var huntRef = qso.AdifExtra?["pota_ref"]?.AsString;
            var activateRef = qso.AdifExtra?["my_pota_ref"]?.AsString;

            if (!string.IsNullOrWhiteSpace(huntRef))
            {
                foreach (var park in huntRef.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    parkEntries.Add((park.ToUpperInvariant(), "Hunter", qso));
            }

            if (!string.IsNullOrWhiteSpace(activateRef))
            {
                foreach (var park in activateRef.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    parkEntries.Add((park.ToUpperInvariant(), "Activator", qso));
            }
        }

        // Apply activity type filter
        if (filters != null && !string.IsNullOrEmpty(filters.ActivityType))
        {
            parkEntries = parkEntries
                .Where(e => string.Equals(e.ActivityType, filters.ActivityType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Group by park reference
        var parkGroups = parkEntries
            .GroupBy(e => e.ParkRef)
            .ToList();

        var parks = new List<PotaParkDetail>();

        foreach (var group in parkGroups)
        {
            var entries = group.ToList();
            var activityTypes = entries.Select(e => e.ActivityType).Distinct().ToList();
            var activityType = activityTypes.Count > 1 ? "Both" : activityTypes[0];

            parks.Add(new PotaParkDetail(
                ParkReference: group.Key,
                ActivityType: activityType,
                QsoCount: entries.Count,
                FirstQso: entries.Min(e => e.Qso.QsoDate),
                LastQso: entries.Max(e => e.Qso.QsoDate)
            ));
        }

        parks = parks.OrderBy(p => p.ParkReference).ToList();

        var uniqueActivated = parks.Count(p => p.ActivityType is "Activator" or "Both");
        var uniqueHunted = parks.Count(p => p.ActivityType is "Hunter" or "Both");
        var totalActivationQsos = parkEntries.Count(e => e.ActivityType == "Activator");
        var totalHuntQsos = parkEntries.Count(e => e.ActivityType == "Hunter");

        return new PotaStatistics(
            UniqueParksActivated: uniqueActivated,
            UniqueParksHunted: uniqueHunted,
            TotalActivationQsos: totalActivationQsos,
            TotalHuntQsos: totalHuntQsos,
            Parks: parks
        );
    }

    public async Task<IotaStatistics> GetIotaStatisticsAsync(IotaFilters? filters = null)
    {
        var allQsos = await _repository.GetAllAsync();
        var qsos = allQsos.ToList();

        // Apply date filters
        if (filters != null)
        {
            if (filters.FromDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate >= filters.FromDate.Value).ToList();

            if (filters.ToDate.HasValue)
                qsos = qsos.Where(q => q.QsoDate <= filters.ToDate.Value.AddDays(1)).ToList();
        }

        // Extract IOTA references from AdifExtra
        var iotaQsos = qsos
            .Where(q => !string.IsNullOrWhiteSpace(q.AdifExtra?["iota"]?.AsString))
            .Select(q => new { Qso = q, IotaRef = q.AdifExtra!["iota"].AsString.ToUpperInvariant() })
            .ToList();

        // Apply continent filter on the IOTA reference prefix (e.g., "EU" from "EU-005")
        if (filters != null && !string.IsNullOrEmpty(filters.Continent))
        {
            iotaQsos = iotaQsos
                .Where(q => q.IotaRef.StartsWith(filters.Continent, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Group by IOTA reference
        var iotaGroups = iotaQsos
            .GroupBy(q => q.IotaRef)
            .ToList();

        var groups = new List<IotaGroupDetail>();

        foreach (var group in iotaGroups)
        {
            var groupQsos = group.Select(g => g.Qso).ToList();
            var confirmed = groupQsos.Any(IsConfirmed);

            // Filter by status
            if (filters != null && !string.IsNullOrEmpty(filters.Status))
            {
                var skip = filters.Status switch
                {
                    "confirmed" => !confirmed,
                    "workedNotConfirmed" => confirmed,
                    "worked" => false,
                    _ => false
                };
                if (skip) continue;
            }

            // Extract continent from IOTA reference (e.g., "EU" from "EU-005")
            var continent = group.Key.Length >= 2 ? group.Key[..2] : "??";

            groups.Add(new IotaGroupDetail(
                IotaReference: group.Key,
                Continent: continent,
                QsoCount: groupQsos.Count,
                Confirmed: confirmed,
                FirstWorked: groupQsos.Min(q => q.QsoDate),
                LastWorked: groupQsos.Max(q => q.QsoDate)
            ));
        }

        groups = groups.OrderBy(g => g.IotaReference).ToList();

        var groupsByContinent = groups
            .GroupBy(g => g.Continent)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalWorked = groups.Count;
        var totalConfirmed = groups.Count(g => g.Confirmed);
        var totalQsos = groups.Sum(g => g.QsoCount);

        return new IotaStatistics(
            TotalGroupsWorked: totalWorked,
            TotalGroupsConfirmed: totalConfirmed,
            TotalQsos: totalQsos,
            GroupsByContinent: groupsByContinent,
            Groups: groups
        );
    }

    private static string GetEntityKey(Qso qso)
    {
        // Prefer DXCC code if available, fall back to country name
        if (qso.Dxcc.HasValue)
            return $"dxcc:{qso.Dxcc.Value}";
        if (qso.Station?.Dxcc.HasValue == true)
            return $"dxcc:{qso.Station.Dxcc.Value}";
        return $"country:{qso.Country?.ToUpperInvariant() ?? "UNKNOWN"}";
    }

    private static bool IsConfirmed(Qso qso)
    {
        // Check LoTW confirmation
        if (string.Equals(qso.Qsl?.Lotw?.Rcvd, "Y", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check eQSL confirmation
        if (string.Equals(qso.Qsl?.Eqsl?.Rcvd, "Y", StringComparison.OrdinalIgnoreCase))
            return true;

        // Check paper QSL
        if (string.Equals(qso.Qsl?.Rcvd, "Y", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static int GetBandOrder(string band) => band.ToLowerInvariant() switch
    {
        "160m" => 1,
        "80m" => 2,
        "60m" => 3,
        "40m" => 4,
        "30m" => 5,
        "20m" => 6,
        "17m" => 7,
        "15m" => 8,
        "12m" => 9,
        "10m" => 10,
        "6m" => 11,
        "2m" => 12,
        "70cm" => 13,
        _ => 99
    };
}
