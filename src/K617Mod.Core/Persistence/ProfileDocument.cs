using K617Mod.Core.Mapping;

namespace K617Mod.Core.Persistence;

/// <summary>
/// Wire format for one saved profile: a named bundle of "how keys
/// behave" (the tuning numbers that were fixed constants in Part 3's
/// InputTuningConfig, now overridable per profile) plus "what keys do"
/// (reusing Part 2's KeyMapDocument directly, rather than re-inventing
/// the same shape a second time).
///
/// This is what turns "Typing" vs "FH6" vs a future "iRacing" from a
/// single hardcoded config into genuinely different, independently
/// saved setups.
/// </summary>
public sealed class ProfileDocument
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>
    /// True for the shipped "Default" profile, which is the known-good
    /// baseline the four editable slots are copied from. The app refuses
    /// to edit it, so there is always something intact to fall back to
    /// after breaking one of the others.
    /// </summary>
    public bool IsReadOnly { get; set; }

    public double SteeringCurveExponent { get; set; } = 1.0;
    public double ThrottleBrakeCurveExponent { get; set; } = 2.0;
    public double DigitalPressThreshold { get; set; } = 0.3;

    public KeyMapDocument KeyMapping { get; set; } = new();
}
