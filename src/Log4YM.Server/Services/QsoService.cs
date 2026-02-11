using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Models;
using Log4YM.Contracts.Events;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public class QsoService : IQsoService
{
    private readonly IQsoRepository _repository;
    private readonly IQrzImageCacheRepository _imageCacheRepository;
    private readonly IHubContext<LogHub, ILogHubClient> _hub;

    public QsoService(
        IQsoRepository repository,
        IQrzImageCacheRepository imageCacheRepository,
        IHubContext<LogHub, ILogHubClient> hub)
    {
        _repository = repository;
        _imageCacheRepository = imageCacheRepository;
        _hub = hub;
    }

    public async Task<QsoResponse?> GetByIdAsync(string id)
    {
        var qso = await _repository.GetByIdAsync(id);
        return qso is null ? null : MapToResponse(qso);
    }

    public async Task<PaginatedQsoResponse> GetQsosAsync(QsoSearchRequest request)
    {
        var (items, totalCount) = await _repository.SearchAsync(request);
        var totalPages = (int)Math.Ceiling(totalCount / (double)request.Limit);
        var page = (request.Skip / request.Limit) + 1;

        return new PaginatedQsoResponse(
            Items: items.Select(MapToResponse),
            TotalCount: totalCount,
            Page: page,
            PageSize: request.Limit,
            TotalPages: totalPages
        );
    }

    public async Task<QsoResponse> CreateAsync(CreateQsoRequest request)
    {
        var qso = new Qso
        {
            Callsign = request.Callsign.ToUpperInvariant(),
            QsoDate = request.QsoDate,
            TimeOn = request.TimeOn,
            Band = request.Band,
            Mode = request.Mode,
            Frequency = request.Frequency,
            RstSent = request.RstSent,
            RstRcvd = request.RstRcvd,
            Comment = request.Comment,
            Station = new StationInfo
            {
                Name = request.Name,
                Grid = request.Grid,
                Country = request.Country
            }
        };

        var created = await _repository.CreateAsync(qso);

        // Broadcast to all clients via SignalR
        await _hub.BroadcastQso(new QsoLoggedEvent(
            created.Id,
            created.Callsign,
            created.QsoDate,
            created.TimeOn,
            created.Band,
            created.Mode,
            created.Frequency,
            created.RstSent,
            created.RstRcvd,
            created.Station?.Grid
        ));

        return MapToResponse(created);
    }

    public async Task<QsoResponse?> UpdateAsync(string id, UpdateQsoRequest request)
    {
        var existing = await _repository.GetByIdAsync(id);
        if (existing is null) return null;

        if (request.Callsign != null) existing.Callsign = request.Callsign.ToUpperInvariant();
        if (request.QsoDate.HasValue) existing.QsoDate = request.QsoDate.Value;
        if (request.TimeOn != null) existing.TimeOn = request.TimeOn;
        if (request.Band != null) existing.Band = request.Band;
        if (request.Mode != null) existing.Mode = request.Mode;
        if (request.Frequency.HasValue) existing.Frequency = request.Frequency;
        if (request.RstSent != null) existing.RstSent = request.RstSent;
        if (request.RstRcvd != null) existing.RstRcvd = request.RstRcvd;
        if (request.Comment != null) existing.Comment = request.Comment;

        existing.Station ??= new StationInfo();
        if (request.Name != null) existing.Station.Name = request.Name;
        if (request.Grid != null) existing.Station.Grid = request.Grid;
        if (request.Country != null) existing.Station.Country = request.Country;

        await _repository.UpdateAsync(id, existing);
        return MapToResponse(existing);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        return await _repository.DeleteAsync(id);
    }

    public async Task<QsoStatistics> GetStatisticsAsync()
    {
        return await _repository.GetStatisticsAsync();
    }

    public async Task<IEnumerable<WorkedStationDto>> GetRecentWorkedStationsAsync(int limit)
    {
        // Get recent QSOs
        var qsos = await _repository.GetRecentAsync(limit);

        // Get unique callsigns from recent QSOs
        var callsigns = qsos.Select(q => q.Callsign).Distinct().ToList();

        // Fetch cached QRZ data for these callsigns
        var cachedData = await _imageCacheRepository.GetByCallsignsAsync(callsigns);
        var cacheDict = cachedData.ToDictionary(c => c.Callsign, c => c);

        // Map to WorkedStationDto, enriching with cached QRZ data
        return qsos.Select(qso =>
        {
            cacheDict.TryGetValue(qso.Callsign.ToUpperInvariant(), out var cache);

            return new WorkedStationDto(
                Callsign: qso.Callsign,
                QsoDate: qso.QsoDate,
                Band: qso.Band,
                Mode: qso.Mode,
                Name: cache?.Name ?? qso.Name ?? qso.Station?.Name,
                Latitude: cache?.Latitude ?? qso.Station?.Latitude,
                Longitude: cache?.Longitude ?? qso.Station?.Longitude,
                Grid: cache?.Grid ?? qso.Grid ?? qso.Station?.Grid,
                ImageUrl: cache?.ImageUrl
            );
        }).Where(ws => ws.Latitude.HasValue && ws.Longitude.HasValue); // Only return stations with coordinates
    }

    private static QsoResponse MapToResponse(Qso qso) => new(
        qso.Id,
        qso.Callsign,
        qso.QsoDate,
        qso.TimeOn,
        qso.TimeOff,
        qso.Band,
        qso.Mode,
        qso.Frequency,
        qso.RstSent,
        qso.RstRcvd,
        new StationInfoDto(
            qso.Name ?? qso.Station?.Name,
            qso.Grid ?? qso.Station?.Grid,
            qso.Country ?? qso.Station?.Country,
            qso.Dxcc ?? qso.Station?.Dxcc,
            qso.Station?.State,
            qso.Continent ?? qso.Station?.Continent,
            qso.Station?.Latitude,
            qso.Station?.Longitude
        ),
        qso.Comment,
        qso.CreatedAt
    );
}
