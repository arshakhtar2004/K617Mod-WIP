using System.Runtime.InteropServices;

namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// The IOCTL_SET_EVENT buffer shape the driver expects: two pointer-sized
/// slots, event handle first, second left zero. Matches interception.py's
/// `(HANDLE * 2)(self.event.handle)` padding exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DualHandle
{
    public IntPtr First;
    public IntPtr Second;
}
