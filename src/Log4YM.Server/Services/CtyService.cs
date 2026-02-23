using System.Text.RegularExpressions;

namespace Log4YM.Server.Services;

internal record CtyEntity(string Country, string Continent, int CqZone, int ItuZone);

internal static partial class CtyService
{
    private static readonly Lazy<Dictionary<string, CtyEntity>> PrefixMap = new(ParseEmbeddedCtyDat);
    private static readonly Lazy<Dictionary<string, string>> CountryToContinentMap = new(BuildCountryToContinentMap);

    // Regex to strip override annotations from prefixes: (#), [#], <#/#>, {aa}, ~#~
    [GeneratedRegex(@"\(\d+\)|\[\d+\]|<\d+/\d+>|\{[a-zA-Z]+\}|~\d+~")]
    private static partial Regex AnnotationRegex();

    public static (string? Country, string? Continent) GetCountryFromCallsign(string callsign)
    {
        if (string.IsNullOrEmpty(callsign))
            return (null, null);

        var map = PrefixMap.Value;

        // Longest-prefix match: try from length 6 down to 1
        for (int len = Math.Min(callsign.Length, 6); len >= 1; len--)
        {
            var prefix = callsign[..len];
            if (map.TryGetValue(prefix, out var entity))
                return (entity.Country, entity.Continent);
        }

        return (null, null);
    }

    public static string? GetContinentFromCountryName(string countryName)
    {
        if (string.IsNullOrEmpty(countryName))
            return null;

        return CountryToContinentMap.Value.TryGetValue(countryName, out var continent)
            ? continent
            : null;
    }

    public static int PrefixCount => PrefixMap.Value.Count;

    private static Dictionary<string, CtyEntity> ParseEmbeddedCtyDat()
    {
        var assembly = typeof(CtyService).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(n => n.EndsWith("cty.dat", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        return ParseCtyDat(content);
    }

    internal static Dictionary<string, CtyEntity> ParseCtyDat(string content)
    {
        var map = new Dictionary<string, CtyEntity>(StringComparer.OrdinalIgnoreCase);
        var lines = content.Split('\n');

        string? country = null;
        string? continent = null;
        int cqZone = 0;
        int ituZone = 0;

        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i].TrimEnd('\r');

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                i++;
                continue;
            }

            // Header line: no leading whitespace, colon-delimited fields
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                // Format: Country:  CQ:  ITU:  Continent:  Lat:  Lon:  UTC:  Primary Prefix:
                var fields = line.Split(':');
                if (fields.Length >= 8)
                {
                    country = fields[0].Trim();
                    int.TryParse(fields[1].Trim(), out cqZone);
                    int.TryParse(fields[2].Trim(), out ituZone);
                    continent = fields[3].Trim();
                    // fields[4] = lat, fields[5] = lon, fields[6] = utc offset
                    // fields[7] = primary prefix (may have * for WAEDC)
                    var primaryPrefix = fields[7].Trim().TrimStart('*');
                    if (!string.IsNullOrEmpty(primaryPrefix))
                    {
                        var entity = new CtyEntity(country, continent, cqZone, ituZone);
                        map.TryAdd(primaryPrefix, entity);
                    }
                }
                i++;
                continue;
            }

            // Prefix line: leading whitespace, comma-separated prefixes, semicolon terminates entity
            if (country != null && continent != null)
            {
                var entity = new CtyEntity(country, continent, cqZone, ituZone);
                var trimmed = line.Trim().TrimEnd(';');
                var prefixes = trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries);

                foreach (var rawPrefix in prefixes)
                {
                    var prefix = rawPrefix.Trim();

                    // Skip exact callsign matches (prefixed with =)
                    if (prefix.StartsWith('='))
                        continue;

                    // Strip override annotations
                    prefix = AnnotationRegex().Replace(prefix, "");

                    // Strip WAEDC indicator
                    prefix = prefix.TrimStart('*');

                    if (!string.IsNullOrEmpty(prefix))
                    {
                        map.TryAdd(prefix, entity);
                    }
                }
            }

            i++;
        }

        return map;
    }

    private static Dictionary<string, string> BuildCountryToContinentMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in PrefixMap.Value)
        {
            map.TryAdd(kvp.Value.Country, kvp.Value.Continent);
        }
        return map;
    }
}
