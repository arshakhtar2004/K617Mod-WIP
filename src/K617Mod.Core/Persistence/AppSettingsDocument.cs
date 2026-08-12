namespace K617Mod.Core.Persistence;

/// <summary>
/// App-wide settings that aren't specific to any one profile - currently
/// just which profile was active last, so the app can reopen on the
/// same one instead of defaulting to whatever's first alphabetically.
/// </summary>
public sealed class AppSettingsDocument
{
    public string? LastActiveProfileName { get; set; }
}
