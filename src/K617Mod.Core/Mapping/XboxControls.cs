namespace K617Mod.Core.Mapping;

/// <summary>Rough grouping, used only to section the remap list in the UI.</summary>
public enum XboxControlGroup
{
    Face,
    Shoulder,
    Trigger,
    DPad,
    Stick,
    System
}

/// <summary>
/// One control on a physical Xbox pad, and whether a key bound to it is
/// read as analog depth or a digital press.
/// </summary>
/// <param name="ActionId">
/// The string used in a profile's ControllerMap. These match the names
/// already in keymapping.default.json and ActionButtonMap for the 17
/// controls that were mapped before this file existed - deliberately
/// NOT renamed, so nothing downstream breaks today. See the naming note
/// on <see cref="XboxControls"/>.
/// </param>
public sealed record XboxControl(
    string ActionId,
    string DisplayName,
    XboxControlGroup Group,
    InputType Kind);

/// <summary>
/// The complete control surface of a standard Xbox pad - every button
/// and axis direction a key could be bound to, whether or not anything
/// is currently bound to it.
///
/// This is what lets the remap screen show "Right Stick Up - unassigned"
/// rather than simply having no row for it. A profile's ControllerMap
/// only records what IS bound; the full list of what COULD be bound has
/// to live somewhere, and this is it.
///
/// NAMING NOTE (deliberate, flagged for a later decision): the original
/// 17 controls carry racing-flavoured ids - "A_HANDBRAKE",
/// "STEER_LEFT", "VIEW_SCOREBOARD" - because that's what the Forza
/// mapping called them and those strings are baked into
/// keymapping.default.json, ActionButtonMap and InputState's constants.
/// Now that this is a general controller replacement rather than a
/// racing mod, those ids read wrong. DisplayName carries the honest
/// Xbox label so the UI is correct regardless; renaming the ids
/// themselves is a single mechanical pass that can happen whenever, and
/// is cheap now but gets more expensive once saved profiles exist in the
/// wild referencing the old strings.
/// </summary>
public static class XboxControls
{
    // --- Ids for controls that had no equivalent in the racing mapping ---
    public const string LeftStickUp = "LS_UP";
    public const string LeftStickDown = "LS_DOWN";
    public const string RightStickLeft = "RS_LEFT";
    public const string RightStickRight = "RS_RIGHT";
    public const string RightStickUp = "RS_UP";
    public const string RightStickDown = "RS_DOWN";
    public const string RightThumbClick = "R3";
    public const string Guide = "GUIDE";

    public static IReadOnlyList<XboxControl> All { get; } = new List<XboxControl>
    {
        // --- Face buttons ---
        new("A_HANDBRAKE",      "A",                    XboxControlGroup.Face,     InputType.Digital),
        new("B_REARVIEW",       "B",                    XboxControlGroup.Face,     InputType.Digital),
        new("X_RESET_RECOVERY", "X",                    XboxControlGroup.Face,     InputType.Digital),
        new("Y_CAMERA_CYCLE",   "Y",                    XboxControlGroup.Face,     InputType.Digital),

        // --- Shoulders ---
        new("LB_SHIFT_DOWN",    "LB",                   XboxControlGroup.Shoulder, InputType.Digital),
        new("RB_SHIFT_UP",      "RB",                   XboxControlGroup.Shoulder, InputType.Digital),

        // --- Triggers (analog on a real pad, analog here too) ---
        new("LT_BRAKE",         "LT",                   XboxControlGroup.Trigger,  InputType.Analog),
        new("RT_ACCELERATE",    "RT",                   XboxControlGroup.Trigger,  InputType.Analog),

        // --- D-Pad ---
        new("DPAD_UP",          "D-Pad Up",             XboxControlGroup.DPad,     InputType.Digital),
        new("DPAD_DOWN",        "D-Pad Down",           XboxControlGroup.DPad,     InputType.Digital),
        new("DPAD_LEFT",        "D-Pad Left",           XboxControlGroup.DPad,     InputType.Digital),
        new("DPAD_RIGHT",       "D-Pad Right",          XboxControlGroup.DPad,     InputType.Digital),

        // --- Left stick. Left/Right were the racing mod's steering. ---
        new("STEER_LEFT",       "Left Stick Left",      XboxControlGroup.Stick,    InputType.Analog),
        new("STEER_RIGHT",      "Left Stick Right",     XboxControlGroup.Stick,    InputType.Analog),
        new(LeftStickUp,        "Left Stick Up",        XboxControlGroup.Stick,    InputType.Analog),
        new(LeftStickDown,      "Left Stick Down",      XboxControlGroup.Stick,    InputType.Analog),

        // --- Right stick. Entirely unmapped in the racing profile. ---
        new(RightStickLeft,     "Right Stick Left",     XboxControlGroup.Stick,    InputType.Analog),
        new(RightStickRight,    "Right Stick Right",    XboxControlGroup.Stick,    InputType.Analog),
        new(RightStickUp,       "Right Stick Up",       XboxControlGroup.Stick,    InputType.Analog),
        new(RightStickDown,     "Right Stick Down",     XboxControlGroup.Stick,    InputType.Analog),

        // --- Stick clicks ---
        new("L3_HORN",          "L3 (Left Stick Click)",   XboxControlGroup.Stick, InputType.Digital),
        new(RightThumbClick,    "R3 (Right Stick Click)",  XboxControlGroup.Stick, InputType.Digital),

        // --- System ---
        new("VIEW_SCOREBOARD",  "View",                 XboxControlGroup.System,   InputType.Digital),
        new("MENU_PAUSE",       "Menu",                 XboxControlGroup.System,   InputType.Digital),
        new(Guide,              "Guide",                XboxControlGroup.System,   InputType.Digital),
    };

    public static XboxControl? ById(string actionId) =>
        All.FirstOrDefault(c => string.Equals(c.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
}
