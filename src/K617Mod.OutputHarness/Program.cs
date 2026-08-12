using K617Mod.Core.Output;
using K617Mod.Core.State;

Console.WriteLine("K617 Mod - Virtual Controller Output Harness");
Console.WriteLine("Verifies Part 4 against a REAL ViGEmBus virtual controller.");
Console.WriteLine("Requires the ViGEmBus driver installed. Open Windows' joy.cpl now");
Console.WriteLine("(Run -> joy.cpl) to watch the axes and buttons move.");
Console.WriteLine("Ctrl+C to exit (releases the virtual pad cleanly).\n");

IVirtualPad pad;
try
{
    pad = new VigemVirtualPad();
    Console.WriteLine("Virtual Xbox 360 controller connected. Sweeping values...\n");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect - is ViGEmBus installed? {ex.Message}");
    return;
}

var stopwatch = System.Diagnostics.Stopwatch.StartNew();

try
{
    while (true)
    {
        double t = stopwatch.Elapsed.TotalSeconds;

        // Smooth, predictable sweeps - not meant to feel like real
        // driving, just meant to be easy to visually confirm as correct.
        double steering = Math.Sin(t);                    // sweeps -1..1
        double accelerate = (Math.Sin(t * 0.7) + 1) / 2;   // sweeps 0..1
        double brake = (Math.Cos(t * 0.5) + 1) / 2;        // sweeps 0..1

        // Toggle a couple of digital buttons every ~2 seconds too, so
        // this checks button wiring, not just the analog axes.
        bool toggle = (int)(t / 2) % 2 == 0;
        var digitalStates = new Dictionary<string, bool>
        {
            ["A_HANDBRAKE"] = toggle,
            ["DPAD_UP"] = !toggle,
        };

        pad.Apply(new ControllerStateSnapshot(steering, accelerate, brake, digitalStates));

        Console.Write(
            $"\rsteer={steering,6:F2}  accel={accelerate,4:F2}  brake={brake,4:F2}  " +
            $"A_HANDBRAKE={toggle,-5}  DPAD_UP={!toggle,-5}   ");

        Thread.Sleep(16); // ~60Hz - same order of magnitude as the real app's tick rate
    }
}
finally
{
    pad.Reset();
    pad.Dispose();
}
