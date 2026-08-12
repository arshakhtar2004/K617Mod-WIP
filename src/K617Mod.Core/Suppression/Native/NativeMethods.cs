using System.Runtime.InteropServices;

namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// Raw Win32 P/Invoke declarations for talking directly to the
/// Interception kernel driver's device files - the same low-level
/// approach the original Python build's interception.py used (calling
/// CreateFileW/DeviceIoControl directly against \\.\interceptionNN,
/// rather than going through interception.dll's own C API). Ported here
/// rather than switching to a different binding, so the known-working
/// IOCTL codes and struct layouts carry over exactly instead of trusting
/// an unverified third-party wrapper's API surface.
/// </summary>
internal static class NativeMethods
{
    public const uint GenericRead = 0x80000000;
    public const uint OpenExisting = 3;
    public static readonly IntPtr InvalidHandleValue = new(-1);

    public const uint WaitTimeout = 0x00000102;
    public const uint WaitFailed = 0xFFFFFFFF;
    public const uint Infinite = 0xFFFFFFFF;

    public const uint IoctlSetEvent = 0x222040;
    public const uint IoctlSetFilter = 0x222010;
    public const uint IoctlGetFilter = 0x222020;
    public const uint IoctlRead = 0x222100;
    public const uint IoctlWrite = 0x222080;
    public const uint IoctlGetHardwareId = 0x222200;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateEventW(
        IntPtr lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DeviceIoControl(
        IntPtr hDevice, uint dwIoControlCode,
        byte[]? lpInBuffer, uint nInBufferSize,
        byte[]? lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint WaitForMultipleObjects(
        uint nCount, IntPtr[] lpHandles,
        [MarshalAs(UnmanagedType.Bool)] bool bWaitAll, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);
}
