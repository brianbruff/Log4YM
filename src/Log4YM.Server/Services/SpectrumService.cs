using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;
using Log4YM.Server.Core.Database;

namespace Log4YM.Server.Services;

/// <summary>
/// Listens for N1MM+ Spectrum Display UDP packets (pre-computed FFT data)
/// and broadcasts them to connected clients via SignalR.
/// </summary>
public class SpectrumService : BackgroundService
{
    private readonly ILogger<SpectrumService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly IServiceProvider _serviceProvider;

    private const int DefaultPort = 13064;
    private const int MaxPointsOut = 512;
    private const long MinBroadcastIntervalMs = 50; // 20fps max

    private UdpClient? _udpClient;
    private int _currentPort;
    private bool _currentEnabled;
    private string? _currentSourceIp;
    private IPAddress? _sourceFilter;
    private long _lastBroadcastTicks;

    public SpectrumService(
        ILogger<SpectrumService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Spectrum service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (enabled, port, sourceIp) = await ReadSettingsAsync();

                if (!enabled)
                {
                    StopListener();
                    _currentEnabled = false;
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                // Start or restart listener if settings changed
                if (!_currentEnabled || port != _currentPort)
                {
                    StopListener();
                    StartListener(port);
                    _currentEnabled = true;
                    _currentPort = port;
                }

                // Update source IP filter (can change without restarting listener)
                if (sourceIp != _currentSourceIp)
                {
                    _currentSourceIp = sourceIp;
                    _sourceFilter = !string.IsNullOrWhiteSpace(sourceIp) && IPAddress.TryParse(sourceIp, out var parsed)
                        ? parsed
                        : null;
                    if (_sourceFilter != null)
                        _logger.LogInformation("Spectrum source filter set to {SourceIp}", _sourceFilter);
                    else if (!string.IsNullOrWhiteSpace(sourceIp))
                        _logger.LogWarning("Invalid spectrum source IP: {SourceIp}, accepting from any host", sourceIp);
                }

                // Receive and process packets until next settings check
                await ReceiveLoopAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Spectrum service error, restarting in 5s");
                StopListener();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        StopListener();
        _logger.LogInformation("Spectrum service stopped");
    }

    private async Task ReceiveLoopAsync(CancellationToken stoppingToken)
    {
        if (_udpClient == null) return;

        // Process packets for 5 seconds, then re-check settings
        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        loopCts.CancelAfter(TimeSpan.FromSeconds(5));

        while (!loopCts.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync(loopCts.Token);

                // Filter by source IP if configured
                if (_sourceFilter != null && !result.RemoteEndPoint.Address.Equals(_sourceFilter))
                    continue;

                await ProcessPacketAsync(result.Buffer);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Socket error receiving spectrum data");
                break;
            }
        }
    }

    private async Task ProcessPacketAsync(byte[] data)
    {
        // Throttle to 20fps
        var nowTicks = Environment.TickCount64;
        if (nowTicks - _lastBroadcastTicks < MinBroadcastIntervalMs)
            return;

        try
        {
            var xml = Encoding.UTF8.GetString(data);
            var parsed = ParseN1mmSpectrum(xml);
            if (parsed == null) return;

            _lastBroadcastTicks = nowTicks;
            await _hubContext.BroadcastSpectrumData(parsed);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse spectrum packet");
        }
    }

    /// <summary>
    /// Fast parse of N1MM+ Spectrum Display XML using IndexOf/Substring
    /// instead of XDocument for 30fps performance.
    /// Thetis sends frequencies in kHz (e.g. 14219.963) — we convert to Hz for the frontend.
    /// </summary>
    private static SpectrumDataEvent? ParseN1mmSpectrum(string xml)
    {
        // Extract LowScopeFrequency (kHz, possibly decimal)
        var lowKhz = ExtractDoubleValue(xml, "LowScopeFrequency");
        if (lowKhz == null) return null;

        // Extract HighScopeFrequency (kHz, possibly decimal)
        var highKhz = ExtractDoubleValue(xml, "HighScopeFrequency");
        if (highKhz == null) return null;

        // Extract SpectrumData (CSV of integers)
        var specData = ExtractTagContent(xml, "SpectrumData");
        if (specData == null) return null;

        // Parse CSV amplitudes
        var rawPoints = ParseCsvInts(specData);
        if (rawPoints.Length == 0) return null;

        // Downsample to MaxPointsOut using max-hold per bin
        var downsampled = Downsample(rawPoints, MaxPointsOut);

        // Convert kHz to Hz
        var lowHz = (long)(lowKhz.Value * 1000);
        var highHz = (long)(highKhz.Value * 1000);

        return new SpectrumDataEvent(lowHz, highHz, downsampled);
    }

    private static double? ExtractDoubleValue(string xml, string tagName)
    {
        var content = ExtractTagContent(xml, tagName);
        if (content != null && double.TryParse(content, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value))
            return value;
        return null;
    }

    private static string? ExtractTagContent(string xml, string tagName)
    {
        var openTag = $"<{tagName}>";
        var closeTag = $"</{tagName}>";
        var start = xml.IndexOf(openTag, StringComparison.Ordinal);
        if (start < 0) return null;
        start += openTag.Length;
        var end = xml.IndexOf(closeTag, start, StringComparison.Ordinal);
        if (end < 0) return null;
        return xml.Substring(start, end - start);
    }

    private static int[] ParseCsvInts(string csv)
    {
        var parts = csv.Split(',');
        var result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (int.TryParse(parts[i].AsSpan().Trim(), out var val))
                result[i] = val;
        }
        return result;
    }

    /// <summary>
    /// Downsample from source length to targetCount using max-hold per bin.
    /// If source is already &lt;= targetCount, returns source as-is.
    /// </summary>
    private static int[] Downsample(int[] source, int targetCount)
    {
        if (source.Length <= targetCount)
            return source;

        var result = new int[targetCount];
        var binSize = (double)source.Length / targetCount;

        for (int i = 0; i < targetCount; i++)
        {
            var binStart = (int)(i * binSize);
            var binEnd = (int)((i + 1) * binSize);
            if (binEnd > source.Length) binEnd = source.Length;

            var max = int.MinValue;
            for (int j = binStart; j < binEnd; j++)
            {
                if (source[j] > max) max = source[j];
            }
            result[i] = max;
        }

        return result;
    }

    private void StartListener(int port)
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
            _logger.LogInformation("Spectrum listener started on UDP port {Port}", port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start spectrum listener on port {Port}", port);
            _udpClient?.Dispose();
            _udpClient = null;
        }
    }

    private void StopListener()
    {
        if (_udpClient != null)
        {
            _logger.LogInformation("Spectrum listener stopped");
            _udpClient.Close();
            _udpClient.Dispose();
            _udpClient = null;
        }
    }

    private async Task<(bool enabled, int port, string? sourceIp)> ReadSettingsAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var settings = await settingsRepo.GetAsync();
            var spectrum = settings?.Spectrum;
            return (spectrum?.Enabled ?? false, spectrum?.ListenPort ?? DefaultPort, spectrum?.SourceIp);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read spectrum settings");
            return (false, DefaultPort, null);
        }
    }
}
