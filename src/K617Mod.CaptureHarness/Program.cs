using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HidSharp;
using K617Mod.Core.Hid;

namespace K617Mod.CaptureHarness;

/// <summary>
/// Opens EVERY HID interface the K617 HE exposes and records every byte
/// that comes out of any of them for a fixed window, with no filtering and
/// no assumptions about what a report should look like.
///
/// Deliberately not built on K617HidSource. That class picks one interface
/// and discards anything that doesn't parse as a known report - which is
/// correct for the mod and useless here, where the unrecognised traffic is
/// the whole point.
///
/// SCOPE, and it matters: this records what the KEYBOARD SENDS. It cannot
/// see what other software sends TO the keyboard, because that traffic
/// never reaches this process. If the device is asleep the analog interface
/// will most likely be silent, and that silence is a finding rather than a
/// bug in this tool.
/// </summary>
internal static class Program
{
    private const int DefaultCaptureSeconds = 3;
    private const int ReadTimeoutMs = 100; // short, so reader threads notice the window closing

    private static readonly List<CapturedReport> Captured = new();
    private static readonly object CaptureLock = new();

    private static volatile bool _capturing;
    private static volatile bool _finished;
    private static readonly Stopwatch Clock = new();

    private static int Main(string[] args)
    {
        var seconds = DefaultCaptureSeconds;
        if (args.Length > 0 && int.TryParse(args[0], out var parsed) && parsed > 0)
        {
            seconds = parsed;
        }

        Console.WriteLine("K617 HE - raw capture harness");
        Console.WriteLine("=============================\n");

        var devices = DeviceList.Local
            .GetHidDevices(HidProtocolConfig.VendorId, HidProtocolConfig.ProductId)
            .ToList();

        if (devices.Count == 0)
        {
            Console.WriteLine(
                $"No K617 HE found (VID=0x{HidProtocolConfig.VendorId:X4} " +
                $"PID=0x{HidProtocolConfig.ProductId:X4}). Is it plugged in?");
            return 1;
        }

        Console.WriteLine($"Found {devices.Count} interface(s).\n");

        var inventory = new List<InterfaceInfo>();
        for (int i = 0; i < devices.Count; i++)
        {
            var info = Describe(i, devices[i]);
            inventory.Add(info);
            PrintInterface(info);
        }

        // --- Open everything that will open ---
        var open = new List<OpenInterface>();
        foreach (var (device, info) in devices.Zip(inventory))
        {
            try
            {
                var stream = device.Open();
                stream.ReadTimeout = ReadTimeoutMs;
                open.Add(new OpenInterface(info, stream));
                info.Opened = true;
            }
            catch (Exception ex)
            {
                info.OpenError = ex.Message;
                Console.WriteLine($"  [{info.Index}] could not open: {ex.Message}");
            }
        }

        if (open.Count == 0)
        {
            Console.WriteLine(
                "\nNothing could be opened. Something else is holding the device - " +
                "close iLumiPC, any Redragon utility, K617Mod.Ui.exe, and any browser " +
                "tab that connected to the keyboard.");
            return 1;
        }

        Console.WriteLine($"\nOpened {open.Count} of {devices.Count} interface(s).");
        Console.WriteLine(
            "\nNOTE: this records what the keyboard SENDS. It cannot see what iLumiPC " +
            "sends TO the keyboard.\n");

        // Reader threads start now but stay idle until _capturing goes true,
        // so the window starts on the keypress rather than on thread startup.
        var threads = open.Select(o =>
        {
            var thread = new Thread(() => ReadLoop(o)) { IsBackground = true, Name = $"reader-{o.Info.Index}" };
            thread.Start();
            return thread;
        }).ToList();

        Console.WriteLine($"Press Enter to start a {seconds} second capture.");
        Console.WriteLine("Press and release keys during the window - including the analog ones.");
        Console.ReadLine();

        Clock.Restart();
        _capturing = true;

        for (int remaining = seconds; remaining > 0; remaining--)
        {
            Console.WriteLine($"  capturing... {remaining}");
            Thread.Sleep(1000);
        }

        _capturing = false;
        Clock.Stop();
        _finished = true;

        // Give the reader threads one timeout's grace to notice and exit.
        Thread.Sleep(ReadTimeoutMs * 2);
        foreach (var o in open)
        {
            try { o.Stream.Dispose(); } catch { /* closing down anyway */ }
        }
        foreach (var thread in threads)
        {
            thread.Join(500);
        }

        Console.WriteLine("\nCapture finished.\n");

        List<CapturedReport> reports;
        lock (CaptureLock)
        {
            reports = Captured.OrderBy(r => r.ElapsedMs).ToList();
        }

        Summarise(reports, inventory);
        var written = WriteFiles(reports, inventory, seconds);

        Console.WriteLine("\nWritten:");
        foreach (var path in written)
        {
            Console.WriteLine("  " + path);
        }

        Console.WriteLine("\nSend the .json file back. Press Enter to close.");
        Console.ReadLine();
        return 0;
    }

    /// <summary>
    /// Reads continuously from the moment the interface opens, but only
    /// RECORDS while the window is open. Reading throughout rather than
    /// sleeping matters: an unread interface has its reports queued by
    /// Windows, and those would then be drained the instant the window
    /// opened and timestamped as though they had just arrived. Reading and
    /// discarding keeps the queue empty, so what lands in the capture
    /// actually happened during the three seconds.
    /// </summary>
    private static void ReadLoop(OpenInterface o)
    {
        var buffer = new byte[Math.Max(o.Info.MaxInputReportLength, HidProtocolConfig.ReportLength)];

        while (!_finished)
        {
            int bytesRead;
            try
            {
                bytesRead = o.Stream.Read(buffer, 0, buffer.Length);
            }
            catch (TimeoutException)
            {
                continue; // no traffic in the last ReadTimeoutMs, perfectly normal
            }
            catch (Exception)
            {
                break; // stream closed or device went away
            }

            if (bytesRead <= 0) continue;
            if (!_capturing) continue; // drained and thrown away

            var bytes = new byte[bytesRead];
            Array.Copy(buffer, bytes, bytesRead);

            lock (CaptureLock)
            {
                Captured.Add(new CapturedReport(Clock.Elapsed.TotalMilliseconds, o.Info.Index, bytes));
            }
        }
    }

    private static InterfaceInfo Describe(int index, HidDevice device)
    {
        var info = new InterfaceInfo
        {
            Index = index,
            DevicePath = Safe(() => device.DevicePath),
            ProductName = Safe(() => device.GetProductName()),
            Manufacturer = Safe(() => device.GetManufacturer()),
        };

        try { info.MaxInputReportLength = device.GetMaxInputReportLength(); } catch { }
        try { info.MaxOutputReportLength = device.GetMaxOutputReportLength(); } catch { }
        try { info.MaxFeatureReportLength = device.GetMaxFeatureReportLength(); } catch { }

        try
        {
            var descriptor = device.GetReportDescriptor();
            foreach (var item in descriptor.DeviceItems)
            {
                foreach (var usage in item.Usages.GetAllValues())
                {
                    info.Usages.Add($"0x{usage >> 16:X4}:0x{usage & 0xFFFF:X4}");
                }
            }
        }
        catch (Exception ex)
        {
            info.DescriptorError = ex.Message;
        }

        return info;
    }

    private static void PrintInterface(InterfaceInfo info)
    {
        Console.WriteLine($"  [{info.Index}] {info.ProductName}");
        Console.WriteLine($"       path     : {info.DevicePath}");
        Console.WriteLine($"       usages   : {(info.Usages.Count == 0 ? info.DescriptorError ?? "none" : string.Join(", ", info.Usages))}");
        Console.WriteLine($"       lengths  : in={info.MaxInputReportLength} out={info.MaxOutputReportLength} feature={info.MaxFeatureReportLength}");

        var isAnalog = info.Usages.Any(u => u.StartsWith($"0x{HidProtocolConfig.UsagePage:X4}:", StringComparison.OrdinalIgnoreCase));
        if (isAnalog)
        {
            Console.WriteLine("       ^^ this is the analog interface the mod uses");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Splits what arrived into "matches the layout we already understand"
    /// and "doesn't" - the second group being the reason this harness
    /// exists. Unknown reports are grouped by their first six bytes, since
    /// that's where a report's shape shows up.
    /// </summary>
    private static void Summarise(List<CapturedReport> reports, List<InterfaceInfo> inventory)
    {
        Console.WriteLine($"Total reports: {reports.Count}\n");

        foreach (var info in inventory)
        {
            var mine = reports.Where(r => r.InterfaceIndex == info.Index).ToList();
            Console.WriteLine($"  [{info.Index}] {mine.Count} report(s){(info.Opened ? "" : " (never opened)")}");

            if (mine.Count == 0) continue;

            var known = mine.Where(r => MatchesKnownLayout(r.Bytes)).ToList();
            var unknown = mine.Except(known).ToList();

            Console.WriteLine($"       known layout  : {known.Count}");
            Console.WriteLine($"       UNRECOGNISED  : {unknown.Count}");

            foreach (var group in unknown
                         .GroupBy(r => Hex(r.Bytes.Take(6).ToArray()))
                         .OrderByDescending(g => g.Count())
                         .Take(12))
            {
                Console.WriteLine($"         {group.Key} ... x{group.Count()}   first at {group.Min(r => r.ElapsedMs):F0} ms");
            }

            Console.WriteLine();
        }

        if (reports.Count == 0)
        {
            Console.WriteLine(
                "Nothing arrived at all. If the analog interface opened cleanly, that is the\n" +
                "un-woken device: it streams nothing until iLumiPC's Travel Test page has been\n" +
                "opened once since power-on.\n");
        }
    }

    /// <summary>
    /// The layout already documented in HidProtocolConfig. Applied loosely
    /// on purpose - header and mode byte only, no depth sanity check, so a
    /// report that is nearly-but-not-quite known still lands in the
    /// unrecognised bucket where it can be looked at.
    /// </summary>
    private static bool MatchesKnownLayout(byte[] raw)
    {
        var data = raw.AsSpan();
        if (data.Length > 0 && data[0] == 1) data = data[1..];
        if (data.Length <= HidProtocolConfig.DepthHighIndex) return false;
        if (data[0] != HidProtocolConfig.HeaderByte) return false;

        var mode = data[HidProtocolConfig.ModeByteIndex];
        return mode == (byte)ReportMode.Live || mode == (byte)ReportMode.Summary;
    }

    private static List<string> WriteFiles(
        List<CapturedReport> reports, List<InterfaceInfo> inventory, int seconds)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var jsonPath = Path.Combine(AppContext.BaseDirectory, $"k617-capture-{stamp}.json");
        var textPath = Path.Combine(AppContext.BaseDirectory, $"k617-capture-{stamp}.txt");

        var payload = new
        {
            capturedAtLocal = DateTime.Now.ToString("O"),
            captureSeconds = seconds,
            vendorId = HidProtocolConfig.VendorId,
            productId = HidProtocolConfig.ProductId,
            interfaces = inventory,
            reports = reports.Select(r => new
            {
                elapsedMs = Math.Round(r.ElapsedMs, 3),
                iface = r.InterfaceIndex,
                length = r.Bytes.Length,
                hex = Hex(r.Bytes),
                known = MatchesKnownLayout(r.Bytes),
            }),
        };

        File.WriteAllText(jsonPath,
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));

        // Plain text as well, because a human scanning for "what looks odd"
        // reads a timestamped hex column far faster than JSON.
        var text = new StringBuilder();
        text.AppendLine($"K617 HE capture - {DateTime.Now:O} - {seconds}s - {reports.Count} report(s)");
        text.AppendLine();
        foreach (var info in inventory)
        {
            text.AppendLine($"[{info.Index}] {info.ProductName} | {info.DevicePath}");
            text.AppendLine($"     usages {(info.Usages.Count == 0 ? info.DescriptorError ?? "none" : string.Join(", ", info.Usages))}");
        }
        text.AppendLine();
        text.AppendLine("    ms  if  ?  bytes");
        foreach (var r in reports)
        {
            text.AppendLine($"{r.ElapsedMs,6:F0}  {r.InterfaceIndex,2}  {(MatchesKnownLayout(r.Bytes) ? " " : "*")}  {Hex(r.Bytes)}");
        }
        text.AppendLine();
        text.AppendLine("* = does not match the documented report layout");
        File.WriteAllText(textPath, text.ToString());

        return new List<string> { jsonPath, textPath };
    }

    private static string Hex(byte[] bytes) =>
        string.Join(' ', bytes.Select(b => b.ToString("x2")));

    private static string Safe(Func<string> get)
    {
        try { return get(); }
        catch (Exception ex) { return $"<{ex.GetType().Name}>"; }
    }

    private sealed record CapturedReport(double ElapsedMs, int InterfaceIndex, byte[] Bytes);

    private sealed record OpenInterface(InterfaceInfo Info, HidStream Stream);

    private sealed class InterfaceInfo
    {
        public int Index { get; set; }
        public string DevicePath { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Manufacturer { get; set; } = string.Empty;
        public int MaxInputReportLength { get; set; }
        public int MaxOutputReportLength { get; set; }
        public int MaxFeatureReportLength { get; set; }
        public List<string> Usages { get; set; } = new();
        public string? DescriptorError { get; set; }
        public bool Opened { get; set; }
        public string? OpenError { get; set; }
    }
}
