using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Log4YM.Contracts.Events;
using Log4YM.Server.Hubs;

namespace Log4YM.Server.Services;

/// <summary>
/// Service for communicating with the 4O3A Tuner Genius XL (TGXL) over TCP port 9010.
///
/// Protocol (from official 4O3A API document):
///   Commands:  C{seq}|{command}\n
///   Responses: R{seq}|{hex_result}|{message}
///   Status:    S{seq}|status key1=val1 key2=val2 ... (space-delimited key=value pairs)
///
/// Key commands:
///   C{N}|status\n              — poll full status (use every 100ms)
///   C{N}|autotune\n            — trigger auto-tune cycle
///   C{N}|bypass set=1\n        — bypass (tuner out of circuit)
///   C{N}|bypass set=0\n        — in-circuit
///   C{N}|operate set=1\n       — operate mode
///   C{N}|operate set=0\n       — standby mode
///   C{N}|activate ch=1\n       — activate Radio 1
///   C{N}|activate ch=2\n       — activate Radio 2
///
/// Status keys:
///   fwd      Forward power in dBm (convert via 10^(x/10)/1000 to watts)
///   swr      Return loss in dB (negative, e.g. -60.0) → convert to SWR ratio
///   freqA    Radio A frequency in kHz (0 when radio does not supply frequency)
///   freqB    Radio B frequency in kHz
///   bandA    Radio A band as integer (e.g. 40 = 40m, 20 = 20m) — present even when freqA=0
///   bandB    Radio B band as integer
///   pttA     1=Radio A transmitting
///   pttB     1=Radio B transmitting
///   modeA    Radio A input mode (0=RF Sense, 1=Flex, 2=CAT, 3=P2B, 4=BCD)
///   modeB    Radio B input mode
///   state    1=OPERATE, 0=STANDBY
///   active   Active radio (1 or 2)
///   tuning   1=tuning in progress
///   bypass   0=in-circuit, 1=bypassed
///   relayL   L position (0-255)
///   relayC1  C1 position (0-255)
///   relayC2  C2 position (0-255)
/// </summary>
public class TunerGeniusService : BackgroundService
{
    private readonly ILogger<TunerGeniusService> _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private readonly ConcurrentDictionary<string, TunerGeniusConnection> _connections = new();

    // Discovery: TGXL broadcasts on UDP 9010 (same port as TCP control)
    private const int DiscoveryPort = 9010;

    public TunerGeniusService(
        ILogger<TunerGeniusService> logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _logger = logger;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tuner Genius XL service starting, listening for UDP discovery on port {Port}", DiscoveryPort);
        try
        {
            await RunDiscoveryListenerAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Tuner Genius XL service stopping");
        }
    }

    private async Task RunDiscoveryListenerAsync(CancellationToken ct)
    {
        using var udpClient = new UdpClient();
        udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await udpClient.ReceiveAsync(ct);
                var message = Encoding.ASCII.GetString(result.Buffer);

                _logger.LogDebug("TGXL UDP: {Message}", message);

                if (message.StartsWith("TunerGenius "))
                {
                    var device = ParseDiscoveryMessage(message);
                    if (device != null)
                        await HandleDeviceDiscoveredAsync(device, ct);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TGXL discovery listener error");
                await Task.Delay(1000, ct);
            }
        }
    }

    private TunerGeniusDiscoveredEvent? ParseDiscoveryMessage(string message)
    {
        // Expected: TunerGenius ip=10.0.0.249 v=1.2.17 serial=241257-1 nickname=Tuner_Genius_XL
        try
        {
            var values = new Dictionary<string, string>();
            foreach (var part in message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                    values[kv[0]] = kv[1];
            }

            var ip = values.GetValueOrDefault("ip", "");
            if (string.IsNullOrEmpty(ip)) return null;

            var port = values.TryGetValue("port", out var ps) && int.TryParse(ps, out var p) ? p : 9010;

            return new TunerGeniusDiscoveredEvent(
                IpAddress: ip,
                Port: port,
                Version: values.GetValueOrDefault("v", ""),
                Serial: values.GetValueOrDefault("serial", ""),
                Name: values.GetValueOrDefault("nickname", "Tuner Genius XL").Replace('_', ' '),
                Model: "TGXL",
                Uptime: 0
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse TGXL discovery message: {Message}", message);
            return null;
        }
    }

    private async Task HandleDeviceDiscoveredAsync(TunerGeniusDiscoveredEvent device, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(device.Serial)) return;

        if (!_connections.TryGetValue(device.Serial, out var connection))
        {
            _logger.LogInformation("Discovered TGXL: {Name} ({Serial}) at {Ip}:{Port} fw={Version}",
                device.Name, device.Serial, device.IpAddress, device.Port, device.Version);

            connection = new TunerGeniusConnection(device, _logger, _hubContext);
            if (_connections.TryAdd(device.Serial, connection))
            {
                await _hubContext.Clients.All.OnTunerGeniusDiscovered(device);
                _ = connection.ConnectAsync(ct);
            }
        }
        else
        {
            connection.UpdateDiscovery(device);
        }
    }

    // ── Public API (called from Hub) ──────────────────────────────────────

    public async Task TuneAsync(string serial, int portId)
    {
        if (_connections.TryGetValue(serial, out var c))
            await c.TuneAsync();
        else
            _logger.LogWarning("TuneAsync: device {Serial} not found", serial);
    }

    public async Task SetBypassAsync(string serial, int portId, bool bypass)
    {
        if (_connections.TryGetValue(serial, out var c))
            await c.SetBypassAsync(bypass);
        else
            _logger.LogWarning("SetBypassAsync: device {Serial} not found", serial);
    }

    public async Task SetOperateAsync(string serial, bool operate)
    {
        if (_connections.TryGetValue(serial, out var c))
            await c.SetOperateAsync(operate);
        else
            _logger.LogWarning("SetOperateAsync: device {Serial} not found", serial);
    }

    public async Task ActivateChannelAsync(string serial, int channel)
    {
        if (_connections.TryGetValue(serial, out var c))
            await c.ActivateChannelAsync(channel);
        else
            _logger.LogWarning("ActivateChannelAsync: device {Serial} not found", serial);
    }

    public TunerGeniusStatusEvent? GetDeviceStatus(string serial)
        => _connections.TryGetValue(serial, out var c) ? c.GetStatus() : null;

    public IEnumerable<TunerGeniusStatusEvent> GetAllDeviceStatuses()
        => _connections.Values.Select(c => c.GetStatus()).Where(s => s != null)!;
}

// ── Connection class ──────────────────────────────────────────────────────────

internal class TunerGeniusConnection
{
    private readonly ILogger _logger;
    private readonly IHubContext<LogHub, ILogHubClient> _hubContext;
    private TunerGeniusDiscoveredEvent _device;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private int _seq = 0;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    // Current parsed state
    private bool _isOperating = false;
    private bool _isBypassed = false;
    private bool _isTuning = false;
    private int _activeRadio = 1;
    private double _forwardPowerWatts = 0;
    private double _swr = 1.0;
    private int _L = 0, _C1 = 0, _C2 = 0;
    private double _freqA = 0, _freqB = 0;
    private int _bandA = 0, _bandB = 0;   // Raw band numbers from device (e.g. 40 = 40m)
    private bool _pttA = false, _pttB = false;
    private string _version = "";

    public bool IsConnected => _tcpClient?.Connected ?? false;

    public TunerGeniusConnection(
        TunerGeniusDiscoveredEvent device,
        ILogger logger,
        IHubContext<LogHub, ILogHubClient> hubContext)
    {
        _device = device;
        _logger = logger;
        _hubContext = hubContext;
    }

    public void UpdateDiscovery(TunerGeniusDiscoveredEvent device) => _device = device;

    public async Task ConnectAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("TGXL connecting to {Ip}:{Port}...", _device.IpAddress, _device.Port);
                _tcpClient = new TcpClient { NoDelay = true };
                _tcpClient.ReceiveTimeout = 10000;
                await _tcpClient.ConnectAsync(_device.IpAddress, _device.Port, ct);
                _stream = _tcpClient.GetStream();

                // Read version banner: "V1.2.17 TG" (or "V1.2.17 TG AUTH" on WAN)
                using var bannerReader = new StreamReader(_stream, Encoding.ASCII, leaveOpen: true);
                var banner = await bannerReader.ReadLineAsync(ct);
                if (banner != null && banner.StartsWith("V"))
                {
                    _version = banner.Split(' ')[0][1..]; // strip leading 'V'
                    _logger.LogInformation("TGXL connected: {Name} fw={Version}", _device.Name, _version);
                }

                await BroadcastStatusAsync();

                // Start poll loop — TGXL requires active polling, it does not push unsolicited status
                await PollLoopAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TGXL connection error ({Name})", _device.Name);
                await _hubContext.Clients.All.OnTunerGeniusDisconnected(
                    new TunerGeniusDisconnectedEvent(_device.Serial));
            }

            Disconnect();

            if (!ct.IsCancellationRequested)
            {
                _logger.LogInformation("TGXL reconnecting to {Name} in 5s...", _device.Name);
                await Task.Delay(5000, ct);
            }
        }
    }

    /// <summary>
    /// Poll status every 100ms — this is the correct way to get live data from the TGXL.
    /// The device does not push unsolicited status updates.
    /// </summary>
    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && IsConnected)
        {
            try
            {
                var response = await SendRawAsync("status", ct);
                if (response != null)
                    await ParseAndBroadcastStatusAsync(response);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TGXL poll error");
                break;
            }

            await Task.Delay(100, ct);
        }
    }

    /// <summary>
    /// Send a command and read back a single line response.
    /// Format sent:    C{seq}|{command}\n
    /// Response line:  R{seq}|{hex}|... or S{seq}|status=...
    /// </summary>
    private async Task<string?> SendRawAsync(string command, CancellationToken ct = default)
    {
        if (_stream == null) return null;

        await _sendLock.WaitAsync(ct);
        try
        {
            var n = (Interlocked.Increment(ref _seq) % 255) + 1;
            var line = $"C{n}|{command}\n";
            var bytes = Encoding.ASCII.GetBytes(line);

            await _stream.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);

            // Read response — may be R{n}|... or S{n}|status=...
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(2000);

            var buffer = new byte[4096];
            var sb = new StringBuilder();

            while (!cts.IsCancellationRequested)
            {
                var bytesRead = await _stream.ReadAsync(buffer, cts.Token);
                if (bytesRead == 0) break;
                sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

                // Check if we have a complete line
                var text = sb.ToString();
                var newline = text.IndexOfAny(new[] { '\n', '\r' });
                if (newline >= 0)
                    return text[..newline].Trim();
            }

            return sb.Length > 0 ? sb.ToString().Trim() : null;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("TGXL command '{Command}' timed out", command);
            return null;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    // ── Status Parsing ────────────────────────────────────────────────────

    /// <summary>
    /// Parse the TGXL status response and broadcast to SignalR clients.
    ///
    /// Response format: S{seq}|status key1=val1 key2=val2 ... keyN=valN
    ///
    /// Key map:
    ///   fwd      Forward power in dBm (e.g. "50.0" = 100W, "63.0" ≈ 2kW) — convert via 10^(x/10)/1000
    ///   swr      Return loss in dB, negative (e.g. "-60.0000") → convert to SWR ratio
    ///   freqA    Radio A frequency in kHz → divide by 1000 for MHz
    ///   freqB    Radio B frequency in kHz
    ///   state    Operate state: 1=OPERATE, 0=STANDBY
    ///   active   Active radio: 1 or 2
    ///   tuning   Tuning in progress: 1=yes
    ///   bypass   Global bypass: 0=in-circuit, 1=bypassed
    ///   relayL   L position  (0-255)
    ///   relayC1  C1 position (0-255)
    ///   relayC2  C2 position (0-255)
    /// </summary>
    private async Task ParseAndBroadcastStatusAsync(string response)
    {
        // Normalise: strip sequence prefix "S{n}|"
        var statusPart = response;
        var pipeIdx = response.IndexOf('|');
        if (pipeIdx >= 0)
            statusPart = response[(pipeIdx + 1)..];

        if (!statusPart.StartsWith("status", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("TGXL ignoring non-status response: {Line}", response);
            return;
        }

        // Parse space-delimited key=value pairs (skip the leading "status " keyword)
        var kvStr = statusPart.Length > 7 ? statusPart[7..] : "";
        var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in kvStr.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = token.IndexOf('=');
            if (eq > 0)
                kv[token[..eq]] = eq < token.Length - 1 ? token[(eq + 1)..] : "";
        }

        try
        {
            // Forward power: value is in dBm, convert to watts (fwd=50.0 → 100W, fwd=63.0 → ~2kW)
            if (kv.TryGetValue("fwd", out var fwdStr) &&
                double.TryParse(fwdStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var fwdDbm))
            {
                var watts = Math.Pow(10, fwdDbm / 10.0) / 1000.0;
                _forwardPowerWatts = watts >= 1.0 ? Math.Round(watts, 1) : 0;
            }

            // Return loss -> SWR (device sends negative dB, e.g. -60.0 = perfect match)
            if (kv.TryGetValue("swr", out var swrStr) &&
                double.TryParse(swrStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rawRl))
            {
                var absRl = Math.Abs(rawRl);
                var gamma = Math.Pow(10, -absRl / 20.0);
                var swrRaw = (1 + gamma) / (1 - gamma);
                _swr = double.IsInfinity(swrRaw) || double.IsNaN(swrRaw) ? 99.9 : Math.Round(swrRaw, 2);
            }

            // Radio A frequency (kHz -> MHz)
            if (kv.TryGetValue("freqA", out var freqAStr) &&
                double.TryParse(freqAStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var freqAKhz))
                _freqA = Math.Round(freqAKhz / 1000.0, 3);

            // Radio B frequency (kHz -> MHz)
            if (kv.TryGetValue("freqB", out var freqBStr) &&
                double.TryParse(freqBStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var freqBKhz))
                _freqB = Math.Round(freqBKhz / 1000.0, 3);

            // Raw band numbers (present even when frequency is 0 — e.g. radio supplies band but not frequency)
            if (kv.TryGetValue("bandA", out var bandAStr) && int.TryParse(bandAStr, out var bandAVal))
                _bandA = bandAVal;
            if (kv.TryGetValue("bandB", out var bandBStr) && int.TryParse(bandBStr, out var bandBVal))
                _bandB = bandBVal;

            // PTT state
            if (kv.TryGetValue("pttA", out var pttAStr))
                _pttA = pttAStr == "1";
            if (kv.TryGetValue("pttB", out var pttBStr))
                _pttB = pttBStr == "1";

            // Operate state
            if (kv.TryGetValue("state", out var stateStr))
                _isOperating = stateStr == "1";

            // Active radio
            if (kv.TryGetValue("active", out var activeStr) && int.TryParse(activeStr, out var ar))
                _activeRadio = ar;

            // Tuning
            if (kv.TryGetValue("tuning", out var tuningStr))
                _isTuning = tuningStr == "1";

            // Bypass
            if (kv.TryGetValue("bypass", out var bypassStr))
                _isBypassed = bypassStr == "1";

            // Matching network positions
            if (kv.TryGetValue("relayL", out var lStr) && int.TryParse(lStr, out var lVal))
                _L = lVal;
            if (kv.TryGetValue("relayC1", out var c1Str) && int.TryParse(c1Str, out var c1Val))
                _C1 = c1Val;
            if (kv.TryGetValue("relayC2", out var c2Str) && int.TryParse(c2Str, out var c2Val))
                _C2 = c2Val;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TGXL status parse error on: {Line}", response);
            return;
        }

        // Broadcast full status — covers both ports (A and B) so each radio's band/PTT are reflected,
        // even when the radio only supplies band data without frequency.
        await BroadcastStatusAsync();
    }

    // ── Commands ──────────────────────────────────────────────────────────

    public async Task TuneAsync()
    {
        var r = await SendRawAsync("autotune");
        _logger.LogInformation("TGXL autotune triggered, response: {R}", r);
    }

    public async Task SetBypassAsync(bool bypass)
    {
        var cmd = bypass ? "bypass set=1" : "bypass set=0";
        var r = await SendRawAsync(cmd);
        _logger.LogInformation("TGXL bypass set={Val}, response: {R}", bypass ? 1 : 0, r);
    }

    public async Task SetOperateAsync(bool operate)
    {
        var cmd = operate ? "operate set=1" : "operate set=0";
        var r = await SendRawAsync(cmd);
        _logger.LogInformation("TGXL operate set={Val}, response: {R}", operate ? 1 : 0, r);
    }

    public async Task ActivateChannelAsync(int channel)
    {
        if (channel != 1 && channel != 2)
        {
            _logger.LogWarning("TGXL ActivateChannel: invalid channel {Ch}", channel);
            return;
        }
        var r = await SendRawAsync($"activate ch={channel}");
        _logger.LogInformation("TGXL activate ch={Ch}, response: {R}", channel, r);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    public TunerGeniusStatusEvent GetStatus()
    {
        var swrX10 = (int)Math.Round(_swr * 10);
        var tuneResult = _swr <= 2.0 ? "OK" : "HighSWR";
        var portA = new TunerGeniusPortStatus(1, !_isBypassed, GetBandForPort(_freqA, _bandA), _freqA,
            swrX10, _isTuning, _pttA, null, tuneResult);
        // Show portB when the device has supplied any band or frequency data for radio B
        TunerGeniusPortStatus? portB = (_freqB > 0 || _bandB > 0)
            ? new TunerGeniusPortStatus(2, !_isBypassed, GetBandForPort(_freqB, _bandB), _freqB,
                swrX10, _isTuning, _pttB, null, tuneResult)
            : null;

        return new TunerGeniusStatusEvent(
            DeviceSerial: _device.Serial,
            DeviceName: _device.Name,
            IpAddress: _device.IpAddress,
            Version: _version,
            Model: _device.Model,
            IsConnected: IsConnected,
            IsOperating: _isOperating,
            IsBypassed: _isBypassed,
            IsTuning: _isTuning,
            ActiveRadio: _activeRadio,
            ForwardPowerWatts: _forwardPowerWatts,
            Swr: _swr,
            L: _L,
            C1: _C1,
            C2: _C2,
            FreqAMhz: _freqA,
            FreqBMhz: _freqB,
            PortA: portA,
            PortB: portB
        );
    }

    private async Task BroadcastStatusAsync()
        => await _hubContext.Clients.All.OnTunerGeniusStatus(GetStatus());

    private void Disconnect()
    {
        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }
        _stream = null;
        _tcpClient = null;
    }

    /// <summary>
    /// Returns band label using frequency when available, falling back to the raw band integer
    /// supplied by the TGXL (e.g. bandA=40 means 40m).  Radios that provide band data but not
    /// frequency (e.g. via BCD or CAT without frequency reporting) will still show the correct band.
    /// </summary>
    private static string GetBandForPort(double freqMhz, int bandNum)
        => freqMhz > 0 ? FreqToBand(freqMhz) : BandNumToString(bandNum);

    private static string FreqToBand(double mhz) => mhz switch
    {
        >= 1.8 and < 2.0   => "160m",
        >= 3.5 and < 4.0   => "80m",
        >= 5.3 and < 5.4   => "60m",
        >= 7.0 and < 7.3   => "40m",
        >= 10.1 and < 10.15 => "30m",
        >= 14.0 and < 14.35 => "20m",
        >= 18.068 and < 18.168 => "17m",
        >= 21.0 and < 21.45 => "15m",
        >= 24.89 and < 24.99 => "12m",
        >= 28.0 and < 29.7  => "10m",
        >= 50.0 and < 54.0  => "6m",
        _ => mhz > 0 ? $"{mhz:F3}" : "None"
    };

    /// <summary>
    /// Converts the raw band integer from the TGXL status (e.g. bandA=40) to a display label.
    /// The device reports the band in meters as a plain integer.
    /// </summary>
    private static string BandNumToString(int band) => band switch
    {
        160 => "160m",
        80  => "80m",
        60  => "60m",
        40  => "40m",
        30  => "30m",
        20  => "20m",
        17  => "17m",
        15  => "15m",
        12  => "12m",
        10  => "10m",
        6   => "6m",
        2   => "2m",
        0   => "None",
        _   => $"{band}m"
    };
}
