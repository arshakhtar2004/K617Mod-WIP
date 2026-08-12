namespace K617Mod.Core.Hid;

/// <summary>
/// Mirrors the two report types the K617 HE's analog HID interface sends.
/// Confirmed via WebHID capture: byte[4] of every report is one of these
/// two values. Anything else isn't a report this device produces and is
/// treated as noise upstream (see K617HidSource.TryParse).
/// </summary>
public enum ReportMode
{
    /// <summary>Live per-tick analog depth update while a key is held.</summary>
    Live = 5,

    /// <summary>
    /// End-of-press calibration summary. Still carries a usable depth
    /// value at the same byte offset as a Live report, so it's accepted
    /// rather than discarded - matches the original Python reader's
    /// behavior of not special-casing either mode at parse time.
    /// </summary>
    Summary = 3
}
