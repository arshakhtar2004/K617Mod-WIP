namespace K617Mod.Core.Hid;

/// <summary>
/// One parsed HID report from the K617 HE's analog interface, before any
/// key-name or controller-action mapping is applied. This module knows
/// nothing about "W" or "steering" - only raw (row, col) grid positions
/// and depth counts. Semantic mapping is deliberately a separate
/// downstream module's job, so this one stays independent of it.
/// </summary>
/// <param name="Row">Raw key grid row, from byte[6].</param>
/// <param name="Col">Raw key grid column, from byte[7].</param>
/// <param name="Depth">
/// Raw little-endian depth reading from bytes[8-9]. Normal operating
/// range is ~0-340 counts; already sanity-checked against
/// HidProtocolConfig.RawDepthSanityMax before this is ever raised.
/// </param>
/// <param name="Mode">Live or Summary - see ReportMode.</param>
/// <param name="Timestamp">
/// Local time the report was parsed. Not used by the control pipeline
/// itself - carried along for diagnostics and for a future HID
/// recorder/replay tool, so recorded sessions preserve real timing.
/// </param>
public readonly record struct RawKeyReport(int Row, int Col, int Depth, ReportMode Mode, DateTime Timestamp);
