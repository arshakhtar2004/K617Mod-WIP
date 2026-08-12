using K617Mod.Core.Persistence;
using Xunit;

namespace K617Mod.Core.Tests.Persistence;

public class ProfileBootstrapperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonProfileStore _store;

    private static string DefaultMappingPath =>
        Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "keymapping.default.json");

    public ProfileBootstrapperTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "K617ModTests_" + Guid.NewGuid());
        _store = new JsonProfileStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void FirstRun_CreatesTypingAndFh6Profiles()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);

        var names = _store.ListProfileNames();
        Assert.Contains("Typing", names);
        Assert.Contains("FH6", names);
    }

    [Fact]
    public void FirstRun_ReturnsFh6AsStartupProfile()
    {
        var startupName = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);
        Assert.Equal("FH6", startupName);
    }

    [Fact]
    public void Fh6Profile_HasRealMappingData_NotEmpty()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);

        var fh6 = _store.LoadProfile("FH6");
        Assert.NotEmpty(fh6.KeyMapping.ControllerMap);
    }

    [Fact]
    public void SecondRun_DoesNotOverwriteExistingProfiles()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);

        // Modify FH6 to prove a second bootstrap call doesn't clobber it.
        var fh6 = _store.LoadProfile("FH6");
        fh6.Description = "Modified by the test.";
        _store.SaveProfile(fh6);

        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);

        var reloaded = _store.LoadProfile("FH6");
        Assert.Equal("Modified by the test.", reloaded.Description);
    }

    [Fact]
    public void WhenLastActiveProfileWasSet_ThatIsReturnedInstead()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);
        _store.SaveProfile(new ProfileDocument { Name = "Custom" });
        _store.SetLastActiveProfileName("Custom");

        var startupName = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultMappingPath);

        Assert.Equal("Custom", startupName);
    }
}
