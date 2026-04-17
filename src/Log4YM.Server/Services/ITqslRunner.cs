namespace Log4YM.Server.Services;

/// <summary>
/// Thin seam around the TQSL binary. Production impl uses System.Diagnostics.Process;
/// unit tests inject a fake so LotwService logic can be exercised without a real TQSL install.
/// </summary>
public interface ITqslRunner
{
    /// <summary>
    /// Run `tqsl -d -q -u &lt;adifPath&gt;` — sign and upload the given ADIF file to LOTW.
    /// Returns TQSL's exit code (see http://www.arrl.org/command-1 for the canonical list).
    /// </summary>
    Task<TqslRunResult> UploadAsync(string tqslPath, string adifPath, string? stationLocation, CancellationToken cancellationToken);

    /// <summary>
    /// Run `tqsl --version` and return the parsed version string, or null if the binary couldn't be launched.
    /// </summary>
    Task<TqslVersionResult> GetVersionAsync(string tqslPath, CancellationToken cancellationToken);
}

public record TqslRunResult(int ExitCode, string StdOut, string StdErr);

public record TqslVersionResult(bool Ok, string? Version, string? Error);
