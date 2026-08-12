using System.ComponentModel;
using System.Runtime.InteropServices;

namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// One of the driver's 20 device files (\\.\interception00 .. 19).
/// Devices 1-10 are keyboards, 11-20 are mice - matches
/// interception.py's MIN/MAX_KEYBOARD/MOUSE device index ranges.
/// </summary>
internal sealed class InterceptionDevice : IDisposable
{
    public int Index { get; }
    public bool IsKeyboard => Index is >= 1 and <= 10;

    public IntPtr Handle { get; }
    public IntPtr EventHandle { get; }

    /// <summary>The most recently received keystroke - populated by Receive(), sent on by Forward().</summary>
    public KeyStroke LastStroke { get; private set; }

    public InterceptionDevice(int index)
    {
        Index = index;
        var path = $@"\\.\interception{index - 1:D2}";

        Handle = NativeMethods.CreateFileW(
            path, NativeMethods.GenericRead, 0, IntPtr.Zero,
            NativeMethods.OpenExisting, 0, IntPtr.Zero);

        if (Handle == NativeMethods.InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open Interception device {path}.");
        }

        EventHandle = NativeMethods.CreateEventW(IntPtr.Zero, true, false, null);
        if (EventHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create synchronization event.");
        }

        var eventBuffer = StructMarshal.ToBytes(new DualHandle { First = EventHandle, Second = IntPtr.Zero });
        IoControl(NativeMethods.IoctlSetEvent, eventBuffer, null);
    }

    public void SetFilter(KeyFilter filter)
    {
        // Marshal the underlying ushort, not the enum itself - .NET's
        // generic Marshal.StructureToPtr<T>/SizeOf<T> reject enum types
        // outright ("cannot be marshaled as an unmanaged structure"),
        // even flags-backed ones with a primitive underlying type. This
        // surfaced as a real crash during hardware testing.
        var input = StructMarshal.ToBytes((ushort)filter);
        IoControl(NativeMethods.IoctlSetFilter, input, null);
    }

    public string? GetHardwareId()
    {
        var output = new byte[500]; // 250 WCHARs - matches interception.py's create_unicode_buffer(250)
        var written = IoControl(NativeMethods.IoctlGetHardwareId, null, output);
        if (written == 0) return null;

        var text = System.Text.Encoding.Unicode.GetString(output);
        var nullIndex = text.IndexOf('\0');
        return nullIndex >= 0 ? text[..nullIndex] : text;
    }

    /// <summary>Blocks until the driver has a keystroke ready on this device, then reads it into LastStroke.</summary>
    public void Receive()
    {
        var output = new byte[Marshal.SizeOf<KeyStroke>()];
        IoControl(NativeMethods.IoctlRead, null, output);
        LastStroke = StructMarshal.FromBytes<KeyStroke>(output);
    }

    /// <summary>Re-sends LastStroke unmodified - this is how a device's input actually reaches Windows/apps.</summary>
    public void Forward()
    {
        var input = StructMarshal.ToBytes(LastStroke);
        IoControl(NativeMethods.IoctlWrite, input, null);
    }

    private uint IoControl(uint code, byte[]? input, byte[]? output)
    {
        var ok = NativeMethods.DeviceIoControl(
            Handle, code,
            input, input is null ? 0u : (uint)input.Length,
            output, output is null ? 0u : (uint)output.Length,
            out var bytesReturned, IntPtr.Zero);

        if (!ok)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"DeviceIoControl failed for device {Index}, IOCTL 0x{code:X}.");
        }

        return bytesReturned;
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero) NativeMethods.CloseHandle(Handle);
        if (EventHandle != IntPtr.Zero) NativeMethods.CloseHandle(EventHandle);
    }
}
