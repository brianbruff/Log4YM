using System.Diagnostics;

namespace Log4YM.Server.Services;

public class TqslRunner : ITqslRunner
{
    private readonly ILogger<TqslRunner> _logger;

    public TqslRunner(ILogger<TqslRunner> logger)
    {
        _logger = logger;
    }

    public async Task<TqslRunResult> UploadAsync(string tqslPath, string adifPath, string? stationLocation, CancellationToken cancellationToken)
    {
        // -d: don't prompt for duplicates (treat as already sent)
        // -q: quit after upload (no GUI)
        // -u: upload to LoTW
        // -l <name>: use the named station location (optional)
        var args = new List<string> { "-d", "-q" };
        if (!string.IsNullOrWhiteSpace(stationLocation))
        {
            args.Add("-l");
            args.Add(stationLocation);
        }
        args.Add("-u");
        args.Add(adifPath);

        var psi = new ProcessStartInfo
        {
            FileName = tqslPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Drain stdout/stderr concurrently so TQSL doesn't block on a full pipe
        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        _logger.LogInformation("TQSL exited with code {ExitCode}", process.ExitCode);
        // Bumped to Info so operators can confirm TQSL's own view of the transaction
        // (how many QSOs it extracted, how many uploaded) without toggling debug logging.
        if (!string.IsNullOrWhiteSpace(stdOut)) _logger.LogInformation("TQSL stdout: {StdOut}", stdOut);
        if (!string.IsNullOrWhiteSpace(stdErr)) _logger.LogInformation("TQSL stderr: {StdErr}", stdErr);

        return new TqslRunResult(process.ExitCode, stdOut, stdErr);
    }

    public async Task<TqslVersionResult> GetVersionAsync(string tqslPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tqslPath))
        {
            return new TqslVersionResult(false, null, "TQSL path is empty");
        }

        var psi = new ProcessStartInfo
        {
            FileName = tqslPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("--version");

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            var stdOut = (await stdOutTask).Trim();
            var stdErr = (await stdErrTask).Trim();

            // TQSL prints its version to stderr on some platforms, stdout on others.
            var combined = !string.IsNullOrEmpty(stdOut) ? stdOut : stdErr;

            if (process.ExitCode == 0 || !string.IsNullOrWhiteSpace(combined))
            {
                return new TqslVersionResult(true, combined.Length > 0 ? combined : null, null);
            }

            return new TqslVersionResult(false, null, $"TQSL exited with code {process.ExitCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invoke TQSL at {Path}", tqslPath);
            return new TqslVersionResult(false, null, ex.Message);
        }
    }
}
