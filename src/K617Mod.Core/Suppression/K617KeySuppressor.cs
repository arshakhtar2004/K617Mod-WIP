using K617Mod.Core.Suppression.Native;

namespace K617Mod.Core.Suppression;

/// <summary>
/// Blocks ALL keys from the K617 HE specifically, via the Interception
/// kernel driver, while leaving every other keyboard on the system
/// completely untouched. Direct port of key_suppressor.py's approach:
/// watch every keyboard device, and for each keystroke either forward it
/// (any other keyboard) or drop it (the K617) - not calling Forward() at
/// all is the actual suppression, since Windows never sees a dropped
/// keystroke.
/// </summary>
public sealed class K617KeySuppressor : IKeySuppressor
{
    private const int PollTimeoutMs = 200; // short so Stop() stays responsive, matches the Python build

    private Thread? _thread;
    private readonly ManualResetEventSlim _ready = new(false);
    private volatile bool _running;
    private string? _startupError;
    private InterceptionContext? _context;

    public void Start()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "K617KeySuppressor" };
        _thread.Start();

        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new TimeoutException("Timed out waiting for the Interception driver to attach.");
        }

        if (_startupError is not null)
        {
            throw new InvalidOperationException(_startupError);
        }
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(2));
        _context?.Dispose();
        _context = null;
    }

    private void Run()
    {
        try
        {
            _context = new InterceptionContext();
            _context.SetKeyboardFilter(KeyFilter.All);
        }
        catch (Exception ex)
        {
            _startupError =
                $"Could not attach to the Interception driver ({ex.Message}). Is it installed? " +
                "Run install-interception.exe /install as Administrator, then reboot. " +
                "Also confirm this app itself is running as Administrator.";
            _ready.Set();
            return;
        }

        _running = true;
        _ready.Set();

        while (_running)
        {
            var device = _context.WaitReceive(PollTimeoutMs);
            if (device is null) continue; // just a poll timeout, loop and re-check _running

            if (!device.IsKeyboard)
            {
                device.Forward();
                continue;
            }

            var hardwareId = device.GetHardwareId();
            if (HardwareIdMatcher.IsK617(hardwareId))
            {
                continue; // dropped: no Forward() -> Windows never sees this keystroke
            }

            device.Forward(); // every other keyboard (or unreadable hardware ID) behaves normally
        }
    }

    public void Dispose() => Stop();
}
