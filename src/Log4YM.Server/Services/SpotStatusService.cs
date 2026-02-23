using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public interface ISpotStatusService
{
    string? GetSpotStatus(string dxCall, string? country, double frequencyKhz, string? mode);
    void OnQsoLogged(string callsign, string? country, string band, string mode);
    Task InvalidateCacheAsync();
}

public class SpotStatusService : ISpotStatusService, IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SpotStatusService> _logger;

    // Thread-safe cache structures — keyed by country name (from QSO log)
    private HashSet<string> _workedCountries = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _workedCountryBands = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _workedCountryBandModes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    public SpotStatusService(
        IServiceProvider serviceProvider,
        ILogger<SpotStatusService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SpotStatusService starting, building cache...");
        await BuildCacheAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public string? GetSpotStatus(string dxCall, string? country, double frequencyKhz, string? mode)
    {
        if (string.IsNullOrEmpty(country))
            return null;

        var band = BandHelper.GetBand((long)(frequencyKhz * 1000));
        if (band == "Unknown")
            return null;

        var normalizedCountry = NormalizeCountryName(country);

        lock (_cacheLock)
        {
            // New DXCC - never worked this country/entity
            if (!_workedCountries.Contains(normalizedCountry))
                return "newDxcc";

            // New Band - worked this country but not on this band
            var countryBandKey = $"{normalizedCountry}:{band}";
            if (!_workedCountryBands.Contains(countryBandKey))
                return "newBand";

            // Worked - country+band+mode match (already worked this entity on this band+mode)
            if (mode != null)
            {
                var normalizedMode = NormalizeMode(mode);
                var countryBandModeKey = $"{normalizedCountry}:{band}:{normalizedMode}";
                if (_workedCountryBandModes.Contains(countryBandModeKey))
                    return "worked";
            }
        }

        // Country+band worked but not with this mode (or mode unknown)
        return null;
    }

    public void OnQsoLogged(string callsign, string? country, string band, string mode)
    {
        lock (_cacheLock)
        {
            var normalizedMode = NormalizeMode(mode);

            if (!string.IsNullOrEmpty(country))
            {
                var normalizedCountry = NormalizeCountryName(country);
                _workedCountries.Add(normalizedCountry);
                _workedCountryBands.Add($"{normalizedCountry}:{band}");

                if (!string.IsNullOrEmpty(normalizedMode))
                {
                    _workedCountryBandModes.Add($"{normalizedCountry}:{band}:{normalizedMode}");
                }
            }

            // Also index by CtyService-resolved country name
            var (ctyCountry, _) = CtyService.GetCountryFromCallsign(callsign);
            if (!string.IsNullOrEmpty(ctyCountry) && !string.Equals(ctyCountry, country, StringComparison.OrdinalIgnoreCase))
            {
                var normalizedCtyCountry = NormalizeCountryName(ctyCountry);
                _workedCountries.Add(normalizedCtyCountry);
                _workedCountryBands.Add($"{normalizedCtyCountry}:{band}");

                if (!string.IsNullOrEmpty(normalizedMode))
                {
                    _workedCountryBandModes.Add($"{normalizedCtyCountry}:{band}:{normalizedMode}");
                }
            }
        }

        _logger.LogDebug("SpotStatusService cache updated for {Callsign} on {Band} {Mode}", callsign, band, mode);
    }

    public async Task InvalidateCacheAsync()
    {
        _logger.LogInformation("SpotStatusService cache invalidation requested, rebuilding...");
        await BuildCacheAsync();
    }

    private async Task BuildCacheAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var qsoRepo = scope.ServiceProvider.GetRequiredService<IQsoRepository>();
            var allQsos = await qsoRepo.GetAllAsync();

            var newCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newCountryBands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newCountryBandModes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var qso in allQsos)
            {
                var country = qso.Country ?? qso.Station?.Country;
                var band = qso.Band;
                var mode = NormalizeMode(qso.Mode);
                var callsign = qso.Callsign?.ToUpperInvariant();

                if (string.IsNullOrEmpty(band) || string.IsNullOrEmpty(callsign))
                    continue;

                if (!string.IsNullOrEmpty(country))
                {
                    var normalizedCountry = NormalizeCountryName(country);
                    newCountries.Add(normalizedCountry);
                    newCountryBands.Add($"{normalizedCountry}:{band}");

                    if (!string.IsNullOrEmpty(mode))
                    {
                        newCountryBandModes.Add($"{normalizedCountry}:{band}:{mode}");
                    }
                }

                // Also index by CtyService-resolved country name to handle naming
                // mismatches (e.g. QSO stores "Germany" but cty.dat uses "Fed. Rep. of Germany")
                var (ctyCountry, _) = CtyService.GetCountryFromCallsign(callsign);
                if (!string.IsNullOrEmpty(ctyCountry) && !string.Equals(ctyCountry, country, StringComparison.OrdinalIgnoreCase))
                {
                    var normalizedCtyCountry = NormalizeCountryName(ctyCountry);
                    newCountries.Add(normalizedCtyCountry);
                    newCountryBands.Add($"{normalizedCtyCountry}:{band}");

                    if (!string.IsNullOrEmpty(mode))
                    {
                        newCountryBandModes.Add($"{normalizedCtyCountry}:{band}:{mode}");
                    }
                }
            }

            lock (_cacheLock)
            {
                _workedCountries = newCountries;
                _workedCountryBands = newCountryBands;
                _workedCountryBandModes = newCountryBandModes;
            }

            _logger.LogInformation(
                "SpotStatusService cache built: {CountryCount} countries, {BandCount} country+band combos, {ModeCount} country+band+mode entries",
                newCountries.Count, newCountryBands.Count, newCountryBandModes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build SpotStatusService cache");
        }
    }

    /// <summary>
    /// Maps alternative country names (from CC cluster feeds or other sources) to the ADIF entity
    /// names stored in the QSO database. CtyService now returns ADIF-standard names directly,
    /// so these aliases handle CC cluster abbreviations and informal naming differences.
    /// </summary>
    private static readonly Dictionary<string, string> CountryAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["UAE"] = "United Arab Emirates",
        ["Trinidad & Tobago"] = "Trinidad and Tobago",
        ["Ivory Coast"] = "Cote d'Ivoire",
    };

    private static string NormalizeCountryName(string country)
    {
        return CountryAliases.TryGetValue(country, out var normalized) ? normalized : country;
    }

    private static string NormalizeMode(string mode)
    {
        if (string.IsNullOrEmpty(mode))
            return mode;

        var upper = mode.ToUpperInvariant();
        return upper switch
        {
            "USB" or "LSB" => "SSB",
            "PSK31" or "PSK63" or "PSK125" => "PSK",
            _ => upper,
        };
    }
}
