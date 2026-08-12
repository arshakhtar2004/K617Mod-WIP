using K617Mod.Core.Mapping;
using Xunit;

namespace K617Mod.Core.Tests.Mapping;

/// <summary>
/// Loads the real keymapping.default.json that ships with the app -
/// separate from KeyMapTests, which only exercises hand-written data.
/// This is what would actually catch a typo'd row/col or a duplicate
/// key in the shipped file itself.
/// </summary>
public class KeyMapLoaderTests
{
    private static string DefaultJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "keymapping.default.json");

    [Fact]
    public void DefaultJson_LoadsWithoutError()
    {
        var map = KeyMapLoader.LoadFromFile(DefaultJsonPath);
        Assert.NotEmpty(map.BoundKeys);
    }

    [Fact]
    public void DefaultJson_KnownConfirmedMappings_AreCorrect()
    {
        // These four were confirmed empirically via WebHID capture against
        // the real keyboard - if this test ever fails, the JSON file was
        // edited incorrectly, not the loader logic.
        var map = KeyMapLoader.LoadFromFile(DefaultJsonPath);

        Assert.Equal(new KeyPosition(2, 2), map.GetHidPosition("W"));
        Assert.Equal(new KeyPosition(3, 2), map.GetHidPosition("A"));
        Assert.Equal(new KeyPosition(3, 3), map.GetHidPosition("S"));
        Assert.Equal(new KeyPosition(3, 4), map.GetHidPosition("D"));
    }

    [Fact]
    public void DefaultJson_SteeringKeys_AreAnalog()
    {
        var map = KeyMapLoader.LoadFromFile(DefaultJsonPath);

        Assert.True(map.IsAnalog("J"));
        Assert.True(map.IsAnalog("L"));
        Assert.True(map.IsAnalog("I"));
        Assert.True(map.IsAnalog("K"));
    }

    [Fact]
    public void DefaultJson_DPadKeys_AreDigital()
    {
        var map = KeyMapLoader.LoadFromFile(DefaultJsonPath);

        Assert.True(map.IsDigital("W"));
        Assert.True(map.IsDigital("A"));
        Assert.True(map.IsDigital("S"));
        Assert.True(map.IsDigital("D"));
    }
}
