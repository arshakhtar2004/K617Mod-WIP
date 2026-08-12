using K617Mod.Core.Mapping;
using K617Mod.Core.Output;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.State;

/// <summary>
/// Covers the axes that existed as mappable controls but drove nothing
/// until the snapshot gained fields for them: left stick vertical and
/// the whole right stick.
/// </summary>
public class InputStateSticksTests
{
    private static int Depth(double fraction) => (int)Math.Round(fraction * InputTuningConfig.RawDepthMax);

    private static IKeyMap BuildMap()
    {
        var document = new KeyMapDocument
        {
            KeyHidMap =
            {
                ["O"] = new[] { 2, 9 },
                ["N"] = new[] { 4, 7 },
                ["T"] = new[] { 2, 5 },
                ["V"] = new[] { 4, 5 },
                ["F"] = new[] { 3, 5 },
                ["G"] = new[] { 3, 6 },
                ["Y"] = new[] { 2, 6 },
                ["TAB"] = new[] { 2, 0 },
            },
            ControllerMap =
            {
                ["O"] = new KeyBindingEntry { Action = "LS_UP", Kind = "analog" },
                ["N"] = new KeyBindingEntry { Action = "LS_DOWN", Kind = "analog" },
                ["T"] = new KeyBindingEntry { Action = "RS_UP", Kind = "analog" },
                ["V"] = new KeyBindingEntry { Action = "RS_DOWN", Kind = "analog" },
                ["F"] = new KeyBindingEntry { Action = "RS_LEFT", Kind = "analog" },
                ["G"] = new KeyBindingEntry { Action = "RS_RIGHT", Kind = "analog" },
                ["Y"] = new KeyBindingEntry { Action = "R3", Kind = "digital" },
                ["TAB"] = new KeyBindingEntry { Action = "GUIDE", Kind = "digital" },
            },
        };

        return KeyMapLoader.FromDocument(document);
    }

    [Fact]
    public void LeftStickVertical_RespondsToItsKeys()
    {
        var state = new InputState(BuildMap());

        state.Update("O", InputTuningConfig.RawDepthMax);
        Assert.Equal(1.0, state.Snapshot().LeftStickY, 3);

        state.Update("O", 0);
        state.Update("N", InputTuningConfig.RawDepthMax);
        Assert.Equal(-1.0, state.Snapshot().LeftStickY, 3);
    }

    [Fact]
    public void RightStick_RespondsOnBothAxes()
    {
        var state = new InputState(BuildMap());

        state.Update("G", InputTuningConfig.RawDepthMax);
        state.Update("T", InputTuningConfig.RawDepthMax);

        var snapshot = state.Snapshot();
        Assert.Equal(1.0, snapshot.RightStickX, 3);
        Assert.Equal(1.0, snapshot.RightStickY, 3);
    }

    [Fact]
    public void OpposingKeysOnOneAxisCancelOut()
    {
        var state = new InputState(BuildMap());

        state.Update("F", InputTuningConfig.RawDepthMax);
        state.Update("G", InputTuningConfig.RawDepthMax);

        Assert.Equal(0.0, state.Snapshot().RightStickX, 3);
    }

    [Fact]
    public void PartialPressGivesPartialDeflection()
    {
        var state = new InputState(BuildMap());
        state.Update("G", Depth(0.5));

        // Right stick X defaults to a linear curve.
        Assert.Equal(0.5, state.Snapshot().RightStickX, 3);
    }

    [Fact]
    public void GuideAndR3NowHaveRealButtonsBehindThem()
    {
        // The gap that made these two dead however they were bound: the
        // output layer silently skips any action with no button.
        Assert.True(ActionButtonMap.TryGetButton("R3", out _));
        Assert.True(ActionButtonMap.TryGetButton("GUIDE", out _));
    }

    [Fact]
    public void EveryDigitalControlOnThePadHasAButton()
    {
        var missing = XboxControls.All
            .Where(c => c.Kind == InputType.Digital)
            .Where(c => !ActionButtonMap.TryGetButton(c.ActionId, out _))
            .Select(c => c.DisplayName)
            .ToList();

        Assert.True(missing.Count == 0,
            "These digital controls can be bound but would do nothing: " + string.Join(", ", missing));
    }
}
