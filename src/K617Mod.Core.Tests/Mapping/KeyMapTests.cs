using K617Mod.Core.Mapping;
using Xunit;

namespace K617Mod.Core.Tests.Mapping;

public class KeyMapTests
{
    // Hand-written, minimal data - deliberately not loaded from JSON here,
    // so these tests exercise KeyMap in total isolation from KeyMapLoader.
    private static KeyMap BuildSampleMap()
    {
        var positions = new Dictionary<string, KeyPosition>
        {
            ["W"] = new KeyPosition(2, 2),
            ["A"] = new KeyPosition(3, 2),
            ["J"] = new KeyPosition(3, 8),
        };

        var bindings = new Dictionary<string, KeyBinding>
        {
            ["W"] = new KeyBinding("DPAD_UP", InputType.Digital),
            ["J"] = new KeyBinding("STEER_LEFT", InputType.Analog),
        };

        return new KeyMap(positions, bindings);
    }

    [Fact]
    public void GetHidPosition_ReturnsKnownPosition()
    {
        var map = BuildSampleMap();
        Assert.Equal(new KeyPosition(2, 2), map.GetHidPosition("W"));
    }

    [Fact]
    public void GetHidPosition_IsCaseInsensitive()
    {
        var map = BuildSampleMap();
        Assert.Equal(new KeyPosition(2, 2), map.GetHidPosition("w"));
    }

    [Fact]
    public void GetHidPosition_UnknownKey_ReturnsNull()
    {
        var map = BuildSampleMap();
        Assert.Null(map.GetHidPosition("Z"));
    }

    [Fact]
    public void GetControllerAction_KnownKey_ReturnsBinding()
    {
        var map = BuildSampleMap();
        var binding = map.GetControllerAction("J");
        Assert.Equal("STEER_LEFT", binding?.Action);
        Assert.Equal(InputType.Analog, binding?.Kind);
    }

    [Fact]
    public void GetControllerAction_UnboundKey_ReturnsNull()
    {
        // "A" has a HID position but no controller binding in this sample -
        // mirrors a real reserved/spare key.
        var map = BuildSampleMap();
        Assert.Null(map.GetControllerAction("A"));
    }

    [Fact]
    public void FindKeyByPosition_ReverseLookupWorks()
    {
        var map = BuildSampleMap();
        Assert.Equal("W", map.FindKeyByPosition(new KeyPosition(2, 2)));
    }

    [Fact]
    public void FindKeyByPosition_UnknownPosition_ReturnsNull()
    {
        var map = BuildSampleMap();
        Assert.Null(map.FindKeyByPosition(new KeyPosition(9, 9)));
    }

    [Fact]
    public void IsAnalog_And_IsDigital_AreMutuallyExclusive()
    {
        var map = BuildSampleMap();

        Assert.True(map.IsAnalog("J"));
        Assert.False(map.IsDigital("J"));

        Assert.True(map.IsDigital("W"));
        Assert.False(map.IsAnalog("W"));
    }

    [Fact]
    public void BoundKeys_OnlyIncludesKeysWithControllerActions()
    {
        var map = BuildSampleMap();

        // "A" has a HID position but no binding, so it shouldn't appear.
        Assert.Contains("W", map.BoundKeys);
        Assert.Contains("J", map.BoundKeys);
        Assert.DoesNotContain("A", map.BoundKeys);
    }
}
