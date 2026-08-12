using K617Mod.Core.Hid;

namespace K617Mod.Core.Suppression;

/// <summary>
/// Pure string matching: does a device's Windows hardware ID belong to
/// the K617 HE? No driver, no P/Invoke involved - this is the one piece
/// of Part 5 that's actually unit-testable without real hardware, and
/// it's kept in its own file specifically so it can be tested in
/// isolation from everything else here.
///
/// References Hid.HidProtocolConfig's VendorId/ProductId directly rather
/// than duplicating them (unlike State.InputTuningConfig, which
/// deliberately duplicated the depth range instead of referencing Part
/// 1). Different reasoning here: VID/PID are a device's fixed identity,
/// not a tunable judgment call - forking that into a third copy would
/// just create a way for two copies to silently drift out of sync if
/// the device identity ever needs updating.
/// </summary>
public static class HardwareIdMatcher
{
    private static readonly string ExpectedSubstring =
        $"VID_{HidProtocolConfig.VendorId:X4}&PID_{HidProtocolConfig.ProductId:X4}";

    /// <summary>True if the given Windows hardware ID string belongs to the K617 HE.</summary>
    public static bool IsK617(string? hardwareId) =>
        hardwareId is not null &&
        hardwareId.Contains(ExpectedSubstring, StringComparison.OrdinalIgnoreCase);
}
