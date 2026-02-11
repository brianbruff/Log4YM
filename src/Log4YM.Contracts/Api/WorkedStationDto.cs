namespace Log4YM.Contracts.Api;

public record WorkedStationDto(
    string Callsign,
    DateTime QsoDate,
    string Band,
    string Mode,
    string? Name,
    double? Latitude,
    double? Longitude,
    string? Grid,
    string? ImageUrl
);
