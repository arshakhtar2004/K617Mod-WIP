namespace K617Mod.Core.Mapping;

/// <summary>
/// Contract for looking up key positions and controller bindings. The
/// Input State module (Part 3) will depend only on this interface, not
/// on KeyMap directly - same independence pattern as Part 1's
/// IHidKeySource. Lets the mapping source change (hand-built, JSON file,
/// future in-app profile editor) without touching anything downstream.
/// </summary>
public interface IKeyMap
{
    /// <summary>Every physical key name currently bound to a controller action.</summary>
    IReadOnlyCollection<string> BoundKeys { get; }

    /// <summary>Raw HID grid position for a key name, or null if unknown.</summary>
    KeyPosition? GetHidPosition(string keyName);

    /// <summary>Controller binding for a key name, or null if that key isn't bound to anything right now.</summary>
    KeyBinding? GetControllerAction(string keyName);

    /// <summary>Reverse lookup: raw HID position -> key name, or null if unmapped.</summary>
    string? FindKeyByPosition(KeyPosition position);

    bool IsAnalog(string keyName);
    bool IsDigital(string keyName);
}
