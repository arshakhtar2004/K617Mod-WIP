namespace K617Mod.Core.Mapping;

/// <summary>
/// A physical key's raw (row, column) grid position, as reported by the
/// HID interface module. Deliberately the same shape of information as
/// Hid.RawKeyReport's Row/Col, but this module doesn't reference the Hid
/// namespace at all - it only knows two integers, kept that way so this
/// module stays testable without the Hid module ever being involved.
/// </summary>
public readonly record struct KeyPosition(int Row, int Col);
