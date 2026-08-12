using K617Mod.Core.Mapping;
using Xunit;

namespace K617Mod.Core.Tests.Mapping;

/// <summary>
/// Tests the document-only loading methods added to KeyMapLoader as part
/// of Part 6 - separate from KeyMapLoaderTests (Part 2), which covers
/// the full IKeyMap-building path that already existed.
/// </summary>
public class KeyMapLoaderDocumentTests
{
    private static string DefaultJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "keymapping.default.json");

    [Fact]
    public void LoadDocumentFromFile_ParsesWithoutError()
    {
        var doc = KeyMapLoader.LoadDocumentFromFile(DefaultJsonPath);
        Assert.NotEmpty(doc.KeyHidMap);
        Assert.NotEmpty(doc.ControllerMap);
    }

    [Fact]
    public void FromDocument_ProducesSameResultAsTheOriginalLoadFromFile()
    {
        // Confirms splitting LoadFromJson into two layers (document parse
        // + build) didn't change behavior for the path Part 2's own
        // tests already rely on.
        var viaDocument = KeyMapLoader.FromDocument(KeyMapLoader.LoadDocumentFromFile(DefaultJsonPath));
        var viaDirectLoad = KeyMapLoader.LoadFromFile(DefaultJsonPath);

        Assert.Equal(viaDirectLoad.GetHidPosition("W"), viaDocument.GetHidPosition("W"));
        Assert.Equal(viaDirectLoad.GetControllerAction("J")?.Action, viaDocument.GetControllerAction("J")?.Action);
        Assert.Equal(viaDirectLoad.IsAnalog("J"), viaDocument.IsAnalog("J"));
    }
}
