using K617Mod.Core.Mapping;
using K617Mod.Core.Persistence;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.State;

/// <summary>
/// Proves the pipeline reads its curves and threshold from the selected
/// profile rather than from fixed constants - the whole point of the
/// curve editor writing anything at all.
/// </summary>
public class InputStateProfileTuningTests
{
    private const int FullDepth = InputTuningConfig.RawDepthMax;
    private static int Depth(double fraction) => (int)Math.Round(fraction * InputTuningConfig.RawDepthMax);

    private static IKeyMap BuildMap()
    {
        var document = new KeyMapDocument
        {
            KeyHidMap =
            {
                ["J"] = new[] { 3, 8 },
                ["L"] = new[] { 3, 10 },
                ["I"] = new[] { 2, 8 },
                ["K"] = new[] { 3, 9 },
                ["SPACE"] = new[] { 5, 6 },
            },
            ControllerMap =
            {
                ["J"] = new KeyBindingEntry { Action = "STEER_LEFT", Kind = "analog" },
                ["L"] = new KeyBindingEntry { Action = "STEER_RIGHT", Kind = "analog" },
                ["I"] = new KeyBindingEntry { Action = "RT_ACCELERATE", Kind = "analog" },
                ["K"] = new KeyBindingEntry { Action = "LT_BRAKE", Kind = "analog" },
                ["SPACE"] = new KeyBindingEntry { Action = "A_HANDBRAKE", Kind = "digital" },
            },
        };

        return KeyMapLoader.FromDocument(document);
    }

    [Fact]
    public void WithNoTuningGiven_BehavesLikeTheOldFixedConstants()
    {
        var state = new InputState(BuildMap());
        state.Update("I", Depth(0.5));

        // The old ThrottleBrakeCurveExponent was 2.0: half depth gave a
        // quarter output.
        Assert.Equal(0.25, state.Snapshot().Accelerate, 3);
    }

    [Fact]
    public void AcceleratorFollowsTheProfilesRightTriggerCurve()
    {
        var profile = new ProfileDocument();
        profile.Curves[CurveAxes.RightTrigger] = ResponseCurve.Linear();

        var state = new InputState(BuildMap(), new TuningSource(profile.ToTuning()));
        state.Update("I", Depth(0.5));

        // Linear now, so half depth means half output - not the quarter
        // the old hardcoded exponent produced.
        Assert.Equal(0.5, state.Snapshot().Accelerate, 3);
    }

    [Fact]
    public void BrakeAndAcceleratorCanHaveDifferentCurves()
    {
        var profile = new ProfileDocument();
        profile.Curves[CurveAxes.RightTrigger] = ResponseCurve.Linear();
        profile.Curves[CurveAxes.LeftTrigger] = ResponseCurve.FromExponent(2.0);

        var state = new InputState(BuildMap(), new TuningSource(profile.ToTuning()));
        state.Update("I", Depth(0.5));
        state.Update("K", Depth(0.5));

        var snapshot = state.Snapshot();
        Assert.Equal(0.5, snapshot.Accelerate, 3);
        Assert.Equal(0.25, snapshot.Brake, 3);
    }

    [Fact]
    public void SteeringDeadzoneFromTheProfileIsHonoured()
    {
        var profile = new ProfileDocument();
        profile.Curves[CurveAxes.LeftStickX] = new ResponseCurve(new[]
        {
            new CurvePoint(0.0, 0.0),
            new CurvePoint(0.4, 0.0),
            new CurvePoint(1.0, 1.0),
        });

        var state = new InputState(BuildMap(), new TuningSource(profile.ToTuning()));

        state.Update("L", Depth(0.2));
        Assert.Equal(0.0, state.Snapshot().Steering, 3);

        state.Update("L", FullDepth);
        Assert.Equal(1.0, state.Snapshot().Steering, 3);
    }

    [Fact]
    public void DigitalThresholdComesFromTheProfile()
    {
        var profile = new ProfileDocument { DigitalPressThreshold = 0.8 };
        var state = new InputState(BuildMap(), new TuningSource(profile.ToTuning()));

        state.Update("SPACE", Depth(0.5));
        Assert.False(state.Snapshot().DigitalStates["A_HANDBRAKE"]);

        state.Update("SPACE", Depth(0.9));
        Assert.True(state.Snapshot().DigitalStates["A_HANDBRAKE"]);
    }

    [Fact]
    public void SwappingTuningWhileRunningTakesEffectOnTheNextSnapshot()
    {
        var source = new TuningSource(ProfileTuning.Default);
        var state = new InputState(BuildMap(), source);

        state.Update("I", Depth(0.5));
        Assert.Equal(0.25, state.Snapshot().Accelerate, 3);

        var linear = new ProfileDocument();
        linear.Curves[CurveAxes.RightTrigger] = ResponseCurve.Linear();
        source.Apply(linear.ToTuning());

        // Same key depth, different profile - no restart involved.
        Assert.Equal(0.5, state.Snapshot().Accelerate, 3);
    }

    [Fact]
    public void AProfileMissingAnAxisFallsBackToItsDefaultCurve()
    {
        var profile = new ProfileDocument();
        profile.Curves.Remove(CurveAxes.RightTrigger);

        var state = new InputState(BuildMap(), new TuningSource(profile.ToTuning()));
        state.Update("I", Depth(0.5));

        Assert.Equal(0.25, state.Snapshot().Accelerate, 3);
    }
}
