using K617Mod.Core.Suppression;

Console.WriteLine("K617 Mod - Key Suppression Harness");
Console.WriteLine("Verifies Part 5 against the REAL Interception driver.");
Console.WriteLine();
Console.WriteLine("REQUIRES:");
Console.WriteLine("  1. The Interception driver installed (install-interception.exe /install as Admin, then reboot).");
Console.WriteLine("  2. THIS terminal/app running as Administrator - low-level device access needs elevation.");
Console.WriteLine();
Console.WriteLine("Once attached: open Notepad. Typing on the K617 HE should produce NOTHING.");
Console.WriteLine("Typing on any other keyboard should work completely normally.");
Console.WriteLine("Ctrl+C to stop - K617 typing returns to normal immediately.\n");

IKeySuppressor suppressor = new K617KeySuppressor();

try
{
    suppressor.Start();
    Console.WriteLine("Attached. K617 HE keystrokes are now suppressed system-wide.");
    Console.WriteLine("Go test it in Notepad now. Press Ctrl+C here when you're done.\n");
    Thread.Sleep(Timeout.Infinite);
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to attach: {ex.Message}");
}
finally
{
    suppressor.Stop();
    Console.WriteLine("\nSuppression stopped. K617 typing is back to normal.");
}
