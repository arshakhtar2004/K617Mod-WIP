using K617Mod.Core.Mapping;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.State;

public class InputStateTests
{
    // Small hand-built map: J/L steer, I/K trigger, SPACE is digital -
    // enough to exercise every code path without pulling in the real
    // 17-key mapping or its JSON file.
    private static IKeyMap BuildSampleMap()
    {
        var positions = new Dictionary<string, KeyPosition>
        {
            ["J"] = new KeyPosition(3, 8),
            ["L"] = new KeyPosition(3, 10),
            ["I"] = new KeyPosition(2, 8),
            ["K"] = new KeyPosition(3, 9),
            ["SPACE"] = new KeyPosition(5, 6),
        };

        var bindings = new Dictionary<string, KeyBinding>
        {
            ["J"] = new KeyBinding("STEER_LEFT", InputType.Analog),
            ["L"] = new KeyBinding("STEER_RIGHT", InputType.Analog),
            ["I"] = new KeyBinding("RT_ACCELERATE", InputType.Analog),
            ["K"] = new KeyBinding("LT_BRAKE", InputType.Analog),
            ["SPACE"] = new KeyBinding("A_HANDBRAKE", InputType.Digital),
        };

        return new KeyMap(positions, bindings);
    }

    [Fact]
    public void NoInput_EverythingIsZeroOrCentered()
    {
        var state = new InputState(BuildSampleMap());
        var snap = state.Snapshot();

        Assert.Equal(0.0, snap.Steering);
        Assert.Equal(0.0, snap.Accelerate);
        Assert.Equal(0.0, snap.Brake);
        Assert.False(snap.DigitalStates["A_HANDBRAKE"]);
    }

    [Fact]
    public void FullRightSteer_ProducesPositiveOne()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("L", 340); // fully bottomed out
        var snap = state.Snapshot();

        Assert.Equal(1.0, snap.Steering, precision: 6);
    }

    [Fact]
    public void FullLeftSteer_ProducesNegativeOne()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("J", 340);
        var snap = state.Snapshot();

        Assert.Equal(-1.0, snap.Steering, precision: 6);
    }

    [Fact]
    public void EqualLeftAndRight_CancelsToZero()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("J", 200);
        state.Update("L", 200);
        var snap = state.Snapshot();

        Assert.Equal(0.0, snap.Steering, precision: 6);
    }

    [Fact]
    public void SteeringIsLinear_NoCurveApplied()
    {
        // SteeringCurveExponent is 1.0 - half depth should mean exactly
        // half axis value, unlike throttle/brake.
        var state = new InputState(BuildSampleMap());
        state.Update("L", 170); // exactly half of RawDepthMax (340)
        var snap = state.Snapshot();

        Assert.Equal(0.5, snap.Steering, precision: 3);
    }

    [Fact]
    public void Throttle_HalfDepth_ProducesQuarterOutput()
    {
        // ThrottleBrakeCurveExponent is 2.0 - 50% depth -> 25% output,
        // per config.py's documented intent.
        var state = new InputState(BuildSampleMap());
        state.Update("I", 170); // half of 340
        var snap = state.Snapshot();

        Assert.Equal(0.25, snap.Accelerate, precision: 3);
    }

    [Fact]
    public void Throttle_FullDepth_ProducesFullOutput()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("I", 340);
        var snap = state.Snapshot();

        Assert.Equal(1.0, snap.Accelerate, precision: 6);
    }

    [Fact]
    public void Brake_FollowsSameCurveAsThrottle()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("K", 170);
        var snap = state.Snapshot();

        Assert.Equal(0.25, snap.Brake, precision: 3);
    }

    [Fact]
    public void DepthAboveMax_IsClampedNotOverdriven()
    {
        // Guards against a corrupted/garbage-but-still-under-sanity-limit
        // reading producing an out-of-range axis value.
        var state = new InputState(BuildSampleMap());
        state.Update("I", 999);
        var snap = state.Snapshot();

        Assert.Equal(1.0, snap.Accelerate, precision: 6);
    }

    [Fact]
    public void NegativeDepth_IsClampedToZero()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("I", -50);
        var snap = state.Snapshot();

        Assert.Equal(0.0, snap.Accelerate, precision: 6);
    }

    [Fact]
    public void DigitalKey_BelowThreshold_IsNotPressed()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("SPACE", 90); // ~26% of 340, below the 30% threshold
        var snap = state.Snapshot();

        Assert.False(snap.DigitalStates["A_HANDBRAKE"]);
    }

    [Fact]
    public void DigitalKey_AboveThreshold_IsPressed()
    {
        var state = new InputState(BuildSampleMap());
        state.Update("SPACE", 110); // ~32% of 340, above the 30% threshold
        var snap = state.Snapshot();

        Assert.True(snap.DigitalStates["A_HANDBRAKE"]);
    }

    [Fact]
    public void UnboundKeyUpdate_IsSilentlyIgnored()
    {
        // "Q" isn't in this sample map at all - mirrors a key that
        // exists on the keyboard but isn't currently bound to anything.
        var state = new InputState(BuildSampleMap());
        state.Update("Q", 340);
        var snap = state.Snapshot();

        // Nothing should have changed or thrown.
        Assert.Equal(0.0, snap.Steering);
        Assert.Equal(0.0, snap.Accelerate);
    }

    [Fact]
    public void RemappingActionToDifferentKey_MapperFollowsWithoutCodeChange()
    {
        // Proves the "look up by action name" design: if STEER_LEFT is
        // bound to a totally different physical key, InputState should
        // still find it correctly with zero changes to InputState itself.
        var positions = new Dictionary<string, KeyPosition>
        {
            ["Z"] = new KeyPosition(4, 2),
        };
        var bindings = new Dictionary<string, KeyBinding>
        {
            ["Z"] = new KeyBinding("STEER_LEFT", InputType.Analog),
        };
        var remappedKeyMap = new KeyMap(positions, bindings);

        var state = new InputState(remappedKeyMap);
        state.Update("Z", 340);
        var snap = state.Snapshot();

        Assert.Equal(-1.0, snap.Steering, precision: 6);
    }
}
