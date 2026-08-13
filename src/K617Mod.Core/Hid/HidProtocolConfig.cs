namespace K617Mod.Core.Hid;

/// <summary>
/// K617 HE analog HID protocol constants. Confirmed via real HID capture
/// (WebHID + iLumiPC + Chrome DevTools) against the physical keyboard -
/// nothing here is guessed. Direct port of the Python build's config.py
/// protocol-level section. Key-to-controller-action mapping intentionally
/// does NOT live here - that's a separate module, kept independent of
/// this one on purpose.
/// </summary>
public static class HidProtocolConfig
{
    // --- Device identity ---
    public const int VendorId = 0x2E3C;
    public const int ProductId = 0xC365;
    public const int UsagePage = 65307; // 0xFF1B - vendor-specific analog interface

    // --- Report layout ---
    public const int ReportLength = 64;   // 1 report-id byte + 63 payload bytes
    public const byte HeaderByte = 0x21;   // byte[0], constant on live/summary reports
    public const int ModeByteIndex = 4;
    public const int KeyIdRowIndex = 6;
    public const int KeyIdColIndex = 7;
    public const int DepthLowIndex = 8;
    public const int DepthHighIndex = 9;

    // --- Depth range ---
    public const int RawDepthMin = 0;
    public const int RawDepthMax = 340;

    /// <summary>
    /// Above this, a reading is treated as corrupted and discarded
    /// outright rather than clamped - clamping a bogus 65535 up to 340
    /// would silently produce a false full-scale spike, which is worse
    /// than dropping one bad report. Matches config.py's reasoning exactly.
    /// </summary>
    public const int RawDepthSanityMax = 500;

    // --- Device wake ---

    /// <summary>
    /// The vendor command that switches the analog interface into
    /// streaming mode, replacing the old "open iLumiPC's Travel Test page
    /// once per power-on" manual step.
    ///
    /// Captured via a WebHID trace of illumipc.com's Travel Test toggle.
    /// An earlier single-packet candidate from the same capture family
    /// (cmd=0x21 sub=0x02, this exact shape) had FAILED an isolated,
    /// clean-baseline test via Test-WakePacket.ps1 - the device replied
    /// with its generic acknowledgment but produced zero depth reports
    /// afterward, which is why this was not wired in on the capture
    /// alone. It was retested end-to-end on the Python build
    /// (test_wake_mvp.py, same clean-baseline structure: verified silent
    /// after a full reboot, packet sent alone, depth confirmed on the
    /// next keypress) and passed - see notes.md, "Wake command confirmed
    /// on the MVP, 13 Aug".
    ///
    /// Each entry is one full report, first byte = report id.
    /// K617HidSource sends them in order immediately after opening,
    /// trying a plain Write first (this device's analog interface reports
    /// MaxFeatureReportLength=0, so SetFeature is expected to fail here -
    /// see TrySendWakeReports).
    /// </summary>
    public static readonly byte[][] WakeReports = { BuildWakeReport() };

    private static byte[] BuildWakeReport()
    {
        // Zero-filled to the full 64-byte report (report id + 63-byte
        // payload) up front, then the known bytes copied over the front -
        // matches the exact wire form validated by test_wake_mvp.py.
        var report = new byte[ReportLength];
        byte[] known =
        {
            0x01, 0x21, 0x00, 0x00, 0x00, 0x18, 0x02,
            0x3e, 0x26, 0x3e, 0x1e, 0x1e, 0x1e, 0x3e, 0x1e, 0x1e, 0x3e, 0x3e, 0x3e, 0x3e,
            0x00, 0x0e,
        };
        Array.Copy(known, report, known.Length);
        return report;
    }
}
