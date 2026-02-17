using Microsoft.AspNetCore.Mvc;
using Log4YM.Contracts.Api;
using Log4YM.Server.Services;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdifController : ControllerBase
{
    private readonly IAdifService _adifService;
    private readonly ILogger<AdifController> _logger;

    // Static cancellation source for import operation - allows cancellation from another request
    private static CancellationTokenSource? _importCancellation;
    private static readonly object _importLock = new();

    public AdifController(IAdifService adifService, ILogger<AdifController> logger)
    {
        _adifService = adifService;
        _logger = logger;
    }

    /// <summary>
    /// Import ADIF file into the log
    /// </summary>
    /// <param name="file">ADIF file to import (.adi, .adif, or .xml)</param>
    /// <param name="skipDuplicates">Skip duplicate QSOs (same callsign, date, time, band, mode)</param>
    /// <param name="markAsSyncedToQrz">Mark imported QSOs as already synced to QRZ (default: true, useful when importing from QRZ export)</param>
    /// <param name="clearExistingLogs">Delete all existing QSOs before import (default: false)</param>
    [HttpPost("import")]
    [ProducesResponseType(typeof(AdifImportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit
    public async Task<ActionResult<AdifImportResponse>> ImportAdif(
        IFormFile file,
        [FromQuery] bool skipDuplicates = true,
        [FromQuery] bool markAsSyncedToQrz = true,
        [FromQuery] bool clearExistingLogs = false)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file provided");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".adi" && extension != ".adif" && extension != ".xml")
        {
            return BadRequest("Invalid file type. Expected .adi, .adif, or .xml");
        }

        _logger.LogInformation("Importing ADIF file: {FileName} ({Size} bytes), skipDuplicates={Skip}, markAsSyncedToQrz={Synced}, clearExisting={Clear}",
            file.FileName, file.Length, skipDuplicates, markAsSyncedToQrz, clearExistingLogs);

        // Create new cancellation source for this import operation
        CancellationTokenSource cts;
        lock (_importLock)
        {
            // Cancel any existing import
            _importCancellation?.Cancel();
            _importCancellation?.Dispose();
            _importCancellation = new CancellationTokenSource();
            cts = _importCancellation;
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _adifService.ImportAdifAsync(stream, skipDuplicates, markAsSyncedToQrz, clearExistingLogs, cts.Token);

            return Ok(new AdifImportResponse(
                result.TotalRecords,
                result.ImportedCount,
                result.SkippedDuplicates,
                result.ErrorCount,
                result.Errors
            ));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ADIF import was cancelled by user");
            return Ok(new AdifImportResponse(0, 0, 0, 0, new[] { "Import cancelled by user" }));
        }
    }

    /// <summary>
    /// Cancel an ongoing ADIF import operation
    /// </summary>
    [HttpPost("import/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CancelImport()
    {
        lock (_importLock)
        {
            if (_importCancellation != null)
            {
                _importCancellation.Cancel();
                _logger.LogInformation("ADIF import cancellation requested");
                return Ok(new { Message = "Import cancellation requested" });
            }
        }

        return Ok(new { Message = "No import in progress" });
    }

    /// <summary>
    /// Export QSOs to ADIF format
    /// </summary>
    [HttpGet("export")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAdif(
        [FromQuery] string? callsign = null,
        [FromQuery] string? band = null,
        [FromQuery] string? mode = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var request = new AdifExportRequest(callsign, band, mode, fromDate, toDate);
        var adif = await _adifService.ExportQsosAsync(request);

        var fileName = $"log4ym_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.adi";

        return File(
            System.Text.Encoding.UTF8.GetBytes(adif),
            "text/plain",
            fileName
        );
    }

    /// <summary>
    /// Export selected QSOs to ADIF format
    /// </summary>
    [HttpPost("export")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportSelectedQsos([FromBody] AdifExportRequest request)
    {
        var adif = await _adifService.ExportQsosAsync(request);

        var fileName = $"log4ym_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.adi";

        return File(
            System.Text.Encoding.UTF8.GetBytes(adif),
            "text/plain",
            fileName
        );
    }
}
