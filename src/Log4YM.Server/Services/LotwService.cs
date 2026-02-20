using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Log4YM.Contracts.Models;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

public partial class LotwService : ILotwService
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAdifService _adifService;
    private readonly ILogger<LotwService> _logger;

    // Pattern to extract station locations from TQSL output
    [GeneratedRegex(@"^\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex StationLocationPattern();

    public LotwService(
        ISettingsRepository settingsRepository,
        IAdifService adifService,
        ILogger<LotwService> logger)
    {
        _settingsRepository = settingsRepository;
        _adifService = adifService;
        _logger = logger;
    }

    public async Task<LotwInstallationStatus> CheckTqslInstallationAsync()
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        var lotwSettings = settings.Lotw;

        // Try custom path first, then default locations
        var pathsToCheck = new List<string>();

        if (!string.IsNullOrEmpty(lotwSettings.TqslPath))
        {
            pathsToCheck.Add(lotwSettings.TqslPath);
        }

        // Add platform-specific default paths
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            pathsToCheck.Add("tqsl.exe");
            pathsToCheck.Add(@"C:\Program Files (x86)\TrustedQSL\tqsl.exe");
            pathsToCheck.Add(@"C:\Program Files\TrustedQSL\tqsl.exe");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            pathsToCheck.Add("tqsl");
            pathsToCheck.Add("/Applications/TrustedQSL.app/Contents/MacOS/tqsl");
            pathsToCheck.Add("/usr/local/bin/tqsl");
        }
        else // Linux
        {
            pathsToCheck.Add("tqsl");
            pathsToCheck.Add("/usr/bin/tqsl");
            pathsToCheck.Add("/usr/local/bin/tqsl");
        }

        foreach (var tqslPath in pathsToCheck)
        {
            try
            {
                var (exitCode, output, error) = await RunTqslCommandAsync(tqslPath, "--version");

                if (exitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    var version = ParseTqslVersion(output);

                    // Cache the result
                    lotwSettings.TqslInstalled = true;
                    lotwSettings.TqslPath = tqslPath;
                    lotwSettings.TqslVersion = version;
                    lotwSettings.InstallationCheckedAt = DateTime.UtcNow;
                    await _settingsRepository.UpsertAsync(settings);

                    _logger.LogInformation("TQSL found at {Path}, version {Version}", tqslPath, version);

                    return new LotwInstallationStatus(
                        true,
                        tqslPath,
                        version,
                        $"TQSL version {version} installed"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to check TQSL at {Path}: {Message}", tqslPath, ex.Message);
            }
        }

        // Not found
        lotwSettings.TqslInstalled = false;
        lotwSettings.InstallationCheckedAt = DateTime.UtcNow;
        await _settingsRepository.UpsertAsync(settings);

        return new LotwInstallationStatus(
            false,
            null,
            null,
            "TQSL is not installed or not found in common locations. Please install TrustedQSL from https://lotw.arrl.org/lotw-help/installation/"
        );
    }

    public async Task<IEnumerable<string>> GetStationLocationsAsync()
    {
        var installStatus = await CheckTqslInstallationAsync();
        if (!installStatus.IsInstalled || string.IsNullOrEmpty(installStatus.TqslPath))
        {
            throw new InvalidOperationException("TQSL is not installed. Please install TrustedQSL first.");
        }

        try
        {
            var (exitCode, output, error) = await RunTqslCommandAsync(installStatus.TqslPath, "-l");

            if (exitCode != 0)
            {
                _logger.LogError("TQSL station location list failed: {Error}", error);
                throw new InvalidOperationException($"Failed to get station locations: {error}");
            }

            // Parse station locations from output
            var locations = new List<string>();
            var matches = StationLocationPattern().Matches(output);
            foreach (Match match in matches)
            {
                var location = match.Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(location) && !location.StartsWith("Station Location:"))
                {
                    locations.Add(location);
                }
            }

            _logger.LogInformation("Found {Count} TQSL station locations", locations.Count);
            return locations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TQSL station locations");
            throw;
        }
    }

    public async Task<LotwUploadResult> SignAndUploadAsync(
        IEnumerable<Qso> qsos,
        string stationLocation,
        IProgress<LotwUploadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installStatus = await CheckTqslInstallationAsync();
        if (!installStatus.IsInstalled || string.IsNullOrEmpty(installStatus.TqslPath))
        {
            return new LotwUploadResult(
                false,
                0,
                0,
                0,
                "TQSL is not installed. Please install TrustedQSL first.",
                new[] { "TQSL not found" }
            );
        }

        if (string.IsNullOrWhiteSpace(stationLocation))
        {
            return new LotwUploadResult(
                false,
                0,
                0,
                0,
                "Station location is required. Please select a station location from TQSL.",
                new[] { "No station location specified" }
            );
        }

        var qsoList = qsos.ToList();
        if (qsoList.Count == 0)
        {
            return new LotwUploadResult(true, 0, 0, 0, "No QSOs to upload", null);
        }

        _logger.LogInformation("Starting LOTW upload for {Count} QSOs using station location '{Location}'",
            qsoList.Count, stationLocation);

        var errors = new List<string>();
        var tempAdifPath = Path.Combine(Path.GetTempPath(), $"log4ym_lotw_{DateTime.UtcNow:yyyyMMddHHmmss}.adi");

        try
        {
            // Generate ADIF file
            progress?.Report(new LotwUploadProgress(qsoList.Count, 0, null, "Generating ADIF file..."));
            var adifContent = GenerateLotwAdif(qsoList);
            await File.WriteAllTextAsync(tempAdifPath, adifContent, cancellationToken);

            _logger.LogDebug("Generated ADIF file at {Path}", tempAdifPath);

            // Sign and upload using TQSL
            progress?.Report(new LotwUploadProgress(qsoList.Count, 0, null, "Signing and uploading to LOTW..."));

            var args = $"-d -l \"{stationLocation}\" -u \"{tempAdifPath}\"";
            var (exitCode, output, error) = await RunTqslCommandAsync(installStatus.TqslPath, args, cancellationToken);

            if (exitCode == 0)
            {
                _logger.LogInformation("Successfully uploaded {Count} QSOs to LOTW", qsoList.Count);
                progress?.Report(new LotwUploadProgress(qsoList.Count, qsoList.Count, null, "Upload complete"));

                return new LotwUploadResult(
                    true,
                    qsoList.Count,
                    qsoList.Count,
                    0,
                    $"Successfully uploaded {qsoList.Count} QSO(s) to LOTW",
                    null
                );
            }
            else
            {
                var errorMsg = !string.IsNullOrEmpty(error) ? error : output;
                _logger.LogError("TQSL upload failed with exit code {ExitCode}: {Error}", exitCode, errorMsg);
                errors.Add($"TQSL exit code {exitCode}: {errorMsg}");

                return new LotwUploadResult(
                    false,
                    qsoList.Count,
                    0,
                    qsoList.Count,
                    $"LOTW upload failed: {errorMsg}",
                    errors
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LOTW upload");
            errors.Add(ex.Message);

            return new LotwUploadResult(
                false,
                qsoList.Count,
                0,
                qsoList.Count,
                $"Upload error: {ex.Message}",
                errors
            );
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempAdifPath))
                {
                    File.Delete(tempAdifPath);
                    _logger.LogDebug("Deleted temporary ADIF file {Path}", tempAdifPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary ADIF file {Path}", tempAdifPath);
            }
        }
    }

    public string GenerateLotwAdif(IEnumerable<Qso> qsos)
    {
        // Use the existing ADIF service to generate the ADIF content
        // LOTW uses standard ADIF format
        var settings = _settingsRepository.GetAsync().Result ?? new UserSettings();
        return _adifService.ExportToAdif(qsos, settings.Station.Callsign);
    }

    private async Task<(int exitCode, string output, string error)> RunTqslCommandAsync(
        string tqslPath,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = tqslPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running TQSL command: {Path} {Args}", tqslPath, arguments);
            throw;
        }
    }

    private static string ParseTqslVersion(string output)
    {
        // TQSL version output is typically like "TrustedQSL version 2.7.1"
        var match = Regex.Match(output, @"version\s+(\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : output.Trim();
    }
}
