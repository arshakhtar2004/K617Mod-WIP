using System.Reflection;
using System.Text.Json;

namespace K617Mod.Core.Persistence;

/// <summary>
/// Reads the baseline profile that the five bootstrapped profiles are
/// copied from.
///
/// The file is compiled into the assembly as an embedded resource, and
/// this reads it from there rather than from disk. The reason is
/// publishing: `profile.default.json` used to be found only via
/// AppContext.BaseDirectory, which is correct when running from
/// bin/Debug but is exactly the kind of assumption that breaks the
/// moment the app is published as a single file - the app would start,
/// find no baseline, and fail to create any profiles at all. An
/// embedded resource travels inside the DLL, so it is present in every
/// build, publish and layout without anything having to remember to copy
/// it.
///
/// The loose copy next to the exe is kept as well. It is what the
/// console host and the harnesses read, and it is the file to edit when
/// changing the baseline - the embedded copy is produced from it at
/// build time, so the two cannot drift.
/// </summary>
public static class DefaultProfileTemplate
{
    /// <summary>
    /// Resource name as the compiler forms it: root namespace, then the
    /// folder path with separators turned into dots.
    /// </summary>
    private const string ResourceName = "K617Mod.Core.Mapping.Data.profile.default.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    /// <summary>The baseline profile, read from the copy embedded in this assembly.</summary>
    public static ProfileDocument Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"The embedded baseline profile '{ResourceName}' is missing from the assembly. " +
                "This means the build stopped embedding it - check the EmbeddedResource entry in K617Mod.Core.csproj.");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<ProfileDocument>(json, JsonOptions)
            ?? throw new InvalidDataException("The embedded baseline profile parsed to null - empty or malformed.");
    }
}
