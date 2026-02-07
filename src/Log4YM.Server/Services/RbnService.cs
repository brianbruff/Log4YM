using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Contracts.Models;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

public interface IRbnService
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    IReadOnlyList<RbnSpot> GetRecentSpots(int minutes = 5);
    Task<(string? Grid, double? Lat, double? Lon, string? Country)> LookupSkimmerLocationAsync(string callsign);
}

public class RbnService : IRbnService, IHostedService, IDisposable
{
    private readonly ILogger<RbnService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentBag<RbnSpot> _recentSpots = new();
    private readonly ConcurrentDictionary<string, (string? Grid, double? Lat, double? Lon, string? Country)> _skimmerLocations = new();

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private Timer? _cleanupTimer;

    private const string RBN_HOST = "telnet.reversebeacon.net";
    private const int RBN_PORT = 7000;
    private const int MAX_SPOTS_TO_KEEP = 500;
    private const int CLEANUP_INTERVAL_MINUTES = 2;

    // Regex pattern for parsing RBN telnet output
    // Example: DX de W3OA       14027.0  K1TTT          CW    31 dB  28 WPM  2133Z
    private static readonly Regex RbnSpotPattern = new Regex(
        @"DX de\s+(\S+)\s+([\d\.]+)\s+(\S+)\s+(?:\S+\s+)?(\w+)\s+([\-\d]+)\s+dB\s+(?:(\d+)\s+WPM\s+)?(\d{4}Z)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public RbnService(
        ILogger<RbnService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RBN service starting...");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Start cleanup timer
        _cleanupTimer = new Timer(CleanupOldSpots, null,
            TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES),
            TimeSpan.FromMinutes(CLEANUP_INTERVAL_MINUTES));

        // Start connection task
        _ = Task.Run(() => ConnectAndMonitorAsync(_cts.Token), _cts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RBN service stopping...");
        _cts?.Cancel();

        await DisconnectAsync();
        _cleanupTimer?.Dispose();
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _cleanupTimer?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }

    private async Task ConnectAndMonitorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(cancellationToken);

                // Read loop
                _readTask = Task.Run(() => ReadSpotsAsync(cancellationToken), cancellationToken);
                await _readTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RBN connection error, reconnecting in 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Connecting to RBN at {RBN_HOST}:{RBN_PORT}...");

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(RBN_HOST, RBN_PORT, cancellationToken);
        _stream = _tcpClient.GetStream();

        _logger.LogInformation("Connected to RBN");

        // Wait for login prompt and send a dummy callsign
        await Task.Delay(1000, cancellationToken);
        var loginCmd = Encoding.ASCII.GetBytes("MONITOR\n");
        await _stream.WriteAsync(loginCmd, cancellationToken);
    }

    private async Task DisconnectAsync()
    {
        try
        {
            _stream?.Close();
            _tcpClient?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from RBN");
        }
        finally
        {
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _stream = null;
            _tcpClient = null;
        }
    }

    private async Task ReadSpotsAsync(CancellationToken cancellationToken)
    {
        if (_stream == null) return;

        var buffer = new byte[4096];
        var lineBuilder = new StringBuilder();

        try
        {
            while (!cancellationToken.IsCancellationRequested && _stream.CanRead)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
                if (bytesRead == 0) break;

                var text = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                lineBuilder.Append(text);

                // Process complete lines
                var lines = lineBuilder.ToString().Split('\n');
                for (int i = 0; i < lines.Length - 1; i++)
                {
                    ProcessLine(lines[i].Trim());
                }

                // Keep the last incomplete line
                lineBuilder.Clear();
                lineBuilder.Append(lines[^1]);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from RBN stream");
            throw;
        }
    }

    private void ProcessLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("DX de", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            var match = RbnSpotPattern.Match(line);
            if (!match.Success)
            {
                // Try alternate parsing for different formats
                return;
            }

            var spot = new RbnSpot
            {
                Callsign = match.Groups[1].Value.Trim(),  // Skimmer callsign
                Frequency = double.Parse(match.Groups[2].Value),  // kHz
                Dx = match.Groups[3].Value.Trim(),  // Spotted callsign
                Mode = match.Groups[4].Value.ToUpperInvariant(),
                Snr = int.Parse(match.Groups[5].Value),
                Speed = match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : null,
                Timestamp = ParseTime(match.Groups[7].Value),
                Band = FrequencyToBand(double.Parse(match.Groups[2].Value))
            };

            // Add to recent spots
            _recentSpots.Add(spot);

            // Notify clients via SignalR
            _ = Task.Run(async () =>
            {
                try
                {
                    await _hubContext.Clients.All.OnRbnSpot(spot);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error broadcasting RBN spot");
                }
            });

            _logger.LogDebug($"RBN spot: {spot.Dx} on {spot.Frequency} kHz by {spot.Callsign} ({spot.Snr} dB)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to parse RBN line: {line}");
        }
    }

    private static DateTime ParseTime(string timeStr)
    {
        // timeStr is in format "2133Z" (HHMMZ)
        var now = DateTime.UtcNow;
        if (timeStr.Length >= 4 && int.TryParse(timeStr[..2], out int hours) && int.TryParse(timeStr.Substring(2, 2), out int minutes))
        {
            var spotTime = new DateTime(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Utc);

            // If spot time is in the future, it's from yesterday
            if (spotTime > now.AddMinutes(5))
            {
                spotTime = spotTime.AddDays(-1);
            }

            return spotTime;
        }

        return now;
    }

    private static string FrequencyToBand(double freqKhz)
    {
        var freqMhz = freqKhz / 1000.0;

        return freqMhz switch
        {
            >= 1.8 and < 2.0 => "160m",
            >= 3.5 and < 4.0 => "80m",
            >= 5.3 and < 5.4 => "60m",
            >= 7.0 and < 7.3 => "40m",
            >= 10.1 and < 10.15 => "30m",
            >= 14.0 and < 14.35 => "20m",
            >= 18.068 and < 18.168 => "17m",
            >= 21.0 and < 21.45 => "15m",
            >= 24.89 and < 24.99 => "12m",
            >= 28.0 and < 29.7 => "10m",
            >= 50.0 and < 54.0 => "6m",
            _ => "Other"
        };
    }

    private void CleanupOldSpots(object? state)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var oldCount = _recentSpots.Count;

            // Convert to list, filter, and recreate bag
            var validSpots = _recentSpots.Where(s => s.Timestamp >= cutoff).ToList();
            _recentSpots.Clear();
            foreach (var spot in validSpots.TakeLast(MAX_SPOTS_TO_KEEP))
            {
                _recentSpots.Add(spot);
            }

            var newCount = _recentSpots.Count;
            if (oldCount != newCount)
            {
                _logger.LogDebug($"Cleaned up RBN spots: {oldCount} -> {newCount}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up RBN spots");
        }
    }

    public IReadOnlyList<RbnSpot> GetRecentSpots(int minutes = 5)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-minutes);
        return _recentSpots
            .Where(s => s.Timestamp >= cutoff)
            .OrderByDescending(s => s.Timestamp)
            .ToList();
    }

    public async Task<(string? Grid, double? Lat, double? Lon, string? Country)> LookupSkimmerLocationAsync(string callsign)
    {
        // Check cache first
        if (_skimmerLocations.TryGetValue(callsign, out var cached))
        {
            return cached;
        }

        // In a real implementation, this would query QRZ or similar
        // For now, return null to indicate location not found
        var result = (Grid: (string?)null, Lat: (double?)null, Lon: (double?)null, Country: (string?)null);
        _skimmerLocations.TryAdd(callsign, result);

        return result;
    }
}
