using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HamlibController : ControllerBase
{
    private readonly ILogger<HamlibController> _logger;

    public HamlibController(ILogger<HamlibController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get list of all Hamlib-supported rotator models
    /// </summary>
    [HttpGet("rotators")]
    [ProducesResponseType(typeof(List<RotatorModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<RotatorModel>>> GetRotatorModels()
    {
        try
        {
            var models = await GetHamlibRotatorListAsync();
            return Ok(models);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Hamlib rotator models");
            return StatusCode(500, new { error = "Failed to retrieve rotator models", message = ex.Message });
        }
    }

    /// <summary>
    /// Execute rotctl -l command and parse the output to get supported rotator models
    /// </summary>
    private async Task<List<RotatorModel>> GetHamlibRotatorListAsync()
    {
        var models = new List<RotatorModel>();

        try
        {
            // Try to find rotctl command (platform-specific)
            string rotctlCommand = GetRotctlCommand();

            var processInfo = new ProcessStartInfo
            {
                FileName = rotctlCommand,
                Arguments = "-l",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                _logger.LogWarning("rotctl -l exited with code {ExitCode}: {Error}", process.ExitCode, error);

                // Return hardcoded common models as fallback
                return GetCommonRotatorModels();
            }

            // Parse the output
            // Expected format: "  1  Dummy    Dummy rot"
            // Format: ModelID  Manufacturer  Model
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var regex = new Regex(@"^\s*(\d+)\s+(\S+)\s+(.+)$");

            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    var modelId = int.Parse(match.Groups[1].Value);
                    var manufacturer = match.Groups[2].Value.Trim();
                    var modelName = match.Groups[3].Value.Trim();

                    // Skip dummy/test models
                    if (manufacturer.Equals("Dummy", StringComparison.OrdinalIgnoreCase))
                        continue;

                    models.Add(new RotatorModel
                    {
                        ModelId = modelId,
                        Manufacturer = manufacturer,
                        ModelName = modelName,
                        DisplayName = $"{manufacturer} {modelName}"
                    });
                }
            }

            _logger.LogInformation("Found {Count} Hamlib rotator models", models.Count);
            return models.OrderBy(m => m.Manufacturer).ThenBy(m => m.ModelName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute rotctl -l command");

            // Return hardcoded common models as fallback
            return GetCommonRotatorModels();
        }
    }

    /// <summary>
    /// Get the platform-specific rotctl command path
    /// </summary>
    private string GetRotctlCommand()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows, try common installation paths
            var windowsPaths = new[]
            {
                @"C:\Program Files\Hamlib\bin\rotctl.exe",
                @"C:\Program Files (x86)\Hamlib\bin\rotctl.exe",
                "rotctl.exe" // Fallback to PATH
            };

            foreach (var path in windowsPaths)
            {
                if (System.IO.File.Exists(path))
                    return path;
            }

            return "rotctl.exe"; // Hope it's in PATH
        }
        else
        {
            // On Linux/macOS, rotctl should be in PATH
            return "rotctl";
        }
    }

    /// <summary>
    /// Hardcoded list of common rotator models as fallback when rotctl is not available
    /// </summary>
    private List<RotatorModel> GetCommonRotatorModels()
    {
        return new List<RotatorModel>
        {
            new() { ModelId = 1, Manufacturer = "Dummy", ModelName = "Dummy rot", DisplayName = "Dummy Dummy rot" },
            new() { ModelId = 201, Manufacturer = "Easycomm", ModelName = "EasyComm 1", DisplayName = "Easycomm EasyComm 1" },
            new() { ModelId = 202, Manufacturer = "Easycomm", ModelName = "EasyComm 2", DisplayName = "Easycomm EasyComm 2" },
            new() { ModelId = 203, Manufacturer = "Easycomm", ModelName = "EasyComm 3", DisplayName = "Easycomm EasyComm 3" },
            new() { ModelId = 601, Manufacturer = "Yaesu", ModelName = "GS-232A", DisplayName = "Yaesu GS-232A" },
            new() { ModelId = 602, Manufacturer = "Yaesu", ModelName = "GS-232", DisplayName = "Yaesu GS-232" },
            new() { ModelId = 603, Manufacturer = "Yaesu", ModelName = "GS-232B", DisplayName = "Yaesu GS-232B" },
            new() { ModelId = 604, Manufacturer = "F1TE", ModelName = "Yaesu GS-232 (GS-232A) emulation", DisplayName = "F1TE Yaesu GS-232 (GS-232A) emulation" },
            new() { ModelId = 801, Manufacturer = "RotorEZ", ModelName = "RCI AzEl", DisplayName = "RotorEZ RCI AzEl" },
            new() { ModelId = 901, Manufacturer = "Heathkit", ModelName = "HD 1780 Intellirotor", DisplayName = "Heathkit HD 1780 Intellirotor" },
            new() { ModelId = 902, Manufacturer = "SPID", ModelName = "Rot1Prog/Rot2Prog", DisplayName = "SPID Rot1Prog/Rot2Prog" },
            new() { ModelId = 1001, Manufacturer = "Fodtrack", ModelName = "FodTrack", DisplayName = "Fodtrack FodTrack" },
            new() { ModelId = 1201, Manufacturer = "M2", ModelName = "RC2800", DisplayName = "M2 RC2800" },
            new() { ModelId = 2001, Manufacturer = "Hamlib", ModelName = "NET rotctl", DisplayName = "Hamlib NET rotctl" },
        };
    }
}

/// <summary>
/// Hamlib rotator model information
/// </summary>
public class RotatorModel
{
    public int ModelId { get; set; }
    public required string Manufacturer { get; set; }
    public required string ModelName { get; set; }
    public required string DisplayName { get; set; }
}
