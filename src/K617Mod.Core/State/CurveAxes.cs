namespace K617Mod.Core.State;

/// <summary>
/// One axis that carries its own response curve, and the controls that
/// follow it.
/// </summary>
public sealed record CurveAxis(
    string Id,
    string DisplayName,
    string Description,
    IReadOnlyList<string> ActionIds);

/// <summary>
/// The six axes a curve can be set for.
///
/// A curve belongs to an *axis*, not to an individual direction: left
/// and right on the same stick share one curve, as do up and down. That
/// keeps a stick symmetrical, which is almost always what's wanted -
/// steering that responds differently pushing left than right would feel
/// broken rather than tuned. It also generalises the original racing
/// arrangement exactly: accelerate, brake and steering were three curves
/// because those were the only analog controls bound, with the two
/// steering directions already sharing one.
/// </summary>
public static class CurveAxes
{
    public const string LeftTrigger = "LT";
    public const string RightTrigger = "RT";
    public const string LeftStickX = "LS_X";
    public const string LeftStickY = "LS_Y";
    public const string RightStickX = "RS_X";
    public const string RightStickY = "RS_Y";

    public static IReadOnlyList<CurveAxis> All { get; } = new List<CurveAxis>
    {
        new(RightTrigger, "RT — Accelerate", "Right trigger. Softened by default so shallow presses stay gentle.",
            new[] { "RT_ACCELERATE" }),

        new(LeftTrigger, "LT — Brake", "Left trigger. Softened by default, same as RT.",
            new[] { "LT_BRAKE" }),

        new(LeftStickX, "Left Stick — Left / Right", "Steering. Linear by default: curving it makes small corrections mushy.",
            new[] { "STEER_LEFT", "STEER_RIGHT" }),

        new(LeftStickY, "Left Stick — Up / Down", "Left stick vertical.",
            new[] { "LS_UP", "LS_DOWN" }),

        new(RightStickX, "Right Stick — Left / Right", "Usually camera horizontal.",
            new[] { "RS_LEFT", "RS_RIGHT" }),

        new(RightStickY, "Right Stick — Up / Down", "Usually camera vertical.",
            new[] { "RS_UP", "RS_DOWN" }),
    };

    public static CurveAxis? ById(string id) =>
        All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The curve set a brand-new profile starts with. Triggers get the
    /// old exponent-2.0 softening, everything else is straight 1:1 -
    /// matching exactly how the app behaved before curves were editable.
    /// </summary>
    public static Dictionary<string, ResponseCurve> Defaults() => new(StringComparer.OrdinalIgnoreCase)
    {
        [RightTrigger] = ResponseCurve.FromExponent(2.0),
        [LeftTrigger] = ResponseCurve.FromExponent(2.0),
        [LeftStickX] = ResponseCurve.Linear(),
        [LeftStickY] = ResponseCurve.Linear(),
        [RightStickX] = ResponseCurve.Linear(),
        [RightStickY] = ResponseCurve.Linear(),
    };
}
