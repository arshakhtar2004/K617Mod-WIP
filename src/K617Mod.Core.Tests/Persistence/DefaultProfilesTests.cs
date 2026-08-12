using K617Mod.Core.Persistence;
using Xunit;

namespace K617Mod.Core.Tests.Persistence;

public class DefaultProfilesTests
{
    [Fact]
    public void Typing_HasNoControllerBindings()
    {
        var profile = DefaultProfiles.Typing();
        Assert.Empty(profile.KeyMapping.ControllerMap);
    }

    [Fact]
    public void Typing_HasExpectedName()
    {
        Assert.Equal("Typing", DefaultProfiles.Typing().Name);
    }

    [Fact]
    public void Typing_CanBeSavedAndLoadedLikeAnyOtherProfile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "K617ModTests_" + Guid.NewGuid());
        try
        {
            var store = new JsonProfileStore(tempDir);
            store.SaveProfile(DefaultProfiles.Typing());

            var loaded = store.LoadProfile("Typing");
            Assert.Equal("Typing", loaded.Name);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
