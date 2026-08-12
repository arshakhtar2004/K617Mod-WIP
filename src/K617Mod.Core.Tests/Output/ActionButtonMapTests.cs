using K617Mod.Core.Mapping;
using K617Mod.Core.Output;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.Output;

public class ActionButtonMapTests
{
    // Derived from XboxControls rather than written out by hand.
    //
    // This list used to be a hardcoded 13 names, and it went stale the
    // moment R3 and Guide were added - the test failed on a count, which
    // says nothing about what actually went wrong. Deriving it means the
    // test now states the real rule: every digital control the pad
    // exposes must have a button behind it, or binding a key to it does
    // nothing. Add a control to XboxControls without wiring a button and
    // this fails immediately, naming the culprit.
    private static readonly string[] ExpectedActions =
        XboxControls.All
            .Where(control => control.Kind == InputType.Digital)
            .Select(control => control.ActionId)
            .ToArray();

    [Fact]
    public void EveryExpectedAction_HasAMappedButton()
    {
        foreach (var action in ExpectedActions)
        {
            Assert.True(ActionButtonMap.TryGetButton(action, out _), $"'{action}' should have a mapped button.");
        }
    }

    [Fact]
    public void MappedActions_CountMatchesExpected()
    {
        Assert.Equal(ExpectedActions.Length, ActionButtonMap.MappedActions.Count);
    }

    [Fact]
    public void EveryDigitalControlOnThePad_HasAButton()
    {
        var missing = XboxControls.All
            .Where(control => control.Kind == InputType.Digital)
            .Where(control => !ActionButtonMap.TryGetButton(control.ActionId, out _))
            .Select(control => $"{control.DisplayName} ({control.ActionId})")
            .ToList();

        Assert.True(missing.Count == 0,
            "These can be bound in the UI but would silently do nothing: " + string.Join(", ", missing));
    }

    [Fact]
    public void UnknownAction_ReturnsFalse()
    {
        Assert.False(ActionButtonMap.TryGetButton("NOT_A_REAL_ACTION", out _));
    }

    [Fact]
    public void AnalogActionNames_AreNotInTheButtonMap()
    {
        // STEER_LEFT etc. are handled separately via the snapshot's
        // Steering/Accelerate/Brake fields - they should never appear
        // here, since that would mean double-handling the same input.
        Assert.False(ActionButtonMap.TryGetButton("STEER_LEFT", out _));
        Assert.False(ActionButtonMap.TryGetButton("STEER_RIGHT", out _));
        Assert.False(ActionButtonMap.TryGetButton("RT_ACCELERATE", out _));
        Assert.False(ActionButtonMap.TryGetButton("LT_BRAKE", out _));
    }

    [Fact]
    public void AllButtons_HasNoFewerEntriesThanMappedActions()
    {
        // Not asserting exact equality here since it'd require comparing
        // Xbox360Button values directly, which this project hasn't
        // needed to rely on being a simple equatable enum. Count parity
        // is enough to catch an accidental missing/duplicate mapping.
        Assert.Equal(ActionButtonMap.MappedActions.Count, ActionButtonMap.AllButtons.Count);
    }
}

public class FakeVirtualPadTests
{
    [Fact]
    public void Apply_RecordsTheSnapshot()
    {
        var pad = new FakeVirtualPad();
        var snapshot = new ControllerStateSnapshot(0.5, 0.25, 0.75, new Dictionary<string, bool>());

        pad.Apply(snapshot);

        Assert.Equal(snapshot, pad.LastApplied);
        Assert.Equal(1, pad.ApplyCallCount);
    }

    [Fact]
    public void Apply_CalledTwice_CountsBoth_KeepsLatest()
    {
        var pad = new FakeVirtualPad();
        var first = new ControllerStateSnapshot(0.1, 0.0, 0.0, new Dictionary<string, bool>());
        var second = new ControllerStateSnapshot(0.9, 0.0, 0.0, new Dictionary<string, bool>());

        pad.Apply(first);
        pad.Apply(second);

        Assert.Equal(2, pad.ApplyCallCount);
        Assert.Equal(second, pad.LastApplied);
    }

    [Fact]
    public void Reset_ClearsLastAppliedAndSetsFlag()
    {
        var pad = new FakeVirtualPad();
        pad.Apply(new ControllerStateSnapshot(1.0, 1.0, 1.0, new Dictionary<string, bool>()));

        pad.Reset();

        Assert.True(pad.WasReset);
        Assert.Null(pad.LastApplied);
    }
}
