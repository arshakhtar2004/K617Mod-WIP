namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// Manages all 20 Interception device files and dispatches whichever one
/// has a keystroke ready. Matches interception.py's Interception class.
/// </summary>
internal sealed class InterceptionContext : IDisposable
{
    private const int DeviceCount = 20;

    private readonly List<InterceptionDevice> _devices = new();
    private readonly IntPtr[] _eventHandles;

    public InterceptionContext()
    {
        for (var i = 1; i <= DeviceCount; i++)
        {
            _devices.Add(new InterceptionDevice(i));
        }
        _eventHandles = _devices.Select(d => d.EventHandle).ToArray();
    }

    public void SetKeyboardFilter(KeyFilter filter)
    {
        foreach (var device in _devices.Where(d => d.IsKeyboard))
        {
            device.SetFilter(filter);
        }
    }

    /// <summary>
    /// Waits up to timeoutMs for any device to have a stroke ready. On
    /// success, that device's LastStroke is already populated - callers
    /// should loop and re-check a stop condition between calls rather
    /// than waiting forever in one call.
    /// </summary>
    public InterceptionDevice? WaitReceive(int timeoutMs)
    {
        var result = NativeMethods.WaitForMultipleObjects(
            (uint)_eventHandles.Length, _eventHandles, false, (uint)timeoutMs);

        if (result == NativeMethods.WaitTimeout || result == NativeMethods.WaitFailed)
        {
            return null;
        }

        var device = _devices[(int)result];
        device.Receive();
        return device;
    }

    public void Dispose()
    {
        foreach (var device in _devices) device.Dispose();
    }
}
