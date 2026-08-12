using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace K617Mod.Core.Output;

/// <summary>
/// Maps controller action names (DIGITAL actions only - matches the
/// "action" strings used in keymapping.default.json) to the ViGEm
/// Xbox360Button that should be pressed/released for them. Analog
/// actions (STEER_LEFT/STEER_RIGHT/RT_ACCELERATE/LT_BRAKE) don't go
/// through here - VigemVirtualPad reads those straight off
/// ControllerStateSnapshot's Steering/Accelerate/Brake fields instead.
///
/// NOTE: Xbox360Button member names below match Nefarius.ViGEm.Client as
/// of the version pinned in K617Mod.Core.csproj at the time this was
/// written, not verified against a live build from this environment. If
/// a build error points at one of these names, the installed package
/// version likely renamed something - check IntelliSense for the
/// current member name and fix it here; this is the only file that
/// would need the change.
/// </summary>
public static class ActionButtonMap
{
    private static readonly Dictionary<string, Xbox360Button> Map = new()
    {
        ["LB_SHIFT_DOWN"] = Xbox360Button.LeftShoulder,
        ["RB_SHIFT_UP"] = Xbox360Button.RightShoulder,

        ["A_HANDBRAKE"] = Xbox360Button.A,
        ["B_REARVIEW"] = Xbox360Button.B,
        ["X_RESET_RECOVERY"] = Xbox360Button.X,
        ["Y_CAMERA_CYCLE"] = Xbox360Button.Y,

        ["DPAD_UP"] = Xbox360Button.Up,
        ["DPAD_DOWN"] = Xbox360Button.Down,
        ["DPAD_LEFT"] = Xbox360Button.Left,
        ["DPAD_RIGHT"] = Xbox360Button.Right,

        ["L3_HORN"] = Xbox360Button.LeftThumb,
        ["R3"] = Xbox360Button.RightThumb,

        ["VIEW_SCOREBOARD"] = Xbox360Button.Back,
        ["MENU_PAUSE"] = Xbox360Button.Start,
        ["GUIDE"] = Xbox360Button.Guide,
    };

    /// <summary>True if this action name has a real button wired to it.
    /// An action defined in the JSON but not (yet) present here is
    /// silently skipped by VigemVirtualPad, same as the Python build's
    /// behavior when its equivalent dictionary lookup missed.</summary>
    public static bool TryGetButton(string action, out Xbox360Button button) =>
        Map.TryGetValue(action, out button);

    public static IReadOnlyCollection<Xbox360Button> AllButtons => Map.Values.ToList();

    /// <summary>Every action name currently wired to a button - exposed mainly for tests.</summary>
    public static IReadOnlyCollection<string> MappedActions => Map.Keys.ToList();
}
