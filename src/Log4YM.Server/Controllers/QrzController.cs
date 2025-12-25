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
public class QrzController : ControllerBase
{
    private readonly IQrzService _qrzService;
    private readonly IQsoRepository _qsoRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ILogger<QrzController> _logger;

    // Static cancellation source for sync operation - allows cancellation from another request
    private static CancellationTokenSource? _syncCancellation;
    private static readonly object _syncLock = new();

    public QrzController(
        IQrzService qrzService,
        IQsoRepository qsoRepository,
        ISettingsRepository settingsRepository,
        IHubContext<LogHub, ILogHubClient> hubContext,
        ILogger<QrzController> logger)
    {
        _qrzService = qrzService;
        _qsoRepository = qsoRepository;
        _settingsRepository = settingsRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Check QRZ subscription status
    /// </summary>
    [HttpGet("subscription")]
    [ProducesResponseType(typeof(QrzSubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QrzSubscriptionResponse>> CheckSubscription()
    {
        var status = await _qrzService.CheckSubscriptionAsync();
        return Ok(new QrzSubscriptionResponse(
            status.IsValid,
            status.HasXmlSubscription,
            status.Username,
            status.Message,
            status.ExpirationDate
        ));
    }

    /// <summary>
    /// Update QRZ settings
    /// </summary>
    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] QrzSettingsRequest request)
    {
        if (string.IsNullOrEmpty(request.Username))
        {
            return BadRequest("Username is required");
        }

        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        settings.Qrz.Username = request.Username;
        settings.Qrz.Password = request.Password;
        settings.Qrz.ApiKey = request.ApiKey ?? string.Empty;
        settings.Qrz.Enabled = request.Enabled;

        await _settingsRepository.UpsertAsync(settings);

        // Verify the credentials
        var status = await _qrzService.CheckSubscriptionAsync();

        return Ok(new
        {
            Success = true,
            Message = status.Message,
            HasXmlSubscription = status.HasXmlSubscription
        });
    }

    /// <summary>
    /// Upload selected QSOs to QRZ logbook (manual upload)
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(QrzUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QrzUploadResponse>> UploadQsos([FromBody] QrzUploadRequest request)
    {
        if (request.QsoIds == null || !request.QsoIds.Any())
        {
            return BadRequest("No QSO IDs provided");
        }

        _logger.LogInformation("Uploading {Count} QSOs to QRZ", request.QsoIds.Count());

        var qsos = await _qsoRepository.GetByIdsAsync(request.QsoIds);
        var qsoList = qsos.ToList();

        if (qsoList.Count == 0)
        {
            return BadRequest("No valid QSOs found for the provided IDs");
        }

        var result = await _qrzService.UploadQsosAsync(qsoList);

        return Ok(new QrzUploadResponse(
            result.TotalCount,
            result.SuccessCount,
            result.FailedCount,
            result.Results.Select(r => new QrzUploadResultDto(
                r.Success,
                r.LogId,
                r.Message,
                r.QsoId
            ))
        ));
    }

    /// <summary>
    /// Sync unsynced/modified QSOs to QRZ logbook with parallel uploads and progress updates via SignalR.
    /// Only syncs QSOs that are new (NotSynced) or have been modified since last sync (Modified).
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(QrzUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QrzUploadResponse>> SyncAllQsos()
    {
        var settings = await _settingsRepository.GetAsync() ?? new UserSettings();
        if (string.IsNullOrEmpty(settings.Qrz.ApiKey))
        {
            return BadRequest("QRZ API key not configured. Please configure it in Settings.");
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

        // Track progress - declared outside try so accessible in catch for cancellation
        var results = new List<QrzUploadResultDto>();
        var successCount = 0;
        var failedCount = 0;
        var completedCount = 0;
        var totalToSync = 0;

        try
        {
            // Get only QSOs that need syncing (NotSynced or Modified status)
            var unsyncedQsos = await _qsoRepository.GetUnsyncedToQrzAsync();
            var qsoList = unsyncedQsos.ToList();
            totalToSync = qsoList.Count;

            if (qsoList.Count == 0)
            {
                await _hubContext.BroadcastQrzSyncProgress(new QrzSyncProgressEvent(
                    Total: 0,
                    Completed: 0,
                    Successful: 0,
                    Failed: 0,
                    IsComplete: true,
                    CurrentCallsign: null,
                    Message: "All QSOs already synced to QRZ"
                ));
                return Ok(new QrzUploadResponse(0, 0, 0, Enumerable.Empty<QrzUploadResultDto>()));
            }

            _logger.LogInformation("Starting parallel QRZ sync for {Count} QSOs (new + modified)", qsoList.Count);

            // Send initial progress immediately
            await _hubContext.BroadcastQrzSyncProgress(new QrzSyncProgressEvent(
                Total: qsoList.Count,
                Completed: 0,
                Successful: 0,
                Failed: 0,
                IsComplete: false,
                CurrentCallsign: null,
                Message: $"Starting sync of {qsoList.Count} QSOs..."
            ));

            // Process in parallel batches with direct SignalR progress updates
            const int maxConcurrency = 5;
            using var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = qsoList.Select(async qso =>
            {
                // Check cancellation before waiting for semaphore
                cts.Token.ThrowIfCancellationRequested();
                await semaphore.WaitAsync(cts.Token);
                try
                {
                    cts.Token.ThrowIfCancellationRequested();

                    var result = await _qrzService.UploadQsoAsync(qso);
                    var uploadResult = new QrzUploadResultDto(result.Success, result.LogId, result.Message, result.QsoId);

                    lock (results)
                    {
                        results.Add(uploadResult);
                        if (result.Success)
                            successCount++;
                        else
                            failedCount++;
                        completedCount++;
                    }

                    // Update sync status for successful upload
                    // Note: QRZ may not always return a LogId, so use a placeholder if missing
                    if (result.Success && !string.IsNullOrEmpty(result.QsoId))
                    {
                        var logId = !string.IsNullOrEmpty(result.LogId)
                            ? result.LogId
                            : $"synced-{DateTime.UtcNow:yyyyMMddHHmmss}";
                        await _qsoRepository.UpdateQrzSyncStatusAsync(result.QsoId, logId);
                    }

                    // Broadcast progress directly (every upload or every few)
                    await _hubContext.BroadcastQrzSyncProgress(new QrzSyncProgressEvent(
                        Total: qsoList.Count,
                        Completed: completedCount,
                        Successful: successCount,
                        Failed: failedCount,
                        IsComplete: false,
                        CurrentCallsign: qso.Callsign,
                        Message: $"Uploaded {qso.Callsign} ({completedCount}/{qsoList.Count})"
                    ));

                    return uploadResult;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            // Send completion
            await _hubContext.BroadcastQrzSyncProgress(new QrzSyncProgressEvent(
                Total: qsoList.Count,
                Completed: completedCount,
                Successful: successCount,
                Failed: failedCount,
                IsComplete: true,
                CurrentCallsign: null,
                Message: $"Sync complete: {successCount} uploaded, {failedCount} failed"
            ));

            _logger.LogInformation("QRZ parallel sync completed: {Success}/{Total} successful", successCount, qsoList.Count);

            return Ok(new QrzUploadResponse(qsoList.Count, successCount, failedCount, results));
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("QRZ sync was cancelled by user after {Completed}/{Total} QSOs", completedCount, totalToSync);

            await _hubContext.BroadcastQrzSyncProgress(new QrzSyncProgressEvent(
                Total: totalToSync,
                Completed: completedCount,
                Successful: successCount,
                Failed: failedCount,
                IsComplete: true,
                CurrentCallsign: null,
                Message: "Sync cancelled by user"
            ));

            return Ok(new QrzUploadResponse(totalToSync, successCount, failedCount, results));
        }
    }

    /// <summary>
    /// Cancel an ongoing QRZ sync operation
    /// </summary>
    [HttpPost("sync/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult CancelSync()
    {
        lock (_syncLock)
        {
            if (_syncCancellation != null)
            {
                _syncCancellation.Cancel();
                _logger.LogInformation("QRZ sync cancellation requested");
                return Ok(new { Message = "Sync cancellation requested" });
            }
        }

        return Ok(new { Message = "No sync in progress" });
    }

    /// <summary>
    /// Lookup a callsign on QRZ (requires XML subscription)
    /// </summary>
    [HttpGet("lookup/{callsign}")]
    [ProducesResponseType(typeof(QrzCallsignResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<QrzCallsignResponse>> LookupCallsign(string callsign)
    {
        try
        {
            var info = await _qrzService.LookupCallsignAsync(callsign);
            if (info == null)
            {
                return NotFound($"Callsign {callsign} not found");
            }

            return Ok(new QrzCallsignResponse(
                info.Callsign,
                info.Name,
                info.FirstName,
                info.Address,
                info.City,
                info.State,
                info.Country,
                info.Grid,
                info.Latitude,
                info.Longitude,
                info.Dxcc,
                info.CqZone,
                info.ItuZone,
                info.Email,
                info.QslManager,
                info.ImageUrl,
                info.LicenseExpiration
            ));
        }
        catch (QrzSubscriptionRequiredException)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                "QRZ XML subscription required for callsign lookups");
        }
    }
}
