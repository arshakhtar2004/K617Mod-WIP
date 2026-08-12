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

    private readonly object _lock = new();
    private readonly Dictionary<string, double> _values;
    private readonly List<(string Action, string KeyName)> _digitalBindings;

    private readonly string? _steerLeftKey;
    private readonly string? _steerRightKey;
    private readonly string? _accelerateKey;
    private readonly string? _brakeKey;

    public InputState(IKeyMap keyMap)
    {
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
        lock (_lock)
        {
            var left = ApplyCurve(GetValue(_steerLeftKey), InputTuningConfig.SteeringCurveExponent);
            var right = ApplyCurve(GetValue(_steerRightKey), InputTuningConfig.SteeringCurveExponent);
            var steering = Math.Clamp(right - left, -1.0, 1.0);

            var accelerate = ApplyCurve(GetValue(_accelerateKey), InputTuningConfig.ThrottleBrakeCurveExponent);
            var brake = ApplyCurve(GetValue(_brakeKey), InputTuningConfig.ThrottleBrakeCurveExponent);

            var digitalStates = new Dictionary<string, bool>();
            foreach (var (action, keyName) in _digitalBindings)
            {
                digitalStates[action] = GetValue(keyName) >= InputTuningConfig.DigitalPressThreshold;
            }

            return new ControllerStateSnapshot(steering, accelerate, brake, digitalStates);
        }
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

    private static double ApplyCurve(double value, double exponent) => Math.Pow(value, exponent);
}
