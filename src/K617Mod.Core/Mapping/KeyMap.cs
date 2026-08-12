namespace K617Mod.Core.Mapping;

/// <summary>
/// Pure, in-memory implementation of IKeyMap. Takes its two lookup
/// tables as plain dictionaries in the constructor and does nothing but
/// answer questions about them - no file I/O, no JSON, no knowledge of
/// where the data came from. That's what makes it trivially unit-testable
/// with hand-written dictionaries and keeps it independent of
/// KeyMapLoader (which owns the JSON-reading concern separately).
/// </summary>
public sealed class KeyMap : IKeyMap
{
    private readonly IReadOnlyDictionary<string, KeyPosition> _hidPositions;
    private readonly IReadOnlyDictionary<string, KeyBinding> _controllerBindings;

    public KeyMap(
        IReadOnlyDictionary<string, KeyPosition> hidPositions,
        IReadOnlyDictionary<string, KeyBinding> controllerBindings)
    {
        _hidPositions = hidPositions;
        _controllerBindings = controllerBindings;
    }

    public IReadOnlyCollection<string> BoundKeys => _controllerBindings.Keys.ToList();

    public KeyPosition? GetHidPosition(string keyName) =>
        _hidPositions.TryGetValue(keyName.ToUpperInvariant(), out var pos) ? pos : null;

    public KeyBinding? GetControllerAction(string keyName) =>
        _controllerBindings.TryGetValue(keyName.ToUpperInvariant(), out var binding) ? binding : null;

    public string? FindKeyByPosition(KeyPosition position)
    {
        foreach (var (keyName, pos) in _hidPositions)
        {
            if (pos == position) return keyName;
        }
        return null;
    }

    public bool IsAnalog(string keyName) => GetControllerAction(keyName)?.Kind == InputType.Analog;

    public bool IsDigital(string keyName) => GetControllerAction(keyName)?.Kind == InputType.Digital;
}
