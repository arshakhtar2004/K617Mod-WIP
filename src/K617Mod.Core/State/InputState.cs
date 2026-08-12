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
    private readonly Dictionary<string, double> _values;
    private readonly List<(string Action, string KeyName)> _digitalBindings;
    private readonly ITuningSource _tuning;

    private readonly string? _steerLeftKey;
    private readonly string? _steerRightKey;
    private readonly string? _accelerateKey;
    private readonly string? _brakeKey;
    private readonly string? _leftStickUpKey;
    private readonly string? _leftStickDownKey;
    private readonly string? _rightStickLeftKey;
    private readonly string? _rightStickRightKey;
    private readonly string? _rightStickUpKey;
    private readonly string? _rightStickDownKey;

    /// <param name="tuning">
    /// Where curves and the digital threshold come from - normally the
    /// selected profile's. Omit it and the built-in defaults are used,
    /// which behave exactly as the old fixed constants did.
    /// </param>
    public InputState(IKeyMap keyMap, ITuningSource? tuning = null)
    {
        _tuning = tuning ?? new TuningSource();

        _values = keyMap.BoundKeys.ToDictionary(k => k, _ => 0.0);
        _digitalBindings = new List<(string, string)>();

        foreach (var keyName in keyMap.BoundKeys)
        {
            var binding = keyMap.GetControllerAction(keyName);
            if (binding is null) continue;

            if (binding.Value.Kind == InputType.Digital)
            {
                _digitalBindings.Add((binding.Value.Action, keyName));
                continue;
            }

            switch (binding.Value.Action)
            {
                case SteerLeftAction: _steerLeftKey = keyName; break;
                case SteerRightAction: _steerRightKey = keyName; break;
                case AccelerateAction: _accelerateKey = keyName; break;
                case BrakeAction: _brakeKey = keyName; break;
                case LeftStickUpAction: _leftStickUpKey = keyName; break;
                case LeftStickDownAction: _leftStickDownKey = keyName; break;
                case RightStickLeftAction: _rightStickLeftKey = keyName; break;
                case RightStickRightAction: _rightStickRightKey = keyName; break;
                case RightStickUpAction: _rightStickUpKey = keyName; break;
                case RightStickDownAction: _rightStickDownKey = keyName; break;
            }
        }
    }

    public void Update(string keyName, int rawDepth)
    {
        var upper = keyName.ToUpperInvariant();
        lock (_lock)
        {
            if (_values.ContainsKey(upper))
            {
                _values[upper] = Normalize(rawDepth);
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
            // Each axis is two opposing keys sharing one curve, combined
            // into a single -1..1 value. Pressing both at once cancels
            // out, exactly as pushing a real stick two ways would.
            var leftStickX = Axis(tuning.CurveFor(CurveAxes.LeftStickX), _steerRightKey, _steerLeftKey);
            var leftStickY = Axis(tuning.CurveFor(CurveAxes.LeftStickY), _leftStickUpKey, _leftStickDownKey);
            var rightStickX = Axis(tuning.CurveFor(CurveAxes.RightStickX), _rightStickRightKey, _rightStickLeftKey);
            var rightStickY = Axis(tuning.CurveFor(CurveAxes.RightStickY), _rightStickUpKey, _rightStickDownKey);

            var accelerate = tuning.Accelerate.Evaluate(GetValue(_accelerateKey));
            var brake = tuning.Brake.Evaluate(GetValue(_brakeKey));

            var digitalStates = new Dictionary<string, bool>();
            foreach (var (action, keyName) in _digitalBindings)
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
