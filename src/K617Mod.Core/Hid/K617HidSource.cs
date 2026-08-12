using HidSharp;

namespace K617Mod.Core.Hid;

/// <summary>
/// Reads live reports directly from the physical K617 HE over HidSharp.
/// This is the only class in the Hid namespace that knows a real device
/// exists - everything downstream talks to IHidKeySource instead, never
/// to this class directly.
/// </summary>
public sealed class K617HidSource : IHidKeySource
{
    /// <summary>How long to wait per read while probing a candidate
    /// interface for real data. Short enough that trying several
    /// candidates doesn't take forever, long enough to catch a report
    /// if the user is holding a key down as instructed.</summary>
    private const int ProbeReadTimeoutMs = 400;

    /// <summary>How many probe reads to attempt per candidate before
    /// giving up on it and moving to the next.</summary>
    private const int ProbeAttempts = 4;

    private HidStream? _stream;
    private Thread? _readThread;
    private volatile bool _running;

    public event EventHandler<RawKeyReport>? ReportReceived;
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Opens the correct HID interface and begins reading.
    /// IMPORTANT: hold down any key on the K617 HE while calling this -
    /// interface selection works by probing each candidate for real
    /// report data, and it needs live data flowing to tell the right
    /// interface apart from an idle-but-openable wrong one.
    /// </summary>
    public void Start()
    {
        if (_running) return;

        var candidates = DeviceList.Local
            .GetHidDevices(HidProtocolConfig.VendorId, HidProtocolConfig.ProductId)
            .ToList();

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"No K617 HE device found. Is it plugged in? " +
                $"(looking for VID=0x{HidProtocolConfig.VendorId:X4} PID=0x{HidProtocolConfig.ProductId:X4})");
        }

        HidStream? openedStream = null;
        var attemptLog = new List<string>();

        foreach (var candidate in candidates)
        {
            HidStream? stream = null;
            try
            {
                stream = candidate.Open();
                stream.ReadTimeout = ProbeReadTimeoutMs;

                var probeBuffer = new byte[HidProtocolConfig.ReportLength];
                bool matched = false;

                for (int attempt = 0; attempt < ProbeAttempts && !matched; attempt++)
                {
                    int bytesRead;
                    try
                    {
                        bytesRead = stream.Read(probeBuffer, 0, probeBuffer.Length);
                    }
                    catch (TimeoutException)
                    {
                        continue; // no data this attempt, try again within the same candidate
                    }

                    // Full parse (header + mode byte + depth sanity), not
                    // just a header-byte match - a header-only check let
                    // a wrong interface false-positive during testing.
                    if (bytesRead > 0 && TryParse(probeBuffer.AsSpan(0, bytesRead)) is not null)
                    {
                        matched = true;
                    }
                }

                if (matched)
                {
                    stream.ReadTimeout = Timeout.Infinite; // back to normal blocking reads for real operation
                    openedStream = stream;
                    attemptLog.Add($"{candidate.DevicePath}: MATCHED");
                    break;
                }

                attemptLog.Add($"{candidate.DevicePath}: opened, no matching data seen");
                stream.Dispose();
            }
            catch (Exception ex)
            {
                stream?.Dispose();
                attemptLog.Add($"{candidate.DevicePath}: {ex.Message}");
            }
        }

        if (openedStream is null)
        {
            throw new InvalidOperationException(
                "Found K617 HE device(s) but none produced recognizable data. " +
                "Make sure you're holding a key down while Start() runs, that no other " +
                "program (iLumiPC, Redragon's own software) currently has the device open, " +
                "and that the device-wake step has been done this boot.\n" +
                "Attempts:\n" + string.Join("\n", attemptLog));
        }

        _stream = openedStream;
        IsConnected = true;
        _running = true;

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "K617HidReadThread" };
        _readThread.Start();
    }

    public void Stop()
    {
        _running = false;
        _readThread?.Join(500);
        _stream?.Dispose();
        _stream = null;
        IsConnected = false;
    }

    private void ReadLoop()
    {
        var buffer = new byte[HidProtocolConfig.ReportLength];

        while (_running && _stream is not null)
        {
            int bytesRead;
            try
            {
                bytesRead = _stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception)
            {
                IsConnected = false;
                break;
            }

            if (bytesRead <= 0) continue;

            var report = TryParse(buffer.AsSpan(0, bytesRead));
            if (report is { } parsed)
            {
                ReportReceived?.Invoke(this, parsed);
            }
        }
    }

    private static RawKeyReport? TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length > 0 && data[0] == 1)
        {
            data = data[1..];
        }

        if (data.Length <= HidProtocolConfig.DepthHighIndex) return null;
        if (data[0] != HidProtocolConfig.HeaderByte) return null;

        var modeByte = data[HidProtocolConfig.ModeByteIndex];
        if (modeByte != (byte)ReportMode.Live && modeByte != (byte)ReportMode.Summary)
        {
            return null;
        }

        int row = data[HidProtocolConfig.KeyIdRowIndex];
        int col = data[HidProtocolConfig.KeyIdColIndex];
        int depth = data[HidProtocolConfig.DepthLowIndex] | (data[HidProtocolConfig.DepthHighIndex] << 8);

        if (depth > HidProtocolConfig.RawDepthSanityMax) return null;

        return new RawKeyReport(row, col, depth, (ReportMode)modeByte, DateTime.UtcNow);
    }

    public void Dispose() => Stop();
}
