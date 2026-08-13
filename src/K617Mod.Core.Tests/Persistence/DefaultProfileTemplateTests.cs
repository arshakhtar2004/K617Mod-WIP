using K617Mod.Core.Mapping;
using K617Mod.Core.Persistence;
using K617Mod.Core.State;
using Xunit;

namespace K617Mod.Core.Tests.Persistence;

/// <summary>
/// The baseline profile is embedded in K617Mod.Core so a published
/// single-file build still has it - there is no folder beside the exe to
/// read it from in that layout. These tests are the guard on that: if
/// the EmbeddedResource entry is ever dropped from the csproj, or the
/// resource name drifts, this fails at build time rather than as a
/// first-run app that creates no profiles.
/// </summary>
public class DefaultProfileTemplateTests
{
    [Fact]
    public void TheEmbeddedBaselineIsPresentAndParses()
    {
        var template = DefaultProfileTemplate.Load();

        Assert.NotNull(template);
        Assert.NotEmpty(template.KeyMapping.KeyHidMap);
        Assert.NotEmpty(template.KeyMapping.ControllerMap);
        Assert.NotEmpty(template.Curves);
    }

    [Fact]
    public void TheEmbeddedBaselineBindsTheAnalogRacingControls()
    {
        var bindings = KeyBindingSet.FromKeyMap(
            KeyMapLoader.FromDocument(DefaultProfileTemplate.Load().KeyMapping));

        Assert.NotNull(bindings.KeyForAnalogAction("RT_ACCELERATE"));
        Assert.NotNull(bindings.KeyForAnalogAction("LT_BRAKE"));
        Assert.NotNull(bindings.KeyForAnalogAction("STEER_LEFT"));
        Assert.NotNull(bindings.KeyForAnalogAction("STEER_RIGHT"));
    }

    [Fact]
    public void TheEmbeddedBaselineMatchesTheLooseFileItIsBuiltFrom()
    {
        // The loose profile.default.json is the copy a person edits. If
        // the two ever diverge, the app and the console host would be
        // bootstrapping different baselines - which would show up as
        // "it works when I run it from Visual Studio".
        var loosePath = Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");
        Assert.True(File.Exists(loosePath), $"Expected the loose baseline next to the test assembly at {loosePath}");

        var embedded = DefaultProfileTemplate.Load();
        var loose = System.Text.Json.JsonSerializer.Deserialize<ProfileDocument>(
            File.ReadAllText(loosePath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        Assert.Equal(loose.KeyMapping.KeyHidMap.Count, embedded.KeyMapping.KeyHidMap.Count);
        Assert.Equal(loose.KeyMapping.ControllerMap.Count, embedded.KeyMapping.ControllerMap.Count);
        Assert.Equal(loose.DigitalPressThreshold, embedded.DigitalPressThreshold);
    }

    [Fact]
    public void BootstrappingFromTheEmbeddedBaselineCreatesAllFiveProfiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "K617ModTests", Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonProfileStore(root);

            var startup = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(store);

            Assert.Equal(ProfileBootstrapper.DefaultProfileName, startup);
            Assert.Equal(
                ProfileBootstrapper.AllProfileNames.OrderBy(n => n),
                store.ListProfileNames().OrderBy(n => n));
            Assert.True(store.LoadProfile(ProfileBootstrapper.DefaultProfileName).IsReadOnly);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
