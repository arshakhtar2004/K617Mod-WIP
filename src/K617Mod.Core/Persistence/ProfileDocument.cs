using K617Mod.Core.Mapping;
using K617Mod.Core.State;

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

    /// <summary>
    /// Response curve per axis, keyed by CurveAxes ids ("RT", "LS_X"…).
    /// Replaced the old SteeringCurveExponent / ThrottleBrakeCurveExponent
    /// pair: an exponent can only bow the line one way, where a point
    /// list can also express deadzones and S-curves.
    ///
    /// A dictionary rather than six named properties so adding an axis
    /// later is a data change, not a schema change. A profile missing an
    /// entry falls back to the default for that axis rather than failing.
    /// </summary>
    public Dictionary<string, ResponseCurve> Curves { get; set; } = CurveAxes.Defaults();

    public double DigitalPressThreshold { get; set; } = 0.3;

    public KeyMapDocument KeyMapping { get; set; } = new();

    /// <summary>
    /// This profile's tuning in the shape the input pipeline consumes.
    ///
    /// Lives here rather than in State so the dependency runs one way:
    /// Persistence knows about State, State knows nothing about
    /// Persistence. InputState can therefore be tested with tuning built
    /// by hand, with no profile or file system involved at all.
    /// </summary>
    public ProfileTuning ToTuning() => new(Curves, DigitalPressThreshold);
}
