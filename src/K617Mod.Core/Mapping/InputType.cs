namespace K617Mod.Core.Mapping;

/// <summary>
/// Whether a controller action reads live analog depth (steering,
/// triggers) or a simple press/release threshold (buttons, D-Pad).
/// Mirrors controller_mapping.py's InputType exactly.
/// </summary>
public enum InputType
{
    Analog,
    Digital
}
