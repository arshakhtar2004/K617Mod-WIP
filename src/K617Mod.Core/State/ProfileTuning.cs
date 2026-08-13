namespace K617Mod.Core.State;

/// <summary>
/// The tuning half of a profile - response curves and the digital press
/// threshold - in the shape the input pipeline actually consumes.
///
/// Treated as immutable once built. Changing tuning means constructing a
/// new one and swapping it in via <see cref="ITuningSource"/>, never
/// mutating one that a tick might already be reading. That is what makes
/// "apply while running" safe without putting a lock on the hot path.
/// </summary>
public sealed class ProfileTuning
{
    private readonly Dictionary<string, ResponseCurve> _curves;

    public ProfileTuning(IReadOnlyDictionary<string, ResponseCurve>? curves, double digitalPressThreshold)
    {
        _curves = new Dictionary<string, ResponseCurve>(StringComparer.OrdinalIgnoreCase);

        // Start from the defaults so an axis missing from a profile
        // still has a usable curve, rather than the pipeline having to
        // null-check on every tick.
        foreach (var (axisId, curve) in CurveAxes.Defaults())
        {
            _curves[axisId] = curve;
        }

        if (curves is not null)
        {
            foreach (var (axisId, curve) in curves)
            {
                if (curve is null) continue;
                var copy = curve.Clone();
                copy.Normalize();
                _curves[axisId] = copy;
            }
        }

        DigitalPressThreshold = Math.Clamp(digitalPressThreshold, 0.0, 1.0);
    }

    /// <summary>What the app behaved like before curves were editable.</summary>
    public static ProfileTuning Default { get; } = new(null, 0.3);

    /// <summary>Fraction of full depth at which a digital key counts as pressed.</summary>
    public double DigitalPressThreshold { get; }

    public ResponseCurve CurveFor(string axisId) =>
        _curves.TryGetValue(axisId, out var curve) ? curve : ResponseCurve.Linear();

    public ResponseCurve Steering => CurveFor(CurveAxes.LeftStickX);
    public ResponseCurve Accelerate => CurveFor(CurveAxes.RightTrigger);
    public ResponseCurve Brake => CurveFor(CurveAxes.LeftTrigger);
}

/// <summary>
/// Where the input pipeline reads its current tuning from. An interface
/// so InputState never has to know whether tuning is fixed, loaded from
/// a profile, or being changed live by a UI.
/// </summary>
public interface ITuningSource
{
    ProfileTuning Current { get; }
}

/// <summary>
/// A tuning source whose value can be replaced while the pipeline runs.
///
/// The swap is a single reference assignment to a volatile field, not a
/// lock. A tick reads the reference once and then works entirely with
/// that snapshot, so it either sees the whole old tuning or the whole
/// new one - never a half-applied mixture where, say, the brake curve
/// had updated but the throttle curve had not. Locking here would mean
/// taking a lock 64 times a second on the audio-rate path to protect
/// something that changes when a person clicks Apply.
/// </summary>
public sealed class TuningSource : ITuningSource
{
    private volatile ProfileTuning _current;

    public TuningSource(ProfileTuning? initial = null) => _current = initial ?? ProfileTuning.Default;

    public ProfileTuning Current => _current;

    public void Apply(ProfileTuning tuning) => _current = tuning ?? ProfileTuning.Default;
}
