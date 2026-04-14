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
    private readonly ISpotStatusService? _spotStatusService;

    // ADIF field pattern: <fieldname:length>value or <fieldname:length:type>value
    [GeneratedRegex(@"<(\w+):(\d+)(?::\w)?>([\s\S]*?)(?=<[A-Za-z_][^>]*?:\d+|\Z)", RegexOptions.IgnoreCase)]
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
        ILogger<AdifService> logger,
        ISpotStatusService? spotStatusService = null)
    {
        _qsoRepository = qsoRepository;
        _settingsRepository = settingsRepository;
        _hub = hub;
        _logger = logger;
        _spotStatusService = spotStatusService;
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

    public async Task<AdifImportResult> ImportAdifAsync(Stream stream, bool skipDuplicates = true, bool markAsSyncedToQrz = true, bool clearExistingLogs = false, CancellationToken cancellationToken = default)
    {
        // ── Step 1: Parse ────────────────────────────────────────────────────
        await _hub.BroadcastAdifImportProgress(new AdifImportProgressEvent(
            0, 0, 0, 0, 0, false, null, "Parsing ADIF file..."));

        var allParsed = ParseAdif(stream).ToList();
        var totalRecords = allParsed.Count;

        _logger.LogInformation("Importing {Count} QSO records from ADIF (markAsSynced={Synced}, clearExisting={Clear})",
            totalRecords, markAsSyncedToQrz, clearExistingLogs);

        await _hub.BroadcastAdifImportProgress(new AdifImportProgressEvent(
            totalRecords, 0, 0, 0, 0, false, null, $"Found {totalRecords} records..."));

        // ── Step 2: Clear existing logs if requested ─────────────────────────
        if (clearExistingLogs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var deletedCount = await _qsoRepository.DeleteAllAsync();
            _logger.LogInformation("Cleared {Count} existing QSOs before import", deletedCount);
        }

        // ── Step 3: Load all existing keys in ONE query ──────────────────────
        // This replaces N individual ExistsAsync() round-trips with a single
        // bulk read + in-memory HashSet lookups (O(1) per check).
        HashSet<string>? existingKeys = null;
        if (skipDuplicates && !clearExistingLogs)
        {
            await _hub.BroadcastAdifImportProgress(new AdifImportProgressEvent(
                totalRecords, 0, 0, 0, 0, false, null, "Loading existing QSOs for duplicate detection..."));

            existingKeys = await _qsoRepository.GetAllDuplicateKeysAsync();
            _logger.LogInformation("Loaded {Count} existing duplicate keys", existingKeys.Count);
        }

        // ── Step 4: Filter in-memory (no DB round-trips) ────────────────────
        var now = DateTime.UtcNow;
        var qrzLogId = markAsSyncedToQrz ? $"imported-{now:yyyyMMddHHmmss}" : null;

        // seenInFile detects duplicates within the ADIF file itself
        var seenInFile = skipDuplicates
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : null;

        var toInsert = new List<Qso>(totalRecords);
        var skippedDuplicates = 0;
        var errors = new List<string>();

        foreach (var qso in allParsed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (skipDuplicates)
            {
                var key = MakeDuplicateKey(qso);

                if (existingKeys != null && existingKeys.Contains(key))
                {
                    skippedDuplicates++;
                    continue;
                }

                if (!seenInFile!.Add(key))
                {
                    // Duplicate within the ADIF file itself
                    skippedDuplicates++;
                    continue;
                }
            }

            qso.CreatedAt = now;
            qso.UpdatedAt = now;

            if (markAsSyncedToQrz)
            {
                qso.QrzSyncStatus = SyncStatus.Synced;
                qso.QrzSyncedAt = now;
                qso.QrzLogId = qrzLogId;
            }

            toInsert.Add(qso);
        }

        _logger.LogInformation("After deduplication: {ToInsert} to insert, {Skipped} duplicates skipped",
            toInsert.Count, skippedDuplicates);

        // ── Step 5: Bulk insert in batches ───────────────────────────────────
        // No per-QSO OnQsoLogged events — the frontend refreshes via
        // importMutation.onSuccess when the HTTP POST response arrives.
        const int batchSize = 1000;
        var importedCount = 0;
        var errorCount = 0;
        var totalBatches = Math.Max(1, (toInsert.Count + batchSize - 1) / batchSize);

        for (int batchIndex = 0; batchIndex < totalBatches; batchIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch = toInsert.Skip(batchIndex * batchSize).Take(batchSize).ToList();
            if (batch.Count == 0) break;

            try
            {
                await _qsoRepository.CreateManyAsync(batch);
                importedCount += batch.Count;
            }
            catch (Exception ex)
            {
                errorCount += batch.Count;
                var errorMsg = $"Batch {batchIndex + 1} failed: {ex.Message}";
                errors.Add(errorMsg);
                _logger.LogWarning(ex, "Error importing batch {Batch}", batchIndex + 1);
            }

            await _hub.BroadcastAdifImportProgress(new AdifImportProgressEvent(
                totalRecords,
                skippedDuplicates + importedCount + errorCount,
                importedCount,
                skippedDuplicates,
                errorCount,
                false,
                batch.Last().Callsign,
                $"Inserting batch {batchIndex + 1} of {totalBatches}..."
            ));
        }

        _logger.LogInformation(
            "ADIF import complete: {Imported} imported, {Skipped} duplicates skipped, {Errors} errors",
            importedCount, skippedDuplicates, errorCount);

        // ── Step 6: Post-import housekeeping ─────────────────────────────────
        if (_spotStatusService != null && importedCount > 0)
        {
            _ = _spotStatusService.InvalidateCacheAsync();
        }

        await _hub.BroadcastAdifImportProgress(new AdifImportProgressEvent(
            totalRecords,
            totalRecords,
            importedCount,
            skippedDuplicates,
            errorCount,
            true,
            null,
            errorCount > 0 ? $"Import complete with {errorCount} error(s)" : "Import complete"
        ));

        return new AdifImportResult(totalRecords, importedCount, skippedDuplicates, errorCount, errors);
    }

    /// <summary>
    /// Builds the composite duplicate-detection key for a QSO.
    /// Must match the format produced by <see cref="IQsoRepository.GetAllDuplicateKeysAsync"/>.
    /// Uses QsoDate.Date (strips time) to match ExistsAsync behaviour.
    /// </summary>
    internal static string MakeDuplicateKey(Qso qso)
        => $"{qso.Callsign.ToUpperInvariant()}|{qso.QsoDate.Date:yyyyMMdd}|{qso.TimeOn}|{qso.Band}|{qso.Mode}";

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
                double? dVal = null;
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
                {
                    dVal = parsedDouble;
                }

                if (dVal.HasValue)
                {
                    // Convert MHz to kHz for frequency fields
                    if (fieldName == "freq" || fieldName == "freq_rx")
                    {
                        dVal *= 1000.0;
                    }
                    fields[fieldName] = dVal.Value;
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

        // Extract DXCC data from ADIF fields if present
        var country = GetStringField(fields, "country");
        var continent = GetStringField(fields, "cont");
        var dxcc = GetIntField(fields, "dxcc");

        // If DXCC data is missing or incomplete, look it up from the callsign
        if (string.IsNullOrEmpty(country) || string.IsNullOrEmpty(continent))
        {
            var (lookedUpCountry, lookedUpContinent) = CtyService.GetCountryFromCallsign(call);

            // Use looked-up values for any missing fields
            country ??= lookedUpCountry;
            continent ??= lookedUpContinent;

            // If country is provided in ADIF but continent isn't, also try country-to-continent lookup
            if (!string.IsNullOrEmpty(country) && string.IsNullOrEmpty(continent))
            {
                continent = CtyService.GetContinentFromCountryName(country);
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
            Country = country,
            Grid = GetStringField(fields, "gridsquare"),
            Dxcc = dxcc,
            Continent = continent,
            Comment = GetStringField(fields, "comment"),
            Notes = GetStringField(fields, "notes"),
            Station = new StationInfo
            {
                Name = GetStringField(fields, "name"),
                Grid = GetStringField(fields, "gridsquare"),
                Country = country,
                Dxcc = dxcc,
                CqZone = GetIntField(fields, "cqz"),
                ItuZone = GetIntField(fields, "ituz"),
                State = GetStringField(fields, "state"),
                Continent = continent,
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
                extraFields[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
        }

        if (extraFields.ElementCount > 0)
        {
            qso.AdifExtra = extraFields;
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
            AppendAdifField(sb, "FREQ", (qso.Frequency.Value / 1000.0).ToString("F7", CultureInfo.InvariantCulture));

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

        // Frequency is now in kHz - convert to Hz for BandHelper
        return BandHelper.GetBand((long)(freq.Value * 1000.0));
    }
}
