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
}
