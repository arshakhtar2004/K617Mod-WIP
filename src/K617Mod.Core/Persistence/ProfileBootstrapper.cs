using System.Text.Json;

namespace K617Mod.Core.Persistence;

/// <summary>
/// Ensures the store holds the five profiles the app expects, and
/// resolves which one should be active on launch. Both the console host
/// and the WPF UI call this the same way, rather than duplicating
/// first-run logic in two entry points.
///
/// The set is deliberately fixed at five rather than letting profiles be
/// created and deleted freely: one read-only "Default" that is always a
/// known-good baseline, plus four editable slots that start life as
/// copies of it. That means breaking a profile is always recoverable by
/// copying Default over it again, and the UI never has to handle an
/// empty profile list or profile-creation flow.
///
/// The old "Typing" profile is no longer bootstrapped. Its job - put the
/// keyboard back to normal - now belongs to the app's master ON/OFF
/// switch, which stops the pipeline entirely rather than loading a
/// profile that happens to bind nothing. DefaultProfiles.Typing() is
/// left in place for anyone who still wants it.
/// </summary>
public static class ProfileBootstrapper
{
    public const string DefaultProfileName = "Default";

    public static IReadOnlyList<string> EditableProfileNames { get; } = new[]
    {
        "Profile 2",
        "Profile 3",
        "Profile 4",
        "Profile 5",
    };

    /// <summary>Default first, then the four editable slots, in display order.</summary>
    public static IReadOnlyList<string> AllProfileNames { get; } =
        new[] { DefaultProfileName }.Concat(EditableProfileNames).ToList();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>
    /// Creates any of the five profiles that are missing, without
    /// touching ones that already exist, then returns the name that
    /// should be loaded on launch: whatever was last active, or
    /// "Default" on a genuine first run.
    /// </summary>
    /// <param name="defaultProfilePath">
    /// Path to profile.default.json. Only read when a profile actually
    /// needs creating, so that file stays the single source of truth for
    /// the baseline mapping rather than a second copy hardcoded here.
    /// </param>
    public static string EnsureBootstrappedAndGetStartupProfileName(IProfileStore store, string defaultProfilePath) =>
        EnsureBootstrapped(store, () => LoadTemplate(defaultProfilePath));

    /// <summary>
    /// Same thing, but taking the baseline from the copy embedded in
    /// this assembly rather than from a file on disk. This is what the
    /// shipping app uses.
    ///
    /// Reading the baseline out of AppContext.BaseDirectory is fine
    /// running from bin/Debug and stops being fine the moment the app is
    /// published as a single file - it would launch, find no baseline,
    /// and create no profiles at all. The embedded copy lives inside the
    /// DLL, so it is present in every build and every layout.
    ///
    /// The loose file is still the one to edit; the embedded copy is
    /// produced from it at build time, so the two cannot drift.
    /// </summary>
    public static string EnsureBootstrappedAndGetStartupProfileName(IProfileStore store) =>
        EnsureBootstrapped(store, DefaultProfileTemplate.Load);

    /// <param name="loadTemplate">
    /// Deferred on purpose: the baseline is only read when a profile
    /// actually needs creating, so a missing or unreadable template is
    /// not an error on the normal path where all five already exist.
    /// </param>
    private static string EnsureBootstrapped(IProfileStore store, Func<ProfileDocument> loadTemplate)
    {
        var existing = store.ListProfileNames();
        var missing = AllProfileNames
            .Where(name => !existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missing.Count > 0)
        {
            var template = loadTemplate();

            foreach (var name in missing)
            {
                var profile = Clone(template);
                profile.Name = name;

                if (string.Equals(name, DefaultProfileName, StringComparison.OrdinalIgnoreCase))
                {
                    profile.IsReadOnly = true;
                    profile.Description = "Read-only baseline. Copy this over another slot to reset it.";
                }
                else
                {
                    profile.IsReadOnly = false;
                    profile.Description = "Editable slot, started as a copy of Default.";
                }

                store.SaveProfile(profile);
            }
        }

        // Checked against the five names this app knows about, not
        // against whatever files happen to be in the profile folder.
        //
        // Those are not the same set. %AppData%\K617Mod\profiles still
        // holds FH6.json and Typing.json from the pre-five-profile
        // design, and "is it in the folder" would happily resolve the
        // startup profile to FH6 - a profile the app would then run
        // while every profile dropdown showed a blank selection, because
        // "FH6" is not one of the five they list. Anything outside the
        // fixed set falls back to Default instead.
        var lastActive = store.GetLastActiveProfileName();
        return lastActive is not null
               && AllProfileNames.Contains(lastActive, StringComparer.OrdinalIgnoreCase)
               && store.ListProfileNames().Contains(lastActive, StringComparer.OrdinalIgnoreCase)
            ? lastActive
            : DefaultProfileName;
    }

    public static bool IsEditable(string profileName) =>
        !string.Equals(profileName, DefaultProfileName, StringComparison.OrdinalIgnoreCase);

    private static ProfileDocument LoadTemplate(string defaultProfilePath)
    {
        if (!File.Exists(defaultProfilePath))
        {
            throw new FileNotFoundException(
                "The baseline profile file is missing, so profiles can't be created from it.",
                defaultProfilePath);
        }

        var json = File.ReadAllText(defaultProfilePath);
        return JsonSerializer.Deserialize<ProfileDocument>(json, JsonOptions)
            ?? throw new InvalidDataException($"'{defaultProfilePath}' parsed to null - empty or malformed.");
    }

    /// <summary>
    /// Deep copy via a serialize/deserialize round trip. Slower than
    /// copying fields by hand, but it can't silently miss a field when
    /// ProfileDocument gains one later - which matters here, since a
    /// half-copied profile would look fine until the missing setting
    /// mattered.
    /// </summary>
    private static ProfileDocument Clone(ProfileDocument source) =>
        JsonSerializer.Deserialize<ProfileDocument>(
            JsonSerializer.Serialize(source, JsonOptions), JsonOptions)!;
}
