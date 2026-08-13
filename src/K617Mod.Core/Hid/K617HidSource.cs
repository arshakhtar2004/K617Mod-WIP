using HidSharp;
using HidSharp.Reports;

namespace K617Mod.Core.Hid;

/// <summary>
/// Reads live reports directly from the physical K617 HE over HidSharp.
/// This is the only class in the Hid namespace that knows a real device
/// exists - everything downstream talks to IHidKeySource instead, never
/// to this class directly.
///
/// Interface selection is IDENTITY-based first (match the vendor-specific
/// analog usage page in the device's own report descriptor) and only
/// falls back to the old DATA-based probe if that finds nothing. The
/// difference matters: identity selection returns instantly and needs
/// nobody holding a key down, which is what makes a click-to-launch tray
/// app possible. The Python build selected the same way - see
/// hid_reader.find_device_path().
/// </summary>
public sealed class K617HidSource : IHidKeySource
{
    /// <summary>How long to wait per read while probing a candidate
    /// interface for real data. Only reached in the fallback path now.
    /// Short enough that trying several candidates doesn't take forever,
    /// long enough to catch a report if a key happens to be held.</summary>
    private const int ProbeReadTimeoutMs = 400;

    /// <summary>How many probe reads to attempt per candidate before
    /// giving up on it and moving to the next. Fallback path only.</summary>
    private const int ProbeAttempts = 4;

    private HidStream? _stream;
    private Thread? _readThread;
    private volatile bool _running;

    public event EventHandler<RawKeyReport>? ReportReceived;
    public bool IsConnected { get; private set; }

    /// <summary>
    /// How the interface was chosen on the last successful Start(), for
    /// diagnostics. "usage page" means the fast identity path; "data
    /// probe" means the fallback ran, which is worth surfacing because it
    /// implies the descriptor read failed on this machine.
    /// </summary>
    public string? SelectionMethod { get; private set; }

    /// <summary>
    /// Opens the correct HID interface and begins reading. Returns as soon
    /// as the interface is open - no key needs to be held, and nothing
    /// blocks waiting for data.
    ///
    /// NOTE: opening the interface used to not be the same as the device
    /// streaming - the analog interface stayed silent until manually
    /// woken via iLumiPC. Attach() now sends the confirmed wake command
    /// itself (see HidProtocolConfig.WakeReports), so a normal Start()
    /// here is expected to produce live reports without that manual step.
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

        var attemptLog = new List<string>();

        // --- Path 1: identity. Instant, no data needed. ---
        var identityMatches = candidates.Where(c => MatchesAnalogInterface(c, attemptLog)).ToList();

        foreach (var candidate in identityMatches)
        {
            if (TryOpen(candidate, out var stream, attemptLog))
            {
                Attach(stream!, "usage page");
                return;
            }
        }

        // --- Path 2: fallback. The old probe-by-data behaviour, kept
        // because a descriptor read can fail on some driver stacks, and an
        // app that still works slowly beats one that doesn't work. ---
        foreach (var candidate in candidates.Except(identityMatches))
        {
            if (TryProbeForData(candidate, out var stream, attemptLog))
            {
                Attach(stream!, "data probe");
                return;
            }
        }

        throw new InvalidOperationException(
            "Found K617 HE device(s) but none could be opened as the analog interface. " +
            "Check that no other program (iLumiPC, Redragon's own software) currently has " +
            "the device open - only one process can hold it at a time.\n" +
            "Attempts:\n" + string.Join("\n", attemptLog));
    }

    private void Attach(HidStream stream, string method)
    {
        stream.ReadTimeout = Timeout.Infinite; // normal blocking reads for real operation
        _stream = stream;
        SelectionMethod = method;
        IsConnected = true;
        _running = true;

        TrySendWakeReports();

        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "K617HidReadThread" };
        _readThread.Start();
    }

    /// <summary>
    /// True if this interface declares the vendor-specific analog usage
    /// page in its report descriptor. This is what distinguishes the
    /// analog interface from the ordinary keyboard interface the device
    /// also exposes, without needing to read a single byte of data.
    /// </summary>
    private static bool MatchesAnalogInterface(HidDevice device, List<string> log)
    {
        try
        {
            var descriptor = device.GetReportDescriptor();

            foreach (var item in descriptor.DeviceItems)
            {
                foreach (var usage in item.Usages.GetAllValues())
                {
                    // A HID usage is a 32-bit value: the top 16 bits are
                    // the usage page, the bottom 16 the usage id. Only the
                    // page identifies the analog interface.
                    if ((usage >> 16) == HidProtocolConfig.UsagePage)
                    {
                        log.Add($"{device.DevicePath}: usage page 0x{HidProtocolConfig.UsagePage:X4} MATCHED");
                        return true;
                    }
                }
            }

            log.Add($"{device.DevicePath}: descriptor read, analog usage page not present");
        }
        catch (Exception ex)
        {
            // Some driver stacks refuse a descriptor read on some
            // interfaces. Not fatal - it just means this candidate can't be
            // identified this way, so it drops through to the probe.
            log.Add($"{device.DevicePath}: descriptor unreadable ({ex.Message})");
        }

        return false;
    }

    private static bool TryOpen(HidDevice candidate, out HidStream? stream, List<string> log)
    {
        try
        {
            stream = candidate.Open();
            return true;
        }
        catch (Exception ex)
        {
            log.Add($"{candidate.DevicePath}: open failed ({ex.Message})");
            stream = null;
            return false;
        }
    }

    /// <summary>
    /// The original selection method: open a candidate and see whether
    /// recognizable reports come out of it. Requires live data, so it only
    /// works while a key is held. Fallback only.
    /// </summary>
    private static bool TryProbeForData(HidDevice candidate, out HidStream? stream, List<string> log)
    {
        stream = null;
        HidStream? opened = null;

        try
        {
            opened = candidate.Open();
            opened.ReadTimeout = ProbeReadTimeoutMs;

            var probeBuffer = new byte[HidProtocolConfig.ReportLength];

            for (int attempt = 0; attempt < ProbeAttempts; attempt++)
            {
                int bytesRead;
                try
                {
                    bytesRead = opened.Read(probeBuffer, 0, probeBuffer.Length);
                }
                catch (TimeoutException)
                {
                    continue; // no data this attempt, try again within the same candidate
                }

                // Full parse (header + mode byte + depth sanity), not just a
                // header-byte match - a header-only check let a wrong
                // interface false-positive during testing.
                if (bytesRead > 0 && TryParse(probeBuffer.AsSpan(0, bytesRead)) is not null)
                {
                    log.Add($"{candidate.DevicePath}: data probe MATCHED");
                    stream = opened;
                    return true;
                }
            }

            log.Add($"{candidate.DevicePath}: opened, no matching data seen");
            opened.Dispose();
            return false;
        }
        catch (Exception ex)
        {
            opened?.Dispose();
            log.Add($"{candidate.DevicePath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends the confirmed vendor wake command that puts the analog
    /// interface into streaming mode (HidProtocolConfig.WakeReports),
    /// replacing the old manual "open iLumiPC once per power-on" step -
    /// see that constant for the confirmation history.
    ///
    /// Failures are swallowed on purpose. A device that is already awake
    /// may reject an unexpected feature report, and that must not stop a
    /// working pipeline from starting.
    /// </summary>
    private void TrySendWakeReports()
    {
        if (_stream is null) return;

        foreach (var report in HidProtocolConfig.WakeReports)
        {
            if (report.Length == 0) continue;

            // Output report first, feature second. The real device reports
            // MaxOutputReportLength=64 and MaxFeatureReportLength=0 on the
            // analog interface (capture, 13 Aug), so it does not support
            // feature reports at all - SetFeature would fail every time.
            // The fallback stays only in case a different firmware revision
            // does support them.
            try
            {
                _stream.Write(report);
            }
            catch
            {
                try
                {
                    _stream.SetFeature(report);
                }
                catch
                {
                    // Neither channel accepted it. Nothing to do but carry
                    // on - the manual wake step is still available.
                }
            }
        }
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
