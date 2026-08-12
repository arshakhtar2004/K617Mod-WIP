namespace K617Mod.Core.Mapping;

/// <summary>
/// Wire format for keymapping.json. Deliberately separate from KeyMap's
/// own types - if the JSON schema ever needs to change (versioning, new
/// fields), only this file and KeyMapLoader need to know about it.
/// </summary>
public sealed class KeyMapDocument
{
    /// <summary>"W": [2, 2] - key name to [row, col].</summary>
    public Dictionary<string, int[]> KeyHidMap { get; set; } = new();

    /// <summary>"W": { "action": "DPAD_UP", "kind": "digital" }</summary>
    public Dictionary<string, KeyBindingEntry> ControllerMap { get; set; } = new();

    /// <summary>Keys with a known HID position but no controller action yet - carried through for future profile editing, not used in lookups.</summary>
    public List<string> ReservedSpareKeys { get; set; } = new();
}

public sealed class KeyBindingEntry
{
    public string Action { get; set; } = "";

    /// <summary>"analog" or "digital", case-insensitive.</summary>
    public string Kind { get; set; } = "digital";
}
