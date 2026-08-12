using System.Text.Json;

namespace K617Mod.Core.Mapping;

/// <summary>
/// Reads keymapping JSON and builds a KeyMap from it. This is the only
/// class in the Mapping namespace that knows JSON exists - KeyMap itself
/// stays pure and testable without any file ever being involved.
///
/// Split into two layers on purpose: the *Document methods parse JSON
/// into the plain KeyMapDocument DTO only, while FromDocument/
/// LoadFromJson/LoadFromFile go the rest of the way to a real IKeyMap.
/// Added in Part 6 (Persistence): a saved profile embeds a
/// KeyMapDocument directly, so that half needed to be reusable on its
/// own rather than forcing a round-trip through IKeyMap just to get
/// plain JSON data back out. LoadFromJson/LoadFromFile behave exactly as
/// before - this is a pure addition, not a behavior change.
/// </summary>
public static class KeyMapLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IKeyMap LoadFromFile(string path) => LoadFromJson(File.ReadAllText(path));

    public static IKeyMap LoadFromJson(string json) => FromDocument(LoadDocumentFromJson(json));

    /// <summary>Parses JSON into the raw KeyMapDocument DTO, without building a KeyMap from it. Added for Part 6.</summary>
    public static KeyMapDocument LoadDocumentFromFile(string path) => LoadDocumentFromJson(File.ReadAllText(path));

    /// <summary>Parses JSON into the raw KeyMapDocument DTO, without building a KeyMap from it. Added for Part 6.</summary>
    public static KeyMapDocument LoadDocumentFromJson(string json) =>
        JsonSerializer.Deserialize<KeyMapDocument>(json, Options)
            ?? throw new InvalidDataException("keymapping document parsed to null - check the file isn't empty or malformed.");

    /// <summary>
    /// Builds a real KeyMap from an already-parsed KeyMapDocument. Used
    /// by LoadFromJson above, and reused directly by Part 6 when loading
    /// a saved profile's embedded mapping - same conversion logic, no
    /// duplication between the two parts.
    /// </summary>
    public static IKeyMap FromDocument(KeyMapDocument doc)
    {
        var hidPositions = new Dictionary<string, KeyPosition>();
        foreach (var (keyName, rowCol) in doc.KeyHidMap)
        {
            if (rowCol.Length != 2)
            {
                throw new InvalidDataException(
                    $"KeyHidMap entry '{keyName}' must be a 2-element [row, col] array, got {rowCol.Length} elements.");
            }
            hidPositions[keyName.ToUpperInvariant()] = new KeyPosition(rowCol[0], rowCol[1]);
        }

        var controllerBindings = new Dictionary<string, KeyBinding>();
        foreach (var (keyName, entry) in doc.ControllerMap)
        {
            var kind = entry.Kind.Equals("analog", StringComparison.OrdinalIgnoreCase)
                ? InputType.Analog
                : InputType.Digital;
            controllerBindings[keyName.ToUpperInvariant()] = new KeyBinding(entry.Action, kind);
        }

        return new KeyMap(hidPositions, controllerBindings);
    }
}
