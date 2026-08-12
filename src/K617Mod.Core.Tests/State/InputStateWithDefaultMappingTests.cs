using K617Mod.Core.Mapping;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.State;

/// <summary>
/// Wires the real keymapping.default.json into a real InputState.
/// InputState matches action names as plain strings ("STEER_LEFT" etc)
/// with no compile-time link to the JSON file - a typo in either place
/// would silently produce "nothing happens" rather than an error. This
/// is what would actually catch that.
/// </summary>
public class InputStateWithDefaultMappingTests
{
    private static string DefaultJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "keymapping.default.json");

    [Fact]
    public void RealMapping_SteeringKeysAreWiredCorrectly()
    {
        var keyMap = KeyMapLoader.LoadFromFile(DefaultJsonPath);
        var state = new InputState(keyMap);

        state.Update("L", 340); // STEER_RIGHT in the real mapping
        var snap = state.Snapshot();

        Assert.Equal(1.0, snap.Steering, precision: 6);
    }

    [Fact]
    public void RealMapping_TriggersAreWiredCorrectly()
    {
        var keyMap = KeyMapLoader.LoadFromFile(DefaultJsonPath);
        var state = new InputState(keyMap);

        state.Update("I", 340); // RT_ACCELERATE in the real mapping
        state.Update("K", 340); // LT_BRAKE in the real mapping
        var snap = state.Snapshot();

        Assert.Equal(1.0, snap.Accelerate, precision: 6);
        Assert.Equal(1.0, snap.Brake, precision: 6);
    }

    [Fact]
    public void RealMapping_HandbrakeDigitalActionAppearsInSnapshot()
    {
        var keyMap = KeyMapLoader.LoadFromFile(DefaultJsonPath);
        var state = new InputState(keyMap);

        state.Update("SPACE", 340);
        var snap = state.Snapshot();

        Assert.True(snap.DigitalStates.ContainsKey("A_HANDBRAKE"));
        Assert.True(snap.DigitalStates["A_HANDBRAKE"]);
    }
}
