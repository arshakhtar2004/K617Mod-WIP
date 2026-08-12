using K617Mod.Core.Mapping;
using K617Mod.Core.Orchestration;
using K617Mod.Core.Tests.Output;
using Xunit;

namespace K617Mod.Core.Tests.Orchestration;

/// <summary>
/// Tests AppOrchestrator's wiring logic with fakes standing in for every
/// hardware/driver-facing dependency - no real keyboard, ViGEmBus, or
/// Interception driver involved anywhere in this file.
///
/// Timing note: the tick loop runs on a real background thread, so a
/// few tests use a short Thread.Sleep to let a handful of ticks happen
/// before asserting. A fast tick rate (500Hz) keeps that wait small.
/// This is a deliberate, acknowledged trade-off - genuinely testing a
/// background thread's behavior needs some real wall-clock time; the
/// alternative (not testing the tick loop at all) would leave the
/// riskiest part of this class unverified.
/// </summary>
public class AppOrchestratorTests
{
    private const int FastTickRateHz = 500;
    private const int SettleMs = 60; // enough time for several ticks at 500Hz

    private static IKeyMap BuildSampleMap()
    {
        var positions = new Dictionary<string, KeyPosition>
        {
            ["J"] = new KeyPosition(3, 8),
            ["L"] = new KeyPosition(3, 10),
        };
        var bindings = new Dictionary<string, KeyBinding>
        {
            ["J"] = new KeyBinding("STEER_LEFT", InputType.Analog),
            ["L"] = new KeyBinding("STEER_RIGHT", InputType.Analog),
        };
        return new KeyMap(positions, bindings);
    }

    [Fact]
    public void Start_BeginsApplyingTicksToTheVirtualPad()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        Thread.Sleep(SettleMs);
        orchestrator.Stop();

        Assert.True(pad.ApplyCallCount > 0);
        Assert.True(hid.StartCalled);
    }

    [Fact]
    public void RawReportForMappedKey_FlowsThroughToOutput()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        hid.RaiseReport(row: 3, col: 10, depth: 340); // "L" - STEER_RIGHT, full depth
        Thread.Sleep(SettleMs);

        // Assert BEFORE Stop() - Stop() correctly resets the pad (clears
        // LastApplied), so checking after would be testing post-reset
        // state instead of the actual applied snapshot.
        Assert.NotNull(pad.LastApplied);
        Assert.Equal(1.0, pad.LastApplied!.Value.Steering, precision: 6);

        orchestrator.Stop();
    }

    [Fact]
    public void RawReportForUnmappedPosition_IsIgnoredWithoutError()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        hid.RaiseReport(row: 99, col: 99, depth: 340); // not in the sample map at all
        Thread.Sleep(SettleMs);

        Assert.NotNull(pad.LastApplied); // ticks are still happening
        Assert.Equal(0.0, pad.LastApplied!.Value.Steering); // just never moved from zero

        orchestrator.Stop();
    }

    [Fact]
    public void Start_WithSuppressionEnabled_StartsTheSuppressor()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, enableSuppression: true, tickRateHz: FastTickRateHz);

        orchestrator.Start();

        Assert.True(suppressor.StartCalled);
        Assert.True(orchestrator.SuppressionActive);
        Assert.Null(orchestrator.SuppressionError);

        orchestrator.Stop();
    }

    [Fact]
    public void Start_WithSuppressionDisabled_NeverCallsTheSuppressor()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, enableSuppression: false, tickRateHz: FastTickRateHz);

        orchestrator.Start();

        Assert.False(suppressor.StartCalled);
        Assert.False(orchestrator.SuppressionActive);
        Assert.Null(orchestrator.SuppressionError);

        orchestrator.Stop();
    }

    [Fact]
    public void Start_WhenSuppressorThrows_FailsOpenInsteadOfCrashing()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor { ThrowOnStart = new InvalidOperationException("driver not installed") };
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        var exception = Record.Exception(() => orchestrator.Start());
        Thread.Sleep(SettleMs);

        Assert.Null(exception); // Start() itself must not throw - this is the fail-open contract
        Assert.False(orchestrator.SuppressionActive);
        Assert.Equal("driver not installed", orchestrator.SuppressionError);
        Assert.True(pad.ApplyCallCount > 0); // the rest of the pipeline kept running regardless

        orchestrator.Stop();
    }

    [Fact]
    public void Stop_ResetsTheVirtualPad()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        Thread.Sleep(SettleMs);
        orchestrator.Stop();

        Assert.True(pad.WasReset);
    }

    [Fact]
    public void Stop_StopsTheSuppressorIfItWasActive()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        orchestrator.Start();
        orchestrator.Stop();

        Assert.True(suppressor.StopCalled);
    }

    [Fact]
    public void Stop_WithoutStart_DoesNotThrow()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        var exception = Record.Exception(() => orchestrator.Stop());

        Assert.Null(exception);
    }

    [Fact]
    public void IsConnected_ReflectsTheHidSourceState()
    {
        var hid = new FakeHidKeySource();
        var pad = new FakeVirtualPad();
        var suppressor = new FakeKeySuppressor();
        using var orchestrator = new AppOrchestrator(hid, BuildSampleMap(), pad, suppressor, tickRateHz: FastTickRateHz);

        Assert.False(orchestrator.IsConnected);
        orchestrator.Start();
        Assert.True(orchestrator.IsConnected);
        orchestrator.Stop();
        Assert.False(orchestrator.IsConnected);
    }
}
