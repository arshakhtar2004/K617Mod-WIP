using System.Text.Json;

namespace K617Mod.Core.Persistence;

/// <summary>
/// Stores each profile as its own human-readable JSON file under
/// {rootDirectory}/profiles/{name}.json, plus one small
/// {rootDirectory}/app-settings.json for app-wide state. Deliberately no
/// separate "index" file listing which profiles exist - the profiles
/// folder itself is the source of truth (ListProfileNames just lists
/// *.json files there), so there's nothing that can drift out of sync
/// with what's actually saved.
///
/// The root directory is passed in rather than hardcoded, so tests can
/// point this at a temp folder and the real app (Part 8) can point it at
/// the user's AppData folder - this class doesn't know or care which.
/// </summary>
public sealed class JsonProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true, // saved profiles are plain, readable JSON - fine to hand-edit if needed
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _profilesDirectory;
    private readonly string _appSettingsPath;

    public JsonProfileStore(string rootDirectory)
    {
        _profilesDirectory = Path.Combine(rootDirectory, "profiles");
        _appSettingsPath = Path.Combine(rootDirectory, "app-settings.json");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public IReadOnlyList<string> ListProfileNames()
    {
        if (!Directory.Exists(_profilesDirectory)) return Array.Empty<string>();

        return Directory.GetFiles(_profilesDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is not null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public ProfileDocument LoadProfile(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No saved profile named '{name}'.", path);
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProfileDocument>(json, Options)
            ?? throw new InvalidDataException($"Profile '{name}' parsed to null - the file may be empty or malformed.");
    }

    public void SaveProfile(ProfileDocument profile)
    {
        var path = PathFor(profile.Name);
        var json = JsonSerializer.Serialize(profile, Options);
        File.WriteAllText(path, json);
    }

    public void DeleteProfile(string name)
    {
        var path = PathFor(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        // Deleting something that was never there is a no-op, not an
        // error - simpler for callers than forcing an existence check first.
    }

    public string? GetLastActiveProfileName()
    {
        if (!File.Exists(_appSettingsPath)) return null;

        var json = File.ReadAllText(_appSettingsPath);
        var settings = JsonSerializer.Deserialize<AppSettingsDocument>(json, Options);
        return settings?.LastActiveProfileName;
    }

    public void SetLastActiveProfileName(string name)
    {
        ValidateName(name);
        var settings = new AppSettingsDocument { LastActiveProfileName = name };
        File.WriteAllText(_appSettingsPath, JsonSerializer.Serialize(settings, Options));
    }

    private string PathFor(string name)
    {
        ValidateName(name);
        return Path.Combine(_profilesDirectory, $"{name}.json");
    }

    /// <summary>
    /// Guards against an empty name and against path-separator characters
    /// that could otherwise let a profile name escape the profiles
    /// folder entirely (e.g. a name like "../something").
    /// </summary>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name can't be empty.", nameof(name));
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        if (name.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException(
                $"Profile name '{name}' contains characters that aren't allowed in a file name.", nameof(name));
        }
    }
}
