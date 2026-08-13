using K617Mod.Core.Mapping;
using K617Mod.Core.Orchestration;
using K617Mod.Core.State;
using K617Mod.Core.Tests.Output;
using Xunit;

namespace K617Mod.Core.Tests.Orchestration;

/// <summary>
/// Covers applying a profile to a pipeline that is already running -
/// both halves of it. The key map goes through AppOrchestrator, because
/// the HID-position lookup lives there; the curves go through the
/// ITuningSource the orchestrator was built with.
///
/// Same timing caveat as AppOrchestratorTests: the tick loop is a real
/// background thread, so these wait a few milliseconds before asserting.
/// </summary>
public class AppOrchestratorProfileSwapTests
{
    private const int FastTickRateHz = 500;
    private const int SettleMs = 60;

    /// <summary>Position (3,10) is "L", and L steers right.</summary>
    private static IKeyMap MapWithLAtPosition() => new KeyMap(
        new Dictionary<string, KeyPosition> { ["L"] = new KeyPosition(3, 10) },
        new Dictionary<string, KeyBinding> { ["L"] = new KeyBinding("STEER_RIGHT", InputType.Analog) });

    /// <summary>The same physical position is now called "D", and D steers LEFT.</summary>
    private static IKeyMap MapWithDAtSamePosition() => new KeyMap(
        new Dictionary<string, KeyPosition> { ["D"] = new KeyPosition(3, 10) },
        new Dictionary<string, KeyBinding> { ["D"] = new KeyBinding("STEER_LEFT", InputType.Analog) });

    [Fact]
    public void ApplyKeyMap_ChangesWhatAKeyDoes_WithoutStopping()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(
            hid, MapWithLAtPosition(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        hid.RaiseReport(3, 10, 340);
        Thread.Sleep(SettleMs);
        Assert.Equal(1.0, pad.LastApplied!.Value.Steering, precision: 6);

        orchestrator.ApplyKeyMap(MapWithDAtSamePosition());
        hid.RaiseReport(3, 10, 340);
        Thread.Sleep(SettleMs);

        // Same physical key, opposite direction - and the pad never
        // stopped receiving ticks in between.
        Assert.Equal(-1.0, pad.LastApplied!.Value.Steering, precision: 6);
        Assert.False(hid.StopCalled);
        Assert.False(pad.WasReset);
        Assert.False(suppressor.StopCalled);

        orchestrator.Stop();
    }

    [Fact]
    public void ApplyTuning_ChangesTheCurveTheRunningPipelineUses()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();

        // Linear steering to begin with: half depth gives half output.
        var tuning = new TuningSource(new ProfileTuning(
            new Dictionary<string, ResponseCurve> { [CurveAxes.LeftStickX] = ResponseCurve.Linear() },
            digitalPressThreshold: 0.3));

        using var orchestrator = new AppOrchestrator(
            hid, MapWithLAtPosition(), pad, suppressor, tuning, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        hid.RaiseReport(3, 10, 170); // half of the 0-340 range
        Thread.Sleep(SettleMs);
        Assert.Equal(0.5, pad.LastApplied!.Value.Steering, precision: 2);

        // Square-law curve: the same half-pressed key should now read
        // roughly a quarter.
        tuning.Apply(new ProfileTuning(
            new Dictionary<string, ResponseCurve> { [CurveAxes.LeftStickX] = ResponseCurve.FromExponent(2.0) },
            digitalPressThreshold: 0.3));

        Thread.Sleep(SettleMs);

        // No new HID report was sent - the change alone is enough,
        // which is the whole point of tuning being read every tick.
        Assert.Equal(0.25, pad.LastApplied!.Value.Steering, precision: 2);

        orchestrator.Stop();
    }

    [Fact]
    public void ApplyKeyMap_BeforeStart_IsHonouredOnceStarted()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(
            hid, MapWithLAtPosition(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.ApplyKeyMap(MapWithDAtSamePosition());
        orchestrator.Start();
        hid.RaiseReport(3, 10, 340);
        Thread.Sleep(SettleMs);

        Assert.Equal(-1.0, pad.LastApplied!.Value.Steering, precision: 6);

        orchestrator.Stop();
    }

    [Fact]
    public void ApplyKeyMap_WithNull_Throws()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, MapWithLAtPosition(), pad, suppressor);

        Assert.Throws<ArgumentNullException>(() => orchestrator.ApplyKeyMap(null!));
    }
}
