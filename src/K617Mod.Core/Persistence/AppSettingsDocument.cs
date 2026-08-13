namespace K617Mod.Core.Persistence;

/// <summary>
/// App-wide settings that aren't specific to any one profile - currently
/// which profile was active last (so the app can reopen on the same one
/// instead of defaulting to whatever's first alphabetically), and whether
/// the mod itself was last switched on or off.
/// </summary>
public sealed class AppSettingsDocument
{
    public string? LastActiveProfileName { get; set; }

    /// <summary>
    /// Whether the mod was on or off the last time it was explicitly
    /// started or stopped (via the tray toggle or the in-window switch) -
    /// not the last time the pipeline happened to stop for any reason.
    /// Null means "never set" (fresh install, or a settings file from
    /// before this existed), and is treated as on - matching the
    /// original always-auto-start-on-launch behaviour, so a first launch
    /// isn't a silent no-op.
    /// </summary>
    public bool? LastModeActive { get; set; }
}
