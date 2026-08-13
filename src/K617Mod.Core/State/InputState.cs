using K617Mod.Core.Mapping;

namespace K617Mod.Core.State;

/// <summary>
/// Tracks the latest normalized depth for every key currently bound to a
/// controller action, and computes steering/accelerate/brake/digital
/// state from it on demand. Thread-safe: Update() is expected to be
/// called from a HID read thread while Snapshot() is called from a
/// separate fixed-rate tick thread.
///
/// Deliberately looks up which physical key drives each analog action
/// through IKeyMap rather than hardcoding key names - unlike the
/// original Python mapper.py, which referenced "J"/"L"/"I"/"K" directly.
/// Remapping steering to different keys via keymapping.json needs no
/// change here.
///
/// Both halves of a profile can now be replaced while running:
/// <see cref="ApplyBindings"/> for the key map, and the ITuningSource for
/// the curves. Between them, applying a profile no longer requires the
/// pipeline to be torn down and rebuilt.
/// </summary>
public sealed class InputState : IInputState
{
    private const string SteerLeftAction = "STEER_LEFT";
    private const string SteerRightAction = "STEER_RIGHT";
    private const string AccelerateAction = "RT_ACCELERATE";
    private const string BrakeAction = "LT_BRAKE";
    private const string LeftStickUpAction = "LS_UP";
    private const string LeftStickDownAction = "LS_DOWN";
    private const string RightStickLeftAction = "RS_LEFT";
    private const string RightStickRightAction = "RS_RIGHT";
    private const string RightStickUpAction = "RS_UP";
    private const string RightStickDownAction = "RS_DOWN";

    private readonly object _lock = new();
    private readonly ITuningSource _tuning;

    /// <summary>
    /// Latest normalized depth per bound key. Case-insensitive: Update()
    /// used to upper-case its argument before looking in here, which
    /// silently ignored any key the map had stored in lower case. The
    /// comparer does that job properly instead.
    /// </summary>
    private readonly Dictionary<string, double> _values =
        new(StringComparer.OrdinalIgnoreCase);

    private KeyBindingSet _bindings;

    /// <param name="tuning">
    /// Where curves and the digital threshold come from - normally the
    /// selected profile's. Omit it and the built-in defaults are used,
    /// which behave exactly as the old fixed constants did.
    /// </param>
    public InputState(IKeyMap keyMap, ITuningSource? tuning = null)
    {
        _tuning = tuning ?? new TuningSource();
        _bindings = KeyBindingSet.FromKeyMap(keyMap);

        foreach (var keyName in _bindings.BoundKeys)
        {
            _values[keyName] = 0.0;
        }
    }

    /// <summary>
    /// Swaps in a different profile's key bindings without interrupting
    /// the pipeline.
    ///
    /// Takes the same lock Update() and Snapshot() already use, rather
    /// than the lock-free volatile swap ITuningSource does. Tuning can
    /// get away without a lock because it is read once per tick and
    /// nothing else touches it; bindings are read together with the
    /// mutable depth values they index into, so the two have to change as
    /// one thing. That lock is taken 64 times a second regardless, and a
    /// binding swap happens when a person clicks something, so the
    /// contention cost is nil.
    ///
    /// Depth readings for keys that stay bound are carried across, so a
    /// key held down through a profile change does not spuriously
    /// release. Keys that are no longer bound are dropped, which is what
    /// stops a stale depth reappearing if that key is rebound later.
    /// </summary>
    public void ApplyBindings(KeyBindingSet bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        lock (_lock)
        {
            var carried = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var keyName in bindings.BoundKeys)
            {
                carried[keyName] = _values.TryGetValue(keyName, out var existing) ? existing : 0.0;
            }

            _values.Clear();
            foreach (var (keyName, value) in carried)
            {
                _values[keyName] = value;
            }

            _bindings = bindings;
        }
    }

    public void Update(string keyName, int rawDepth)
    {
        lock (_lock)
        {
            if (_values.ContainsKey(keyName))
            {
                _values[keyName] = Normalize(rawDepth);
            }
        }
    }

    public ControllerStateSnapshot Snapshot()
    {
        // Read the tuning reference exactly once per tick. If a profile
        // is applied midway through this method, the swap is invisible
        // here - this tick finishes with the tuning it started with, and
        // the next one picks up the new set. That is what stops a single
        // frame being computed from a mixture of two profiles.
        var tuning = _tuning.Current;

        lock (_lock)
        {
            // Same reasoning one level down: read the binding set once so
            // every axis in this snapshot comes from the same profile,
            // even if Apply lands mid-method.
            var bindings = _bindings;

            // Each axis is two opposing keys sharing one curve, combined
            // into a single -1..1 value. Pressing both at once cancels
            // out, exactly as pushing a real stick two ways would.
            var leftStickX = Axis(tuning.CurveFor(CurveAxes.LeftStickX),
                bindings.KeyForAnalogAction(SteerRightAction),
                bindings.KeyForAnalogAction(SteerLeftAction));

            var leftStickY = Axis(tuning.CurveFor(CurveAxes.LeftStickY),
                bindings.KeyForAnalogAction(LeftStickUpAction),
                bindings.KeyForAnalogAction(LeftStickDownAction));

            var rightStickX = Axis(tuning.CurveFor(CurveAxes.RightStickX),
                bindings.KeyForAnalogAction(RightStickRightAction),
                bindings.KeyForAnalogAction(RightStickLeftAction));

            var rightStickY = Axis(tuning.CurveFor(CurveAxes.RightStickY),
                bindings.KeyForAnalogAction(RightStickUpAction),
                bindings.KeyForAnalogAction(RightStickDownAction));

            var accelerate = tuning.Accelerate.Evaluate(GetValue(bindings.KeyForAnalogAction(AccelerateAction)));
            var brake = tuning.Brake.Evaluate(GetValue(bindings.KeyForAnalogAction(BrakeAction)));

            var digitalStates = new Dictionary<string, bool>();
            foreach (var (action, keyName) in bindings.DigitalBindings)
            {
                digitalStates[action] = GetValue(keyName) >= tuning.DigitalPressThreshold;
            }

            return new ControllerStateSnapshot(
                leftStickX, leftStickY, rightStickX, rightStickY,
                accelerate, brake, digitalStates);
        }
    }

    /// <summary>
    /// Combines the two keys of one axis into -1..1. Must be called
    /// while already holding _lock.
    /// </summary>
    private double Axis(ResponseCurve curve, string? positiveKey, string? negativeKey)
    {
        var positive = curve.Evaluate(GetValue(positiveKey));
        var negative = curve.Evaluate(GetValue(negativeKey));
        return Math.Clamp(positive - negative, -1.0, 1.0);
    }

    /// <summary>Must be called while already holding _lock.</summary>
    private double GetValue(string? keyName) =>
        keyName is not null && _values.TryGetValue(keyName, out var v) ? v : 0.0;

    private static double Normalize(int rawDepth)
    {
        var clamped = Math.Clamp(rawDepth, InputTuningConfig.RawDepthMin, InputTuningConfig.RawDepthMax);
        var span = InputTuningConfig.RawDepthMax - InputTuningConfig.RawDepthMin;
        return span == 0 ? 0.0 : (double)(clamped - InputTuningConfig.RawDepthMin) / span;
    }
}
