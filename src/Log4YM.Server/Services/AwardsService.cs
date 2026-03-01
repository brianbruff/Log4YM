using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public interface IAwardsService
{
    Task<DxccStatistics> GetDxccStatisticsAsync(StatisticsFilters? filters = null);
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
