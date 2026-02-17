using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public partial class AdifService : IAdifService
{
    private readonly IQsoRepository _qsoRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IHubContext<LogHub, ILogHubClient> _hub;
    private readonly ILogger<AdifService> _logger;

    // ADIF field pattern: <fieldname:length>value or <fieldname:length:type>value
    [GeneratedRegex(@"<(\w+):(\d+)(?::\w)?>([\s\S]*?)(?=<[A-Za-z_]|\Z)", RegexOptions.IgnoreCase)]
    private static partial Regex AdifFieldPattern();

    // End of header marker
    [GeneratedRegex(@"<EOH>", RegexOptions.IgnoreCase)]
    private static partial Regex EndOfHeaderPattern();

    // End of record marker
    [GeneratedRegex(@"<EOR>", RegexOptions.IgnoreCase)]
    private static partial Regex EndOfRecordPattern();

    // Numeric fields that should be converted
    private static readonly HashSet<string> NumericFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "freq", "freq_rx", "tx_pwr", "rx_pwr", "distance", "cqz", "ituz", "dxcc",
        "my_cq_zone", "my_dxcc", "my_itu_zone", "a_index", "k_index", "sfi"
    };

    public AdifService(
        IQsoRepository qsoRepository,
        ISettingsRepository settingsRepository,
        IHubContext<LogHub, ILogHubClient> hub,
        ILogger<AdifService> logger)
    {
        _qsoRepository = qsoRepository;
        _settingsRepository = settingsRepository;
        _hub = hub;
        _logger = logger;
    }

    public IEnumerable<Qso> ParseAdif(string adifContent)
    {
        // Skip header if present
        var eohMatch = EndOfHeaderPattern().Match(adifContent);
        var content = eohMatch.Success ? adifContent[(eohMatch.Index + eohMatch.Length)..] : adifContent;

        // Split by end of record marker
        var records = EndOfRecordPattern().Split(content);

        foreach (var record in records)
        {
            var trimmedRecord = record.Trim();
            if (string.IsNullOrEmpty(trimmedRecord)) continue;

            var qso = ParseRecord(trimmedRecord);
            if (qso != null)
            {
                yield return qso;
            }
        }
    }

    public IEnumerable<Qso> ParseAdif(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = reader.ReadToEnd();
        return ParseAdif(content).ToList();
    }

    public string ExportToAdif(IEnumerable<Qso> qsos, string? stationCallsign = null)
    {
        var sb = new StringBuilder();

        // ADIF Header
        sb.AppendLine("Log4YM ADIF Export");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        AppendAdifField(sb, "ADIF_VER", "3.1.4");
        AppendAdifField(sb, "PROGRAMID", "Log4YM");
        AppendAdifField(sb, "PROGRAMVERSION", "1.0");
        sb.AppendLine("<EOH>");
        sb.AppendLine();

        // QSO Records
        foreach (var qso in qsos)
        {
            ExportQsoRecord(sb, qso, stationCallsign);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async Task<AdifImportResult> ImportAdifAsync(Stream stream, bool skipDuplicates = true, bool markAsSyncedToQrz = true, bool clearExistingLogs = false)
    {
        var qsos = ParseAdif(stream).ToList();
        var importedCount = 0;
        var skippedDuplicates = 0;
        var errorCount = 0;
        var errors = new List<string>();

        _logger.LogInformation("Importing {Count} QSO records from ADIF (markAsSynced={Synced}, clearExisting={Clear})",
            qsos.Count, markAsSyncedToQrz, clearExistingLogs);

        // Clear existing logs if requested
        if (clearExistingLogs)
        {
            var deletedCount = await _qsoRepository.DeleteAllAsync();
            _logger.LogInformation("Cleared {Count} existing QSOs before import", deletedCount);
        }

        foreach (var qso in qsos)
        {
            try
            {
                if (skipDuplicates && !clearExistingLogs)
                {
                    // Check for duplicate based on callsign, date, time, band, and mode
                    // Skip check if we just cleared all logs
                    var isDuplicate = await _qsoRepository.ExistsAsync(
                        qso.Callsign,
                        qso.QsoDate,
                        qso.TimeOn,
                        qso.Band,
                        qso.Mode
                    );

                    if (isDuplicate)
                    {
                        skippedDuplicates++;
                        continue;
                    }
                }

                qso.CreatedAt = DateTime.UtcNow;
                qso.UpdatedAt = DateTime.UtcNow;

                // Mark as already synced to QRZ if requested (useful for QRZ exports)
                if (markAsSyncedToQrz)
                {
                    qso.QrzSyncStatus = SyncStatus.Synced;
                    qso.QrzSyncedAt = DateTime.UtcNow;
                    // Use a placeholder ID to indicate it came from QRZ import
                    qso.QrzLogId = $"imported-{DateTime.UtcNow:yyyyMMddHHmmss}";
                }

                var created = await _qsoRepository.CreateAsync(qso);
                importedCount++;

                // Broadcast to connected clients
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
            }
            catch (Exception ex)
            {
                errorCount++;
                errors.Add($"Error importing QSO with {qso.Callsign}: {ex.Message}");
                _logger.LogWarning(ex, "Error importing QSO with {Callsign}", qso.Callsign);
            }
        }

        _logger.LogInformation(
            "ADIF import complete: {Imported} imported, {Skipped} duplicates skipped, {Errors} errors",
            importedCount, skippedDuplicates, errorCount);

        return new AdifImportResult(qsos.Count, importedCount, skippedDuplicates, errorCount, errors);
    }

    public async Task<string> ExportQsosAsync(AdifExportRequest? request = null)
    {
        var searchRequest = new QsoSearchRequest(
            Callsign: request?.Callsign,
            Band: request?.Band,
            Mode: request?.Mode,
            FromDate: request?.FromDate,
            ToDate: request?.ToDate,
            Limit: 100000 // Large limit for export
        );

        var (qsos, _) = await _qsoRepository.SearchAsync(searchRequest);
        var qsoList = qsos.ToList();

        // If specific IDs were requested, filter to those
        if (request?.QsoIds?.Any() == true)
        {
            var idSet = new HashSet<string>(request.QsoIds);
            qsoList = qsoList.Where(q => idSet.Contains(q.Id)).ToList();
        }

        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        return ExportToAdif(qsoList, settings.Station.Callsign);
    }

    private Qso? ParseRecord(string record)
    {
        var fields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var extraFields = new BsonDocument();

        foreach (Match match in AdifFieldPattern().Matches(record))
        {
            var fieldName = match.Groups[1].Value.ToLowerInvariant();
            var length = int.Parse(match.Groups[2].Value);
            var rawValue = match.Groups[3].Value;

            // Truncate to specified length
            var value = rawValue.Length > length ? rawValue[..length] : rawValue;
            value = value.Trim();

            if (string.IsNullOrEmpty(value)) continue;

            // Convert numeric fields
            if (NumericFields.Contains(fieldName))
            {
                if (value.Contains('.'))
                {
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dVal))
                        fields[fieldName] = dVal;
                }
                else
                {
                    if (int.TryParse(value, out var iVal))
                        fields[fieldName] = iVal;
                }
            }
            else
            {
                fields[fieldName] = value;
            }
        }

        // Validate required fields
        if (!fields.TryGetValue("call", out var callObj) || callObj is not string call)
        {
            _logger.LogWarning("Skipping ADIF record: missing CALL field");
            return null;
        }

        if (!fields.TryGetValue("qso_date", out var dateObj) || dateObj is not string dateStr)
        {
            _logger.LogWarning("Skipping ADIF record for {Call}: missing QSO_DATE field", call);
            return null;
        }

        // Parse date and time
        DateTime qsoDateTime;
        if (fields.TryGetValue("time_on", out var timeObj) && timeObj is string timeStr)
        {
            var paddedTime = timeStr.PadRight(6, '0')[..6];
            if (!DateTime.TryParseExact($"{dateStr}{paddedTime}", "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out qsoDateTime))
            {
                if (!DateTime.TryParseExact(dateStr, "yyyyMMdd",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out qsoDateTime))
                {
                    _logger.LogWarning("Skipping ADIF record for {Call}: invalid date format", call);
                    return null;
                }
            }
        }
        else
        {
            if (!DateTime.TryParseExact(dateStr, "yyyyMMdd",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out qsoDateTime))
            {
                _logger.LogWarning("Skipping ADIF record for {Call}: invalid date format", call);
                return null;
            }
        }

        var qso = new Qso
        {
            Callsign = call.ToUpperInvariant(),
            QsoDate = DateTime.SpecifyKind(qsoDateTime, DateTimeKind.Utc),
            TimeOn = GetStringField(fields, "time_on") ?? qsoDateTime.ToString("HHmm"),
            TimeOff = GetStringField(fields, "time_off"),
            Band = GetStringField(fields, "band") ?? DeriveFromFrequency(fields),
            Mode = GetStringField(fields, "mode") ?? "SSB",
            Frequency = GetDoubleField(fields, "freq"),
            RstSent = GetStringField(fields, "rst_sent"),
            RstRcvd = GetStringField(fields, "rst_rcvd"),
            Name = GetStringField(fields, "name"),
            Country = GetStringField(fields, "country"),
            Grid = GetStringField(fields, "gridsquare"),
            Dxcc = GetIntField(fields, "dxcc"),
            Continent = GetStringField(fields, "cont"),
            Comment = GetStringField(fields, "comment"),
            Notes = GetStringField(fields, "notes"),
            Station = new StationInfo
            {
                Name = GetStringField(fields, "name"),
                Grid = GetStringField(fields, "gridsquare"),
                Country = GetStringField(fields, "country"),
                Dxcc = GetIntField(fields, "dxcc"),
                CqZone = GetIntField(fields, "cqz"),
                ItuZone = GetIntField(fields, "ituz"),
                State = GetStringField(fields, "state"),
                Continent = GetStringField(fields, "cont"),
                Latitude = GetDoubleField(fields, "lat"),
                Longitude = GetDoubleField(fields, "lon")
            },
            Qsl = new QslStatus
            {
                Sent = GetStringField(fields, "qsl_sent"),
                SentDate = ParseDateField(GetStringField(fields, "qslsdate")),
                Rcvd = GetStringField(fields, "qsl_rcvd"),
                RcvdDate = ParseDateField(GetStringField(fields, "qslrdate")),
                Lotw = new LotwStatus
                {
                    Sent = GetStringField(fields, "lotw_qsl_sent"),
                    Rcvd = GetStringField(fields, "lotw_qsl_rcvd")
                },
                Eqsl = new EqslStatus
                {
                    Sent = GetStringField(fields, "eqsl_qsl_sent"),
                    Rcvd = GetStringField(fields, "eqsl_qsl_rcvd")
                }
            },
            Contest = fields.ContainsKey("contest_id") ? new ContestInfo
            {
                ContestId = GetStringField(fields, "contest_id"),
                SerialSent = GetStringField(fields, "stx") ?? GetStringField(fields, "stx_string"),
                SerialRcvd = GetStringField(fields, "srx") ?? GetStringField(fields, "srx_string"),
                Exchange = GetStringField(fields, "srx_string")
            } : null
        };

        // Store unmapped ADIF fields in AdifExtra
        var mappedFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "call", "qso_date", "time_on", "time_off", "band", "mode", "freq",
            "rst_sent", "rst_rcvd", "name", "country", "gridsquare", "dxcc", "cont",
            "comment", "notes", "cqz", "ituz", "state", "lat", "lon",
            "qsl_sent", "qslsdate", "qsl_rcvd", "qslrdate",
            "lotw_qsl_sent", "lotw_qsl_rcvd", "eqsl_qsl_sent", "eqsl_qsl_rcvd",
            "contest_id", "stx", "stx_string", "srx", "srx_string"
        };

        foreach (var kvp in fields)
        {
            if (!mappedFields.Contains(kvp.Key))
            {
                // Convert all extra fields to strings to avoid BSON type conflicts
                // (e.g., when importing "Y"/"N" values that might be stored as BsonBoolean elsewhere)
                var stringValue = kvp.Value?.ToString() ?? string.Empty;
                extraFields[kvp.Key] = new MongoDB.Bson.BsonString(stringValue);
            }
        }

        if (extraFields.ElementCount > 0)
        {
            qso.AdifExtra = NormalizeBsonDocument(extraFields);
        }

        return qso;
    }

    private void ExportQsoRecord(StringBuilder sb, Qso qso, string? stationCallsign)
    {
        // Required fields
        AppendAdifField(sb, "CALL", qso.Callsign);
        AppendAdifField(sb, "QSO_DATE", qso.QsoDate.ToString("yyyyMMdd"));
        AppendAdifField(sb, "TIME_ON", FormatTime(qso.TimeOn));
        AppendAdifField(sb, "BAND", qso.Band);
        AppendAdifField(sb, "MODE", qso.Mode);

        // Optional standard fields
        if (!string.IsNullOrEmpty(qso.TimeOff))
            AppendAdifField(sb, "TIME_OFF", FormatTime(qso.TimeOff));

        if (qso.Frequency.HasValue)
            AppendAdifField(sb, "FREQ", (qso.Frequency.Value / 1000.0).ToString("F6", CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(qso.RstSent))
            AppendAdifField(sb, "RST_SENT", qso.RstSent);

        if (!string.IsNullOrEmpty(qso.RstRcvd))
            AppendAdifField(sb, "RST_RCVD", qso.RstRcvd);

        // Station info
        var name = qso.Name ?? qso.Station?.Name;
        if (!string.IsNullOrEmpty(name))
            AppendAdifField(sb, "NAME", name);

        var grid = qso.Grid ?? qso.Station?.Grid;
        if (!string.IsNullOrEmpty(grid))
            AppendAdifField(sb, "GRIDSQUARE", grid);

        var country = qso.Country ?? qso.Station?.Country;
        if (!string.IsNullOrEmpty(country))
            AppendAdifField(sb, "COUNTRY", country);

        var dxcc = qso.Dxcc ?? qso.Station?.Dxcc;
        if (dxcc.HasValue)
            AppendAdifField(sb, "DXCC", dxcc.Value.ToString());

        var continent = qso.Continent ?? qso.Station?.Continent;
        if (!string.IsNullOrEmpty(continent))
            AppendAdifField(sb, "CONT", continent);

        if (qso.Station?.CqZone.HasValue == true)
            AppendAdifField(sb, "CQZ", qso.Station.CqZone.Value.ToString());

        if (qso.Station?.ItuZone.HasValue == true)
            AppendAdifField(sb, "ITUZ", qso.Station.ItuZone.Value.ToString());

        if (!string.IsNullOrEmpty(qso.Station?.State))
            AppendAdifField(sb, "STATE", qso.Station.State);

        if (qso.Station?.Latitude.HasValue == true)
            AppendAdifField(sb, "LAT", qso.Station.Latitude.Value.ToString("F6", CultureInfo.InvariantCulture));

        if (qso.Station?.Longitude.HasValue == true)
            AppendAdifField(sb, "LON", qso.Station.Longitude.Value.ToString("F6", CultureInfo.InvariantCulture));

        // Comments
        if (!string.IsNullOrEmpty(qso.Comment))
            AppendAdifField(sb, "COMMENT", qso.Comment);

        if (!string.IsNullOrEmpty(qso.Notes))
            AppendAdifField(sb, "NOTES", qso.Notes);

        // QSL status
        if (qso.Qsl != null)
        {
            if (!string.IsNullOrEmpty(qso.Qsl.Sent))
                AppendAdifField(sb, "QSL_SENT", qso.Qsl.Sent);

            if (qso.Qsl.SentDate.HasValue)
                AppendAdifField(sb, "QSLSDATE", qso.Qsl.SentDate.Value.ToString("yyyyMMdd"));

            if (!string.IsNullOrEmpty(qso.Qsl.Rcvd))
                AppendAdifField(sb, "QSL_RCVD", qso.Qsl.Rcvd);

            if (qso.Qsl.RcvdDate.HasValue)
                AppendAdifField(sb, "QSLRDATE", qso.Qsl.RcvdDate.Value.ToString("yyyyMMdd"));

            if (qso.Qsl.Lotw != null)
            {
                if (!string.IsNullOrEmpty(qso.Qsl.Lotw.Sent))
                    AppendAdifField(sb, "LOTW_QSL_SENT", qso.Qsl.Lotw.Sent);

                if (!string.IsNullOrEmpty(qso.Qsl.Lotw.Rcvd))
                    AppendAdifField(sb, "LOTW_QSL_RCVD", qso.Qsl.Lotw.Rcvd);
            }

            if (qso.Qsl.Eqsl != null)
            {
                if (!string.IsNullOrEmpty(qso.Qsl.Eqsl.Sent))
                    AppendAdifField(sb, "EQSL_QSL_SENT", qso.Qsl.Eqsl.Sent);

                if (!string.IsNullOrEmpty(qso.Qsl.Eqsl.Rcvd))
                    AppendAdifField(sb, "EQSL_QSL_RCVD", qso.Qsl.Eqsl.Rcvd);
            }
        }

        // Contest info
        if (qso.Contest != null)
        {
            if (!string.IsNullOrEmpty(qso.Contest.ContestId))
                AppendAdifField(sb, "CONTEST_ID", qso.Contest.ContestId);

            if (!string.IsNullOrEmpty(qso.Contest.SerialSent))
                AppendAdifField(sb, "STX_STRING", qso.Contest.SerialSent);

            if (!string.IsNullOrEmpty(qso.Contest.SerialRcvd))
                AppendAdifField(sb, "SRX_STRING", qso.Contest.SerialRcvd);
        }

        // Station callsign
        if (!string.IsNullOrEmpty(stationCallsign))
            AppendAdifField(sb, "STATION_CALLSIGN", stationCallsign);

        // Export extra ADIF fields that were preserved during import
        if (qso.AdifExtra != null)
        {
            foreach (var element in qso.AdifExtra)
            {
                var value = element.Value?.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    AppendAdifField(sb, element.Name.ToUpperInvariant(), value);
                }
            }
        }

        sb.Append("<EOR>");
    }

    private static void AppendAdifField(StringBuilder sb, string fieldName, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        sb.Append($"<{fieldName}:{value.Length}>{value}");
    }

    private static string? GetStringField(Dictionary<string, object> fields, string key)
    {
        return fields.TryGetValue(key, out var value) && value is string str ? str : null;
    }

    private static double? GetDoubleField(Dictionary<string, object> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value)) return null;
        return value switch
        {
            double d => d,
            int i => i,
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }

    private static int? GetIntField(Dictionary<string, object> fields, string key)
    {
        if (!fields.TryGetValue(key, out var value)) return null;
        return value switch
        {
            int i => i,
            double d => (int)d,
            string s when int.TryParse(s, out var i) => i,
            _ => null
        };
    }

    private static DateTime? ParseDateField(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return null;
        return DateTime.TryParseExact(dateStr, "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : null;
    }

    private static string FormatTime(string? time)
    {
        if (string.IsNullOrEmpty(time)) return "0000";
        return time.Replace(":", "").PadRight(4, '0')[..4];
    }

    private static string DeriveFromFrequency(Dictionary<string, object> fields)
    {
        var freq = GetDoubleField(fields, "freq");
        if (!freq.HasValue) return "20m";

        return freq.Value switch
        {
            >= 1.8 and < 2.0 => "160m",
            >= 3.5 and < 4.0 => "80m",
            >= 5.3 and < 5.5 => "60m",
            >= 7.0 and < 7.3 => "40m",
            >= 10.1 and < 10.15 => "30m",
            >= 14.0 and < 14.35 => "20m",
            >= 18.068 and < 18.168 => "17m",
            >= 21.0 and < 21.45 => "15m",
            >= 24.89 and < 24.99 => "12m",
            >= 28.0 and < 29.7 => "10m",
            >= 50.0 and < 54.0 => "6m",
            >= 144.0 and < 148.0 => "2m",
            >= 420.0 and < 450.0 => "70cm",
            _ => "20m"
        };
    }

    /// <summary>
    /// Normalizes all values in a BsonDocument to BsonString to avoid type conflicts.
    /// This prevents "Unable to cast BsonString to BsonBoolean" errors when importing
    /// ADIF files with fields that may have been stored with different BSON types.
    /// </summary>
    private static BsonDocument NormalizeBsonDocument(BsonDocument document)
    {
        var normalized = new BsonDocument();
        foreach (var element in document)
        {
            var value = element.Value;
            // Convert all values to BsonString for consistency
            normalized[element.Name] = value.BsonType switch
            {
                BsonType.String => value,
                BsonType.Null => BsonString.Empty,
                _ => new BsonString(value.ToString() ?? string.Empty)
            };
        }
        return normalized;
    }
}
