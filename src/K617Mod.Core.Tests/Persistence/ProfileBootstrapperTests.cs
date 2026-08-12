using K617Mod.Core.Persistence;
using Xunit;

namespace K617Mod.Core.Tests.Persistence;

public class ProfileBootstrapperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonProfileStore _store;

    private static string DefaultProfilePath =>
        Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");

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
    public void FirstRun_CreatesAllFiveProfiles()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        var names = _store.ListProfileNames();
        Assert.Equal(5, names.Count);
        foreach (var expected in ProfileBootstrapper.AllProfileNames)
        {
            Assert.Contains(expected, names);
        }
    }

    [Fact]
    public void FirstRun_ReturnsDefaultAsStartupProfile()
    {
        var startupName = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);
        Assert.Equal(ProfileBootstrapper.DefaultProfileName, startupName);
    }

    [Fact]
    public void DefaultProfile_IsMarkedReadOnly()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        var def = _store.LoadProfile(ProfileBootstrapper.DefaultProfileName);
        Assert.True(def.IsReadOnly);
    }

    [Fact]
    public void EditableProfiles_AreNotReadOnly()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        foreach (var name in ProfileBootstrapper.EditableProfileNames)
        {
            Assert.False(_store.LoadProfile(name).IsReadOnly, $"'{name}' should be editable.");
        }
    }

    [Fact]
    public void EveryProfile_StartsAsACopyOfTheSameMapping()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        var baseline = _store.LoadProfile(ProfileBootstrapper.DefaultProfileName).KeyMapping.ControllerMap;
        Assert.NotEmpty(baseline);

        foreach (var name in ProfileBootstrapper.EditableProfileNames)
        {
            var copy = _store.LoadProfile(name).KeyMapping.ControllerMap;
            Assert.Equal(baseline.Count, copy.Count);
            foreach (var (key, binding) in baseline)
            {
                Assert.True(copy.ContainsKey(key), $"'{name}' is missing key '{key}'.");
                Assert.Equal(binding.Action, copy[key].Action);
            }
        }
    }

    [Fact]
    public void CopiesAreIndependent_EditingOneDoesNotAffectAnother()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        var second = _store.LoadProfile("Profile 2");
        second.KeyMapping.ControllerMap.Clear();
        _store.SaveProfile(second);

        Assert.NotEmpty(_store.LoadProfile("Profile 3").KeyMapping.ControllerMap);
        Assert.NotEmpty(_store.LoadProfile(ProfileBootstrapper.DefaultProfileName).KeyMapping.ControllerMap);
    }

    [Fact]
    public void SecondRun_DoesNotOverwriteExistingProfiles()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        var edited = _store.LoadProfile("Profile 2");
        edited.Description = "Modified by the test.";
        _store.SaveProfile(edited);

        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        Assert.Equal("Modified by the test.", _store.LoadProfile("Profile 2").Description);
    }

    [Fact]
    public void WhenLastActiveProfileWasSet_ThatIsReturnedInstead()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);
        _store.SetLastActiveProfileName("Profile 4");

        var startupName = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        Assert.Equal("Profile 4", startupName);
    }

    [Fact]
    public void WhenLastActiveProfileNoLongerExists_FallsBackToDefault()
    {
        ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);
        _store.SetLastActiveProfileName("Deleted Profile");

        var startupName = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, DefaultProfilePath);

        Assert.Equal(ProfileBootstrapper.DefaultProfileName, startupName);
    }

    [Fact]
    public void IsEditable_IsFalseOnlyForDefault()
    {
        Assert.False(ProfileBootstrapper.IsEditable(ProfileBootstrapper.DefaultProfileName));
        Assert.True(ProfileBootstrapper.IsEditable("Profile 2"));
    }
}
