namespace K617Mod.Core.Mapping;

/// <summary>
/// What a physical key does on the virtual controller: which action name
/// (e.g. "STEER_LEFT", "A_HANDBRAKE") and whether it's read as analog
/// depth or a digital press/release. Action names are plain strings
/// rather than an enum on purpose - matches controller_mapping.py's
/// approach and means new actions can be added via JSON alone, with no
/// recompile, later.
/// </summary>
public readonly record struct KeyBinding(string Action, InputType Kind);
