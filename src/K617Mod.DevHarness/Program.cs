using K617Mod.Core.Hid;

Console.WriteLine("K617 HE HID Source - Dev Harness");
Console.WriteLine("Verifies Part 1 (HID Interface module) in isolation - no mapping, output, or UI involved.");
Console.WriteLine();
Console.WriteLine("IMPORTANT: hold down a key (e.g. W) on the K617 HE NOW and keep it held while");
Console.WriteLine("this connects - interface selection needs live data to find the right one.");
Console.Write("Press Enter here once you're holding a key down...");
Console.ReadLine();

IHidKeySource source = new K617HidSource();

source.ReportReceived += (_, report) =>
{
    Console.WriteLine(
        $"[{report.Timestamp:HH:mm:ss.fff}] mode={report.Mode,-7} row={report.Row,2} col={report.Col,2} depth={report.Depth,3}");
};

try
{
    source.Start();
    Console.WriteLine("Connected. You can release the key now - streaming live from here. Ctrl+C to exit.\n");
    Thread.Sleep(Timeout.Infinite);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
finally
{
    source.Stop();
}
