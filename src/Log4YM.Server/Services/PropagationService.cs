using System.Collections.Concurrent;
using System.Xml.Linq;

namespace Log4YM.Server.Services;

#region DTOs

public record BandPrediction(
    string Band,
    double FreqMHz,
    int Reliability,
    string Status
);

public record PropagationPrediction(
    double DeLat,
    double DeLon,
    double DxLat,
    double DxLon,
    double DistanceKm,
    double BearingDeg,
    double MufMHz,
    double LufMHz,
    int Sfi,
    int KIndex,
    BandPrediction[] CurrentBands,
    int[][] HeatmapData,
    string[] BandNames,
    DateTime Timestamp
);

public record GenericBandConditions(
    BandConditionEntry[] Bands,
    int Sfi,
    int KIndex,
    int Ssn,
    string Source,
    DateTime Timestamp
);

public record BandConditionEntry(
    string Band,
    string DayStatus,
    string NightStatus
);

#endregion

public interface IPropagationService
{
    Task<PropagationPrediction> PredictAsync(double deLat, double deLon, double dxLat, double dxLon);
    Task<GenericBandConditions> GetGenericConditionsAsync();
}

public class PropagationService : IPropagationService
{
    private readonly ISpaceWeatherService _spaceWeatherService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<PropagationService> _logger;

    // Path prediction cache: key = rounded DX coords, value = (prediction, timestamp)
    private readonly ConcurrentDictionary<string, (PropagationPrediction Prediction, DateTime FetchedAt)> _pathCache = new();
    private static readonly TimeSpan PathCacheTtl = TimeSpan.FromMinutes(5);

    // Generic conditions cache
    private GenericBandConditions? _cachedGeneric;
    private DateTime _lastGenericFetch = DateTime.MinValue;
    private static readonly TimeSpan GenericCacheTtl = TimeSpan.FromMinutes(30);

    // HF bands to evaluate (matching frontend HF_BANDS)
    internal static readonly (string Name, double FreqMHz)[] HfBands =
    {
        ("80m", 3.5), ("60m", 5.3), ("40m", 7.0), ("30m", 10.1),
        ("20m", 14.0), ("17m", 18.1), ("15m", 21.0), ("12m", 24.9), ("10m", 28.0)
    };

    public PropagationService(
        ISpaceWeatherService spaceWeatherService,
        IHttpClientFactory httpClientFactory,
        ILogger<PropagationService> logger)
    {
        _spaceWeatherService = spaceWeatherService;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _logger = logger;
    }

    public async Task<PropagationPrediction> PredictAsync(double deLat, double deLon, double dxLat, double dxLon)
    {
        // Check cache
        var key = MakeCacheKey(deLat, deLon, dxLat, dxLon);
        if (_pathCache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.FetchedAt < PathCacheTtl)
            return cached.Prediction;

        var weather = await _spaceWeatherService.GetCurrentAsync();
        int sfi = weather.SolarFluxIndex;
        int kIndex = weather.KIndex;
        int ssn = SsnFromSfi(sfi);

        double distKm = HaversineDistanceKm(deLat, deLon, dxLat, dxLon);
        double bearing = BearingDeg(deLat, deLon, dxLat, dxLon);
        var (midLat, midLon) = MidpointLatLon(deLat, deLon, dxLat, dxLon);
        int hops = Math.Max(1, (int)Math.Ceiling(distKm / 3500.0));

        var now = DateTime.UtcNow;
        int currentHour = now.Hour;

        // Compute 24-hour heatmap: [bandIdx][hourIdx]
        int[][] heatmap = new int[HfBands.Length][];
        for (int b = 0; b < HfBands.Length; b++)
        {
            heatmap[b] = new int[24];
            for (int h = 0; h < 24; h++)
            {
                var utcAtHour = now.Date.AddHours(h);
                double muf = CalculateMuf(ssn, distKm, midLat, midLon, utcAtHour);
                double luf = CalculateLuf(ssn, distKm, midLat, midLon, utcAtHour, kIndex);
                heatmap[b][h] = CalculateReliability(HfBands[b].FreqMHz, muf, luf, kIndex, hops);
            }
        }

        // Current-hour MUF/LUF
        double currentMuf = CalculateMuf(ssn, distKm, midLat, midLon, now);
        double currentLuf = CalculateLuf(ssn, distKm, midLat, midLon, now, kIndex);

        // Current-hour per-band predictions
        var currentBands = HfBands.Select((band, idx) =>
        {
            int rel = heatmap[idx][currentHour];
            string status = rel >= 80 ? "EXCELLENT" : rel >= 60 ? "GOOD" : rel >= 40 ? "FAIR" : rel >= 20 ? "POOR" : "CLOSED";
            return new BandPrediction(band.Name, band.FreqMHz, rel, status);
        }).ToArray();

        var prediction = new PropagationPrediction(
            deLat, deLon, dxLat, dxLon,
            Math.Round(distKm, 1),
            Math.Round(bearing, 1),
            Math.Round(currentMuf, 1),
            Math.Round(currentLuf, 1),
            sfi, kIndex,
            currentBands,
            heatmap,
            HfBands.Select(b => b.Name).ToArray(),
            DateTime.UtcNow
        );

        // Update cache (evict stale entries if too many)
        if (_pathCache.Count > 100)
        {
            foreach (var k in _pathCache.Where(kv => DateTime.UtcNow - kv.Value.FetchedAt > PathCacheTtl).Select(kv => kv.Key).ToList())
                _pathCache.TryRemove(k, out _);
        }
        _pathCache[key] = (prediction, DateTime.UtcNow);

        return prediction;
    }

    public async Task<GenericBandConditions> GetGenericConditionsAsync()
    {
        if (_cachedGeneric != null && DateTime.UtcNow - _lastGenericFetch < GenericCacheTtl)
            return _cachedGeneric;

        try
        {
            var xml = await _httpClient.GetStringAsync("https://www.hamqsl.com/solarxml.php");
            var doc = XDocument.Parse(xml);
            var solar = doc.Root?.Element("solardata") ?? doc.Root;

            int sfi = int.Parse(solar?.Element("solarflux")?.Value?.Trim() ?? "70");
            int kIndex = int.Parse(solar?.Element("kindex")?.Value?.Trim() ?? "2");
            int ssn = int.Parse(solar?.Element("sunspots")?.Value?.Trim() ?? "0");

            var bands = new List<BandConditionEntry>();
            var conditions = solar?.Element("calculatedconditions");

            if (conditions != null)
            {
                var dayEntries = conditions.Elements("band")
                    .Where(b => b.Attribute("time")?.Value == "day")
                    .ToDictionary(
                        b => b.Attribute("name")?.Value ?? "",
                        b => NormalizeCondition(b.Value.Trim())
                    );
                var nightEntries = conditions.Elements("band")
                    .Where(b => b.Attribute("time")?.Value == "night")
                    .ToDictionary(
                        b => b.Attribute("name")?.Value ?? "",
                        b => NormalizeCondition(b.Value.Trim())
                    );

                // Map N0NBH band groups to individual bands
                var bandGroupMap = new Dictionary<string, string[]>
                {
                    ["80m-40m"] = ["80m", "60m", "40m"],
                    ["30m-20m"] = ["30m", "20m"],
                    ["17m-15m"] = ["17m", "15m"],
                    ["12m-10m"] = ["12m", "10m"],
                };

                foreach (var (group, individualBands) in bandGroupMap)
                {
                    string dayStatus = dayEntries.GetValueOrDefault(group, "Fair");
                    string nightStatus = nightEntries.GetValueOrDefault(group, "Fair");
                    foreach (var band in individualBands)
                        bands.Add(new BandConditionEntry(band, dayStatus, nightStatus));
                }
            }

            var result = new GenericBandConditions(bands.ToArray(), sfi, kIndex, ssn, "N0NBH", DateTime.UtcNow);
            _cachedGeneric = result;
            _lastGenericFetch = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch N0NBH conditions");
            if (_cachedGeneric != null) return _cachedGeneric;

            var weather = await _spaceWeatherService.GetCurrentAsync();
            return new GenericBandConditions(
                [],
                weather.SolarFluxIndex, weather.KIndex, weather.SunspotNumber,
                "Unavailable", DateTime.UtcNow
            );
        }
    }

    #region Propagation Math

    /// <summary>SSN derived from SFI (OpenHamClock formula)</summary>
    public static int SsnFromSfi(int sfi)
        => Math.Max(0, (int)Math.Round((sfi - 67.0) / 0.97));

    /// <summary>Great-circle distance in km using Haversine formula</summary>
    public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = ToRad(lat2 - lat1);
        double dLon = ToRad(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    /// <summary>Initial bearing in degrees from point 1 to point 2</summary>
    public static double BearingDeg(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = ToRad(lon2 - lon1);
        double y = Math.Sin(dLon) * Math.Cos(ToRad(lat2));
        double x = Math.Cos(ToRad(lat1)) * Math.Sin(ToRad(lat2)) -
                   Math.Sin(ToRad(lat1)) * Math.Cos(ToRad(lat2)) * Math.Cos(dLon);
        return (ToDeg(Math.Atan2(y, x)) + 360) % 360;
    }

    /// <summary>Great-circle midpoint between two coordinates</summary>
    public static (double Lat, double Lon) MidpointLatLon(double lat1, double lon1, double lat2, double lon2)
    {
        double lat1r = ToRad(lat1), lon1r = ToRad(lon1);
        double lat2r = ToRad(lat2);
        double dLon = ToRad(lon2 - lon1);
        double bx = Math.Cos(lat2r) * Math.Cos(dLon);
        double by = Math.Cos(lat2r) * Math.Sin(dLon);
        double midLat = Math.Atan2(
            Math.Sin(lat1r) + Math.Sin(lat2r),
            Math.Sqrt((Math.Cos(lat1r) + bx) * (Math.Cos(lat1r) + bx) + by * by));
        double midLon = lon1r + Math.Atan2(by, Math.Cos(lat1r) + bx);
        return (ToDeg(midLat), ToDeg(midLon));
    }

    /// <summary>Simplified solar zenith angle at a given location and UTC time</summary>
    public static double SolarZenithAngle(double lat, double lon, DateTime utc)
    {
        int dayOfYear = utc.DayOfYear;
        double hourUtc = utc.Hour + utc.Minute / 60.0;

        // Solar declination (simplified)
        double declination = -23.44 * Math.Cos(ToRad(360.0 / 365.0 * (dayOfYear + 10)));

        // Hour angle
        double solarNoon = 12.0 - lon / 15.0;
        double hourAngle = 15.0 * (hourUtc - solarNoon);

        // Zenith angle
        double cosZenith = Math.Sin(ToRad(lat)) * Math.Sin(ToRad(declination)) +
                           Math.Cos(ToRad(lat)) * Math.Cos(ToRad(declination)) * Math.Cos(ToRad(hourAngle));
        return ToDeg(Math.Acos(Math.Clamp(cosZenith, -1.0, 1.0)));
    }

    /// <summary>
    /// Calculate Maximum Usable Frequency for a path.
    /// Based on OpenHamClock's built-in estimation model.
    /// </summary>
    public static double CalculateMuf(int ssn, double distanceKm, double midLat, double midLon, DateTime utc)
    {
        double zenith = SolarZenithAngle(midLat, midLon, utc);
        int hops = Math.Max(1, (int)Math.Ceiling(distanceKm / 3500.0));

        // foF2: Critical frequency of F2 layer
        // Base increases with SSN
        double ssnFactor = 4.0 + ssn * 0.035;

        // Day/night variation
        double dayNightFactor;
        if (zenith < 90)
        {
            // Daytime: higher foF2
            dayNightFactor = Math.Pow(Math.Cos(ToRad(zenith)), 0.3);
        }
        else
        {
            // Nighttime: foF2 drops but doesn't go to zero
            dayNightFactor = 0.2 + 0.1 * Math.Cos(ToRad(Math.Min(zenith, 120) - 90));
        }

        // Latitude factor: foF2 higher near equator
        double latFactor = 1.0 + 0.3 * Math.Cos(ToRad(midLat * 2));

        double foF2 = ssnFactor * dayNightFactor * latFactor;

        // M-factor (depends on hop distance / elevation angle)
        double hopDistance = distanceKm / hops;
        double mFactor = 1.0 + 2.5 * Math.Sin(ToRad(Math.Min(90, hopDistance / 3500.0 * 90)));

        double muf = foF2 * mFactor;

        // Multi-hop degradation: each additional hop reduces effective MUF
        if (hops > 1)
            muf *= Math.Pow(0.90, hops - 1);

        return Math.Max(2.0, muf);
    }

    /// <summary>
    /// Calculate Lowest Usable Frequency (D-layer absorption model).
    /// </summary>
    public static double CalculateLuf(int ssn, double distanceKm, double midLat, double midLon, DateTime utc, int kIndex)
    {
        double zenith = SolarZenithAngle(midLat, midLon, utc);
        int hops = Math.Max(1, (int)Math.Ceiling(distanceKm / 3500.0));

        // Nighttime: D-layer largely disappears
        if (zenith >= 90)
            return 2.0 + kIndex * 0.3;

        // Daytime: D-layer absorption increases
        double cosZenith = Math.Cos(ToRad(zenith));
        double baseLuf = 2.0 + (ssn * 0.02 + 1.5) * Math.Pow(cosZenith, 0.5);

        // Multi-hop increases absorption
        baseLuf *= 1.0 + 0.2 * (hops - 1);

        // K-index storms increase absorption
        baseLuf += kIndex * 0.5;

        return Math.Max(1.8, Math.Min(baseLuf, 15.0));
    }

    /// <summary>
    /// Calculate reliability (0-99%) for a frequency on a given path.
    /// Based on where the frequency sits relative to MUF and LUF.
    /// </summary>
    public static int CalculateReliability(double freqMHz, double mufMHz, double lufMHz, int kIndex, int hops)
    {
        // Above MUF: signal passes through ionosphere
        if (freqMHz > mufMHz)
            return 0;

        // Below LUF: signal absorbed by D-layer
        if (freqMHz < lufMHz)
            return 0;

        double usableRange = mufMHz - lufMHz;
        if (usableRange <= 0) return 0;

        // FOT (Frequency of Optimum Traffic) = 0.85 * MUF
        double fot = mufMHz * 0.85;
        double fotPosition = (fot - lufMHz) / usableRange;
        double positionInRange = (freqMHz - lufMHz) / usableRange;

        // Bell curve peaked at FOT
        double deviation = Math.Abs(positionInRange - fotPosition);
        double baseReliability = 90.0 * Math.Exp(-2.0 * deviation * deviation);

        // K-index degradation
        double kDegradation = 1.0;
        if (kIndex >= 4)
            kDegradation = Math.Max(0.1, 1.0 - (kIndex - 3) * 0.2);
        else if (kIndex >= 2)
            kDegradation = 1.0 - (kIndex - 1) * 0.05;
        baseReliability *= kDegradation;

        // Multi-hop penalty
        baseReliability *= Math.Pow(0.92, Math.Max(0, hops - 1));

        return (int)Math.Clamp(Math.Round(baseReliability), 0, 99);
    }

    #endregion

    #region Helpers

    private static string MakeCacheKey(double deLat, double deLon, double dxLat, double dxLon)
        => $"{Math.Round(deLat, 1)},{Math.Round(deLon, 1)},{Math.Round(dxLat, 1)},{Math.Round(dxLon, 1)}";

    private static string NormalizeCondition(string condition)
    {
        // N0NBH returns "Good", "Fair", "Poor" — normalize casing
        return condition switch
        {
            var c when c.Contains("good", StringComparison.OrdinalIgnoreCase) => "Good",
            var c when c.Contains("fair", StringComparison.OrdinalIgnoreCase) => "Fair",
            var c when c.Contains("poor", StringComparison.OrdinalIgnoreCase) => "Poor",
            _ => condition
        };
    }

    private static double ToRad(double deg) => deg * Math.PI / 180.0;
    private static double ToDeg(double rad) => rad * 180.0 / Math.PI;

    #endregion
}
