using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using K617Mod.Core.Mapping;
using K617Mod.Core.Persistence;

namespace K617Mod.Ui;

/// <summary>
/// Supplies the remap page with "which physical key is currently bound
/// to each Xbox control".
///
/// The profile stores the relationship the other way round - key name ->
/// action, because that's the direction the HID pipeline looks it up.
/// The remap page needs action -> key name, so this inverts it once at
/// load rather than searching the map for every label on screen.
///
/// Read-only for now. Editing arrives with the buffer/Apply behaviour.
/// </summary>
public sealed class RemapViewModel
{
    public const string Unassigned = "—";

    /// <summary>
    /// Action id -> physical key name. Every control in
    /// XboxControls.All is present, unmapped ones holding
    /// <see cref="Unassigned"/>, so a binding can never miss and
    /// silently render blank.
    /// </summary>
    public IReadOnlyDictionary<string, string> KeyForAction { get; }

    /// <summary>Empty when the profile loaded cleanly. Shown on the page if not.</summary>
    public string LoadError { get; } = string.Empty;

    public RemapViewModel()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var control in XboxControls.All)
        {
            map[control.ActionId] = Unassigned;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");

        try
        {
            if (!File.Exists(path))
            {
                LoadError = $"Default profile not found at:\n{path}";
            }
            else
            {
                var document = JsonSerializer.Deserialize<ProfileDocument>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                var controllerMap = document?.KeyMapping?.ControllerMap;
                if (controllerMap is null)
                {
                    LoadError = "Profile loaded but contained no ControllerMap.";
                }
                else
                {
                    foreach (var (keyName, binding) in controllerMap)
                    {
                        if (!string.IsNullOrWhiteSpace(binding.Action))
                        {
                            map[binding.Action] = keyName;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LoadError = $"Could not read the default profile: {ex.Message}";
        }

        KeyForAction = map;
    }
}
