using K617Mod.Core.Hid;
using K617Mod.Core.Mapping;
using K617Mod.Core.Orchestration;
using K617Mod.Core.Output;
using K617Mod.Core.Persistence;
using K617Mod.Core.State;
using K617Mod.Core.Suppression;

Console.WriteLine("K617 HE Racing Mod");
Console.WriteLine("===================\n");

// --- Where profiles live, and where the shipped default FH6 mapping comes from ---
var appDataRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K617Mod");
var defaultProfilePath = Path.Combine(AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");

var profileStore = new JsonProfileStore(appDataRoot);
var startupProfileName = ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(profileStore, defaultProfilePath);

Console.WriteLine($"Loading profile: {startupProfileName}");
var profile = profileStore.LoadProfile(startupProfileName);
var keyMap = KeyMapLoader.FromDocument(profile.KeyMapping);

// The profile's own response curves and digital threshold now drive the
// pipeline, rather than the fixed constants InputState used to read.
// Held in a TuningSource rather than passed as a plain value so it can
// be swapped while running, once there's a UI to swap it from.
var tuningSource = new TuningSource(profile.ToTuning());

IHidKeySource hidSource = new K617HidSource();

IVirtualPad virtualPad;
try
{
    virtualPad = new VigemVirtualPad();
}
catch (Exception ex)
{
    Console.WriteLine($"Could not connect to ViGEmBus: {ex.Message}");
    Console.WriteLine("Is ViGEmBus installed? See README-Part4.md.");
    return;
}

IKeySuppressor keySuppressor = new K617KeySuppressor();

using var orchestrator = new AppOrchestrator(hidSource, keyMap, virtualPad, keySuppressor, tuningSource);

Console.WriteLine("\nHold a key on the K617 HE NOW, then press Enter.");
Console.WriteLine("(Same reason as Part 1's harness - interface detection needs live data.)");
Console.ReadLine();

try
{
    orchestrator.Start();
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to start: {ex.Message}");
    return;
}

Console.WriteLine($"\nConnected: {orchestrator.IsConnected}");
Console.WriteLine(orchestrator.SuppressionActive
    ? "Suppression: ACTIVE - K617 keystrokes are blocked, controller input only."
    : $"Suppression: OFF ({orchestrator.SuppressionError ?? "disabled"}). " +
      "K617 will ALSO type normally alongside controller input.");

Console.WriteLine("\nRunning. Open Forza Horizon 6 (or joy.cpl) to test. Press Enter here to stop.\n");
Console.ReadLine();

orchestrator.Stop();
Console.WriteLine("Stopped. K617 typing restored, virtual controller released.");
