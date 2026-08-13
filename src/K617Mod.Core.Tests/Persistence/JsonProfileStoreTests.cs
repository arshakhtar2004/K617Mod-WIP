using K617Mod.Core.Mapping;
using K617Mod.Core.Persistence;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.Persistence;

/// <summary>
/// Uses a fresh temp directory per test instance (xUnit creates a new
/// test class instance per test method by default, so the constructor/
/// Dispose pair below effectively runs once per test) - hermetic, never
/// touches the real user's AppData, safe to run repeatedly or in
/// parallel.
/// </summary>
public class JsonProfileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly JsonProfileStore _store;

    public JsonProfileStoreTests()
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
    public void NoProfilesSavedYet_ListIsEmpty()
    {
        Assert.Empty(_store.ListProfileNames());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsBasicFields()
    {
        var profile = new ProfileDocument
        {
            Name = "TestProfile",
            Description = "A profile used only in tests.",
            DigitalPressThreshold = 0.4,
        };

        _store.SaveProfile(profile);
        var loaded = _store.LoadProfile("TestProfile");

        Assert.Equal("TestProfile", loaded.Name);
        Assert.Equal("A profile used only in tests.", loaded.Description);
        Assert.Equal(0.4, loaded.DigitalPressThreshold);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsResponseCurves()
    {
        var profile = new ProfileDocument { Name = "CurveProfile" };
        profile.Curves[CurveAxes.RightTrigger] = new ResponseCurve(new[]
        {
            new CurvePoint(0.0, 0.0),
            new CurvePoint(0.4, 0.0),   // deadzone: nothing until 40% depth
            new CurvePoint(1.0, 1.0),
        });

        _store.SaveProfile(profile);
        var loaded = _store.LoadProfile("CurveProfile");

        var curve = loaded.Curves[CurveAxes.RightTrigger];
        Assert.Equal(3, curve.Points.Count);
        Assert.Equal(0.0, curve.Evaluate(0.2), 6);
        Assert.Equal(0.5, curve.Evaluate(0.7), 6);
    }

    [Fact]
    public void SaveThenLoad_PreservesEmbeddedKeyMapping()
    {
        var profile = new ProfileDocument { Name = "MappingTest" };
        profile.KeyMapping.KeyHidMap["W"] = new[] { 2, 2 };
        profile.KeyMapping.ControllerMap["W"] = new KeyBindingEntry { Action = "DPAD_UP", Kind = "digital" };

        _store.SaveProfile(profile);
        var loaded = _store.LoadProfile("MappingTest");

        Assert.True(loaded.KeyMapping.KeyHidMap.ContainsKey("W"));
        Assert.Equal(2, loaded.KeyMapping.KeyHidMap["W"][0]);
        Assert.Equal(2, loaded.KeyMapping.KeyHidMap["W"][1]);
        Assert.Equal("DPAD_UP", loaded.KeyMapping.ControllerMap["W"].Action);
    }

    [Fact]
    public void SavedProfile_CanBeBuiltIntoARealKeyMap()
    {
        // Proves the two modules actually connect - not just that JSON
        // round-trips, but that a saved profile's mapping is directly
        // usable by Part 2's loader with no conversion step in between.
        var profile = new ProfileDocument { Name = "IntegrationTest" };
        profile.KeyMapping.KeyHidMap["J"] = new[] { 3, 8 };
        profile.KeyMapping.ControllerMap["J"] = new KeyBindingEntry { Action = "STEER_LEFT", Kind = "analog" };

        _store.SaveProfile(profile);
        var loaded = _store.LoadProfile("IntegrationTest");
        var keyMap = KeyMapLoader.FromDocument(loaded.KeyMapping);

        Assert.True(keyMap.IsAnalog("J"));
        Assert.Equal(new KeyPosition(3, 8), keyMap.GetHidPosition("J"));
    }

    [Fact]
    public void ListProfileNames_ReflectsWhatWasSaved()
    {
        _store.SaveProfile(new ProfileDocument { Name = "Alpha" });
        _store.SaveProfile(new ProfileDocument { Name = "Beta" });

        var names = _store.ListProfileNames();

        Assert.Equal(2, names.Count);
        Assert.Contains("Alpha", names);
        Assert.Contains("Beta", names);
    }

    [Fact]
    public void LoadProfile_UnknownName_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => _store.LoadProfile("DoesNotExist"));
    }

    [Fact]
    public void DeleteProfile_RemovesItFromTheList()
    {
        _store.SaveProfile(new ProfileDocument { Name = "ToDelete" });
        Assert.Single(_store.ListProfileNames());

        _store.DeleteProfile("ToDelete");

        Assert.Empty(_store.ListProfileNames());
    }

    [Fact]
    public void DeleteProfile_ThatDoesNotExist_DoesNotThrow()
    {
        _store.DeleteProfile("NeverExisted"); // should be a silent no-op
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SaveProfile_EmptyOrWhitespaceName_Throws(string badName)
    {
        var profile = new ProfileDocument { Name = badName };
        Assert.Throws<ArgumentException>(() => _store.SaveProfile(profile));
    }

    [Fact]
    public void SaveProfile_NameWithPathSeparator_Throws()
    {
        // Guards against a profile name accidentally (or maliciously)
        // escaping the profiles folder via a path separator.
        var profile = new ProfileDocument { Name = "../evil" };
        Assert.Throws<ArgumentException>(() => _store.SaveProfile(profile));
    }

    [Fact]
    public void LastActiveProfile_DefaultsToNull()
    {
        Assert.Null(_store.GetLastActiveProfileName());
    }

    [Fact]
    public void LastActiveProfile_PersistsAfterSet()
    {
        _store.SetLastActiveProfileName("FH6");
        Assert.Equal("FH6", _store.GetLastActiveProfileName());
    }

    [Fact]
    public void LastActiveProfile_SurvivesANewStoreInstance()
    {
        // Confirms it's actually written to disk, not just cached in memory.
        _store.SetLastActiveProfileName("FH6");

        var reopened = new JsonProfileStore(_tempDir);

        Assert.Equal("FH6", reopened.GetLastActiveProfileName());
    }

    [Fact]
    public void LastModeActive_DefaultsToTrue()
    {
        // Null-in-storage means "never set", treated as on - a fresh
        // install should still auto-start, matching the original
        // always-auto-start behaviour from before this setting existed.
        Assert.True(_store.GetLastModeActive());
    }

    [Fact]
    public void LastModeActive_PersistsAfterSet()
    {
        _store.SetLastModeActive(false);
        Assert.False(_store.GetLastModeActive());

        _store.SetLastModeActive(true);
        Assert.True(_store.GetLastModeActive());
    }

    [Fact]
    public void LastModeActive_SurvivesANewStoreInstance()
    {
        _store.SetLastModeActive(false);

        var reopened = new JsonProfileStore(_tempDir);

        Assert.False(reopened.GetLastModeActive());
    }

    [Fact]
    public void SettingActiveProfileAndModeActive_BothPersistIndependently()
    {
        // Regression test: the app-settings.json setters used to each
        // construct a brand new AppSettingsDocument from scratch and
        // overwrite the whole file, so setting one field silently wiped
        // out whatever the other one had just saved. Load-modify-save
        // fixed that - this proves both fields survive regardless of
        // which order they're set in.
        _store.SetLastActiveProfileName("FH6");
        _store.SetLastModeActive(false);

        Assert.Equal("FH6", _store.GetLastActiveProfileName());
        Assert.False(_store.GetLastModeActive());

        _store.SetLastModeActive(true);
        Assert.Equal("FH6", _store.GetLastActiveProfileName()); // still there
        Assert.True(_store.GetLastModeActive());
    }
}
