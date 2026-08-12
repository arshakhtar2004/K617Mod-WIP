namespace K617Mod.Core.Persistence;

/// <summary>
/// Built-in starter profile(s), so the app has something usable before
/// the person has saved anything themselves.
///
/// Deliberately only "Typing" lives here. An "FH6" default could have
/// been hardcoded here too, mirroring keymapping.default.json - but that
/// would mean the same 17-key mapping existing as data in two separate
/// places with nothing keeping them in sync. Instead, bootstrapping a
/// first-run "FH6" profile from the real keymapping.default.json file
/// (via KeyMapLoader.LoadDocumentFromFile) is left as Part 8's job - it's
/// really a question of "what should exist the first time the whole app
/// runs," which is an orchestrator-level decision, not a persistence
/// mechanic. "Typing" has no such data dependency, so it's safe to keep
/// here as a genuine constant.
/// </summary>
public static class DefaultProfiles
{
    public static ProfileDocument Typing() => new()
    {
        Name = "Typing",
        Description = "Normal keyboard behavior - no controller mapping active.",
        SteeringCurveExponent = 1.0,
        ThrottleBrakeCurveExponent = 2.0,
        DigitalPressThreshold = 0.3,
        // KeyMapping left as an empty KeyMapDocument on purpose - nothing
        // bound to a controller action, so suppression/output should
        // stay fully inactive while this profile is selected.
    };
}
