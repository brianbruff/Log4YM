using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Api;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Services;
using Log4YM.Server.Core.Database;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class LotwController : ControllerBase
{
    private readonly ILotwService _lotwService;
    private readonly IQsoRepository _qsoRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ILogger<LotwController> _logger;

    // Static cancellation source for sync operation - allows cancellation from another request
    private static CancellationTokenSource? _syncCancellation;
    private static readonly object _syncLock = new();

    public LotwController(
        ILotwService lotwService,
        IQsoRepository qsoRepository,
        ISettingsRepository settingsRepository,
        IHubContext<LogHub, ILogHubClient> hubContext,
        ILogger<LotwController> logger)
    {
        _lotwService = lotwService;
        _qsoRepository = qsoRepository;
        _settingsRepository = settingsRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Check TQSL installation status
    /// </summary>
    [HttpGet("installation")]
    [ProducesResponseType(typeof(LotwInstallationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<LotwInstallationResponse>> CheckInstallation()
    {
        var status = await _lotwService.CheckTqslInstallationAsync();
        return Ok(new LotwInstallationResponse(
            status.IsInstalled,
            status.TqslPath,
            status.Version,
            status.Message
        ));
    }

    /// <summary>
    /// Get list of configured station locations from TQSL
    /// </summary>
    [HttpGet("locations")]
    [ProducesResponseType(typeof(LotwStationLocationsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LotwStationLocationsResponse>> GetStationLocations()
    {
        try
        {
            var locations = await _lotwService.GetStationLocationsAsync();
            return Ok(new LotwStationLocationsResponse(locations.ToList()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Update LOTW settings
    /// </summary>
    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] LotwSettingsRequest request)
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        settings.Lotw.Enabled = request.Enabled;
        settings.Lotw.TqslPath = request.TqslPath;
        settings.Lotw.StationLocation = request.StationLocation;

        await _settingsRepository.UpsertAsync(settings);

        // Verify TQSL installation
        var status = await _lotwService.CheckTqslInstallationAsync();

        return Ok(new
        {
            Success = true,
            Message = status.Message,
            TqslInstalled = status.IsInstalled,
            Version = status.Version
        });
    }

    /// <summary>
    /// Upload selected QSOs to LOTW (manual upload)
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(LotwUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LotwUploadResponse>> UploadQsos([FromBody] LotwUploadRequest request)
    {
        if (request.QsoIds == null || !request.QsoIds.Any())
        {
            return BadRequest("No QSO IDs provided");
        }

        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        if (string.IsNullOrEmpty(settings.Lotw.StationLocation))
        {
            return BadRequest("Station location not configured. Please select a station location in LOTW settings.");
        }

        _logger.LogInformation("Uploading {Count} QSOs to LOTW", request.QsoIds.Count());

        var qsos = await _qsoRepository.GetByIdsAsync(request.QsoIds);
        var qsoList = qsos.ToList();

        if (qsoList.Count == 0)
        {
            return BadRequest("No valid QSOs found for the provided IDs");
        }

        var result = await _lotwService.SignAndUploadAsync(qsoList, settings.Lotw.StationLocation);

        // Update sync status for successfully uploaded QSOs
        if (result.Success && result.UploadedCount > 0)
        {
            foreach (var qso in qsoList)
            {
                await _qsoRepository.UpdateLotwSyncStatusAsync(qso.Id);
            }
        }

        return Ok(new LotwUploadResponse(
            result.TotalCount,
            result.UploadedCount,
            result.FailedCount,
            result.Success,
            result.Message,
            result.Errors?.ToList()
        ));
    }

    /// <summary>
    /// Sync unsynced/modified QSOs to LOTW with progress updates via SignalR.
    /// Only syncs QSOs that are new (NotSynced) or have been modified since last sync (Modified).
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(LotwUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LotwUploadResponse>> SyncAllQsos()
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        if (string.IsNullOrEmpty(settings.Lotw.StationLocation))
        {
            return BadRequest("Station location not configured. Please select a station location in LOTW settings.");
        }

        // Create new cancellation source for this sync operation
        CancellationTokenSource cts;
        lock (_syncLock)
        {
            // Cancel any existing sync
            _syncCancellation?.Cancel();
            _syncCancellation?.Dispose();
            _syncCancellation = new CancellationTokenSource();
            cts = _syncCancellation;
        }

        try
        {
            // Get only QSOs that need syncing (NotSynced or Modified status)
            var unsyncedQsos = await _qsoRepository.GetUnsyncedToLotwAsync();
            var qsoList = unsyncedQsos.ToList();

            if (qsoList.Count == 0)
            {
                await _hubContext.BroadcastLotwSyncProgress(new LotwSyncProgressEvent(
                    Total: 0,
                    Completed: 0,
                    Successful: 0,
                    Failed: 0,
                    IsComplete: true,
                    CurrentCallsign: null,
                    Message: "All QSOs already synced to LOTW"
                ));
                return Ok(new LotwUploadResponse(0, 0, 0, true, "All QSOs already synced", null));
            }

            _logger.LogInformation("Starting LOTW sync for {Count} QSOs (new + modified)", qsoList.Count);

            // Send initial progress immediately
            await _hubContext.BroadcastLotwSyncProgress(new LotwSyncProgressEvent(
                Total: qsoList.Count,
                Completed: 0,
                Successful: 0,
                Failed: 0,
                IsComplete: false,
                CurrentCallsign: null,
                Message: $"Starting sync of {qsoList.Count} QSOs to LOTW..."
            ));

            // Progress reporter
            var progress = new Progress<LotwUploadProgress>(p =>
            {
                _hubContext.BroadcastLotwSyncProgress(new LotwSyncProgressEvent(
                    Total: p.TotalCount,
                    Completed: p.CompletedCount,
                    Successful: p.CompletedCount,
                    Failed: 0,
                    IsComplete: false,
                    CurrentCallsign: p.CurrentCallsign,
                    Message: p.Status
                )).Wait();
            });

            // Upload all QSOs in a batch to LOTW
            var result = await _lotwService.SignAndUploadAsync(qsoList, settings.Lotw.StationLocation, progress, cts.Token);

            // Update sync status for successfully uploaded QSOs
            if (result.Success && result.UploadedCount > 0)
            {
                foreach (var qso in qsoList)
                {
                    await _qsoRepository.UpdateLotwSyncStatusAsync(qso.Id);
                }
            }

            // Send final progress
            await _hubContext.BroadcastLotwSyncProgress(new LotwSyncProgressEvent(
                Total: result.TotalCount,
                Completed: result.UploadedCount,
                Successful: result.UploadedCount,
                Failed: result.FailedCount,
                IsComplete: true,
                CurrentCallsign: null,
                Message: result.Message ?? "Sync complete"
            ));

            return Ok(new LotwUploadResponse(
                result.TotalCount,
                result.UploadedCount,
                result.FailedCount,
                result.Success,
                result.Message,
                result.Errors?.ToList()
            ));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("LOTW sync was cancelled");
            await _hubContext.BroadcastLotwSyncProgress(new LotwSyncProgressEvent(
                Total: 0,
                Completed: 0,
                Successful: 0,
                Failed: 0,
                IsComplete: true,
                CurrentCallsign: null,
                Message: "Sync cancelled"
            ));
            return Ok(new LotwUploadResponse(0, 0, 0, false, "Sync cancelled by user", null));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LOTW sync");
            await _hubContext.BroadcastLotwSyncProgress(new LotwSyncProgressEvent(
                Total: 0,
                Completed: 0,
                Successful: 0,
                Failed: 0,
                IsComplete: true,
                CurrentCallsign: null,
                Message: $"Sync error: {ex.Message}"
            ));
            return BadRequest(new { Message = $"Sync error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Cancel ongoing LOTW sync operation
    /// </summary>
    [HttpPost("sync/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CancelSync()
    {
        lock (_syncLock)
        {
            _syncCancellation?.Cancel();
            _syncCancellation?.Dispose();
            _syncCancellation = null;
        }

        _logger.LogInformation("LOTW sync cancellation requested");
        return Ok(new { Message = "Sync cancellation requested" });
    }
}
