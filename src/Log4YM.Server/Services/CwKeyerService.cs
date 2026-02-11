using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

/// <summary>
/// Service for CW keyer functionality with macro management
/// </summary>
public class CwKeyerService
{
    private readonly ILogger<CwKeyerService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly TciRadioService _tciRadioService;
    private readonly HamlibService _hamlibService;

    // Track CW state per radio
    private readonly ConcurrentDictionary<string, CwKeyerState> _keyerStates = new();

    // Default WPM speed
    private const int DefaultWpm = 25;

    public CwKeyerService(
        ILogger<CwKeyerService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        TciRadioService tciRadioService,
        HamlibService hamlibService)
    {
        _logger = logger;
        _hubContext = hubContext;
        _tciRadioService = tciRadioService;
        _hamlibService = hamlibService;
    }

    /// <summary>
    /// Send CW message to radio
    /// </summary>
    public async Task SendCwAsync(string radioId, string message, int? speedWpm = null)
    {
        try
        {
            var speed = speedWpm ?? DefaultWpm;

            // Get or create state for this radio
            var state = _keyerStates.GetOrAdd(radioId, _ => new CwKeyerState
            {
                RadioId = radioId,
                SpeedWpm = speed
            });

            // Update state
            state.IsKeying = true;
            state.CurrentMessage = message;
            state.SpeedWpm = speed;

            // Broadcast status
            await BroadcastStatusAsync(radioId);

            // Send to appropriate radio service
            await SendToRadioAsync(radioId, message, speed);

            // Note: IsKeying will be set to false when radio confirms TX complete
            // For now, we'll reset it after a delay based on message length
            _ = Task.Run(async () =>
            {
                // Rough estimate: 5 WPM = 1 word per 12 seconds (PARIS standard)
                // Calculate approximate duration
                var wordCount = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                var durationSeconds = (wordCount * 60.0 / speed) + 1; // Add 1 second buffer
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

                if (state.CurrentMessage == message) // Only clear if same message
                {
                    state.IsKeying = false;
                    state.CurrentMessage = null;
                    await BroadcastStatusAsync(radioId);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send CW message for radio {RadioId}", radioId);
            throw;
        }
    }

    /// <summary>
    /// Stop CW transmission immediately
    /// </summary>
    public async Task StopCwAsync(string radioId)
    {
        try
        {
            if (_keyerStates.TryGetValue(radioId, out var state))
            {
                state.IsKeying = false;
                state.CurrentMessage = null;
                await BroadcastStatusAsync(radioId);
            }

            // TODO: Send stop command to radio if supported
            _logger.LogInformation("CW keying stopped for radio {RadioId}", radioId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop CW keying for radio {RadioId}", radioId);
            throw;
        }
    }

    /// <summary>
    /// Set CW keyer speed
    /// </summary>
    public async Task SetSpeedAsync(string radioId, int speedWpm)
    {
        try
        {
            if (speedWpm < 5 || speedWpm > 60)
            {
                throw new ArgumentException("Speed must be between 5 and 60 WPM", nameof(speedWpm));
            }

            var state = _keyerStates.GetOrAdd(radioId, _ => new CwKeyerState
            {
                RadioId = radioId,
                SpeedWpm = speedWpm
            });

            state.SpeedWpm = speedWpm;
            await BroadcastStatusAsync(radioId);

            // Send speed command to radio
            await SetRadioSpeedAsync(radioId, speedWpm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set CW speed for radio {RadioId}", radioId);
            throw;
        }
    }

    /// <summary>
    /// Get current keyer status for a radio
    /// </summary>
    public CwKeyerStatusEvent? GetStatus(string radioId)
    {
        if (_keyerStates.TryGetValue(radioId, out var state))
        {
            return new CwKeyerStatusEvent(
                state.RadioId,
                state.IsKeying,
                state.SpeedWpm,
                state.CurrentMessage
            );
        }
        return null;
    }

    private async Task SendToRadioAsync(string radioId, string message, int speedWpm)
    {
        // Try TCI first
        if (await _tciRadioService.SendCwAsync(radioId, message, speedWpm))
        {
            _logger.LogInformation("CW sent via TCI for radio {RadioId}: {Message}", radioId, message);
            return;
        }

        // Try Hamlib
        if (await _hamlibService.SendCwAsync(radioId, message, speedWpm))
        {
            _logger.LogInformation("CW sent via Hamlib for radio {RadioId}: {Message}", radioId, message);
            return;
        }

        _logger.LogWarning("No suitable radio connection found for CW keying: {RadioId}", radioId);
        throw new InvalidOperationException($"Radio {radioId} does not support CW keying or is not connected");
    }

    private async Task SetRadioSpeedAsync(string radioId, int speedWpm)
    {
        // Try TCI first
        if (await _tciRadioService.SetCwSpeedAsync(radioId, speedWpm))
        {
            return;
        }

        // Try Hamlib
        if (await _hamlibService.SetCwSpeedAsync(radioId, speedWpm))
        {
            return;
        }

        _logger.LogWarning("Could not set CW speed for radio {RadioId}", radioId);
    }

    private async Task BroadcastStatusAsync(string radioId)
    {
        var status = GetStatus(radioId);
        if (status != null)
        {
            await _hubContext.Clients.All.OnCwKeyerStatus(status);
        }
    }

    private class CwKeyerState
    {
        public required string RadioId { get; set; }
        public bool IsKeying { get; set; }
        public int SpeedWpm { get; set; } = DefaultWpm;
        public string? CurrentMessage { get; set; }
    }
}
