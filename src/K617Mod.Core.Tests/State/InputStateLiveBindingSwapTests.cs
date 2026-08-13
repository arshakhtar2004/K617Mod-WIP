using K617Mod.Core.Mapping;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.State;

/// <summary>
/// Covers applying a different profile's key bindings to a running
/// InputState. Before this existed a remap needed the whole pipeline
/// rebuilt, so these tests are the guarantee that the cheaper path is
/// actually equivalent - not just faster.
/// </summary>
public class InputStateLiveBindingSwapTests
{
    /// <summary>J/L steer, I accelerates, SPACE is a digital button.</summary>
    private static IKeyMap RacingMap() => new KeyMap(
        new Dictionary<string, KeyPosition>
        {
            ["J"] = new KeyPosition(3, 8),
            ["L"] = new KeyPosition(3, 10),
            ["I"] = new KeyPosition(2, 8),
            ["SPACE"] = new KeyPosition(5, 6),
        },
        new Dictionary<string, KeyBinding>
        {
            ["J"] = new KeyBinding("STEER_LEFT", InputType.Analog),
            ["L"] = new KeyBinding("STEER_RIGHT", InputType.Analog),
            ["I"] = new KeyBinding("RT_ACCELERATE", InputType.Analog),
            ["SPACE"] = new KeyBinding("A_HANDBRAKE", InputType.Digital),
        });

    /// <summary>The same controls on different keys: A/D steer, W accelerates.</summary>
    private static IKeyMap WasdMap() => new KeyMap(
        new Dictionary<string, KeyPosition>
        {
            ["A"] = new KeyPosition(3, 2),
            ["D"] = new KeyPosition(3, 4),
            ["W"] = new KeyPosition(2, 3),
            ["SPACE"] = new KeyPosition(5, 6),
        },
        new Dictionary<string, KeyBinding>
        {
            ["A"] = new KeyBinding("STEER_LEFT", InputType.Analog),
            ["D"] = new KeyBinding("STEER_RIGHT", InputType.Analog),
            ["W"] = new KeyBinding("RT_ACCELERATE", InputType.Analog),
            ["SPACE"] = new KeyBinding("A_HANDBRAKE", InputType.Digital),
        });

    [Fact]
    public void AfterSwap_TheNewKeysDriveTheControls()
    {
        var state = new InputState(RacingMap());

        state.ApplyBindings(KeyBindingSet.FromKeyMap(WasdMap()));
        state.Update("D", 340);

        Assert.Equal(1.0, state.Snapshot().Steering, precision: 6);
    }

    [Fact]
    public void AfterSwap_TheOldKeysDoNothing()
    {
        var state = new InputState(RacingMap());

        state.ApplyBindings(KeyBindingSet.FromKeyMap(WasdMap()));
        state.Update("L", 340); // steered right under the old profile

        Assert.Equal(0.0, state.Snapshot().Steering, precision: 6);
    }

    [Fact]
    public void SwapDoesNotStrandAKeyThatWasHeldDown()
    {
        // SPACE is bound in both maps. Someone holding it through a
        // profile change should still be holding it afterwards - a
        // handbrake that silently releases mid-corner is exactly the
        // kind of failure that would be blamed on the game.
        var state = new InputState(RacingMap());
        state.Update("SPACE", 340);

        state.ApplyBindings(KeyBindingSet.FromKeyMap(WasdMap()));

        Assert.True(state.Snapshot().DigitalStates["A_HANDBRAKE"]);
    }

    [Fact]
    public void ADepthFromAnUnboundKeyDoesNotComeBackWhenItIsReboundLater()
    {
        // Press L (steer right) under the racing map, switch to a map
        // where L means nothing, then switch back. L should read as
        // released, because nothing has told us it is still down.
        var state = new InputState(RacingMap());
        state.Update("L", 340);

        state.ApplyBindings(KeyBindingSet.FromKeyMap(WasdMap()));
        state.ApplyBindings(KeyBindingSet.FromKeyMap(RacingMap()));

        Assert.Equal(0.0, state.Snapshot().Steering, precision: 6);
    }

    [Fact]
    public void SwappingToAProfileWithNothingBound_CentresEverything()
    {
        var state = new InputState(RacingMap());
        state.Update("L", 340);
        state.Update("I", 340);

        state.ApplyBindings(KeyBindingSet.Empty);
        var snap = state.Snapshot();

        Assert.Equal(0.0, snap.Steering, precision: 6);
        Assert.Equal(0.0, snap.Accelerate, precision: 6);
        Assert.Empty(snap.DigitalStates);
    }

    [Fact]
    public void DigitalActionsFromTheOldProfileDisappearFromTheSnapshot()
    {
        var noButtons = new KeyMap(
            new Dictionary<string, KeyPosition> { ["J"] = new KeyPosition(3, 8) },
            new Dictionary<string, KeyBinding> { ["J"] = new KeyBinding("STEER_LEFT", InputType.Analog) });

        var state = new InputState(RacingMap());
        state.ApplyBindings(KeyBindingSet.FromKeyMap(noButtons));

        Assert.False(state.Snapshot().DigitalStates.ContainsKey("A_HANDBRAKE"));
    }

    [Fact]
    public void ApplyingNullBindings_Throws()
    {
        var state = new InputState(RacingMap());
        Assert.Throws<ArgumentNullException>(() => state.ApplyBindings(null!));
    }

    [Fact]
    public void SwapIsSafeWhileSnapshotsAreBeingTaken()
    {
        // The point of the lock. If bindings and depth values could be
        // seen half-updated, this loop would eventually throw or produce
        // a value outside the legal -1..1 range.
        var state = new InputState(RacingMap());
        var maps = new[] { RacingMap(), WasdMap() };
        var stop = false;
        Exception? failure = null;

        var reader = new Thread(() =>
        {
            try
            {
                while (!Volatile.Read(ref stop))
                {
                    var snap = state.Snapshot();
                    Assert.InRange(snap.Steering, -1.0, 1.0);
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        reader.Start();
        for (var i = 0; i < 2000; i++)
        {
            state.ApplyBindings(KeyBindingSet.FromKeyMap(maps[i % 2]));
            state.Update(i % 2 == 0 ? "L" : "D", 200);
        }

        Volatile.Write(ref stop, true);
        reader.Join(TimeSpan.FromSeconds(5));

        Assert.Null(failure);
    }
}
