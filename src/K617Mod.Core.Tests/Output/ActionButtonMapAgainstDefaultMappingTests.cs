using K617Mod.Core.Mapping;
using K617Mod.Core.Output;
using Xunit;

namespace K617Mod.Core.Tests.Output;

/// <summary>
/// ActionButtonMap and keymapping.default.json are two independent
/// files with no compile-time link between them. If someone adds a new
/// digital action to the JSON and forgets to wire it to a button here
/// (or renames one without updating the other), that's a silent "button
/// does nothing" bug in the real app. This test is what would catch it.
/// </summary>
public class ActionButtonMapAgainstDefaultMappingTests
{
    private static string DefaultJsonPath =>
        Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "keymapping.default.json");

    [Fact]
    public void EveryDigitalActionInDefaultMapping_HasAMappedButton()
    {
        var keyMap = KeyMapLoader.LoadFromFile(DefaultJsonPath);

        foreach (var keyName in keyMap.BoundKeys)
        {
            var binding = keyMap.GetControllerAction(keyName);
            if (binding is not { Kind: InputType.Digital }) continue;

            Assert.True(
                ActionButtonMap.TryGetButton(binding.Value.Action, out _),
                $"Action '{binding.Value.Action}' (key '{keyName}') is DIGITAL in keymapping.default.json " +
                $"but has no button wired in ActionButtonMap.");
        }
    }
}
