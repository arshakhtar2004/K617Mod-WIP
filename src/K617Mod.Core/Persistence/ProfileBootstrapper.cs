using K617Mod.Core.Mapping;

namespace K617Mod.Core.Persistence;

/// <summary>
/// Ensures the store has sane starting profiles and resolves which one
/// should be active on launch. Both the console host (Part 8) and the
/// future WPF UI (Part 7) can call this the same way, rather than
/// duplicating first-run logic in two different entry points.
///
/// This is the piece Part 6's README deferred - "what should exist the
/// first time the whole app runs" only makes sense once there's an
/// actual app entry point to run, which is what Part 8 provides.
/// </summary>
public static class ProfileBootstrapper
{
    public const string TypingProfileName = "Typing";
    public const string DefaultFh6ProfileName = "FH6";

    /// <summary>
    /// Makes sure "Typing" and a starter "FH6" profile both exist in the
    /// store (without overwriting either if already present), then
    /// returns the name that should be loaded on launch: whatever was
    /// last active, or "FH6" on a genuine first run.
    /// </summary>
    /// <param name="fh6DefaultMappingPath">
    /// Path to keymapping.default.json - only read if no "FH6" profile
    /// is saved yet, so this stays the single source of truth for that
    /// mapping rather than a second hardcoded copy living here.
    /// </param>
    public static string EnsureBootstrappedAndGetStartupProfileName(IProfileStore store, string fh6DefaultMappingPath)
    {
        var existing = store.ListProfileNames();

        if (!existing.Contains(TypingProfileName, StringComparer.OrdinalIgnoreCase))
        {
            store.SaveProfile(DefaultProfiles.Typing());
        }

        if (!existing.Contains(DefaultFh6ProfileName, StringComparer.OrdinalIgnoreCase))
        {
            var mappingDoc = KeyMapLoader.LoadDocumentFromFile(fh6DefaultMappingPath);
            store.SaveProfile(new ProfileDocument
            {
                Name = DefaultFh6ProfileName,
                Description = "Forza Horizon 6 - the original 17-key racing mapping.",
                KeyMapping = mappingDoc,
            });
        }

        return store.GetLastActiveProfileName() ?? DefaultFh6ProfileName;
    }
}
