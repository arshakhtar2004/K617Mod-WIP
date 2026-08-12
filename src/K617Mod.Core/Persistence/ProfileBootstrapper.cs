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
    public static string EnsureBootstrappedAndGetStartupProfileName(IProfileStore store, string defaultProfilePath)
    {
        var existing = store.ListProfileNames();
        var missing = AllProfileNames
            .Where(name => !existing.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missing.Count > 0)
        {
            var template = LoadTemplate(defaultProfilePath);

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

        var lastActive = store.GetLastActiveProfileName();
        return lastActive is not null
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
