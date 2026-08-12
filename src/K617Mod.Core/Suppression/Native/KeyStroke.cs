using System.Runtime.InteropServices;

namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// Matches interception.py's KeyStroke ctypes.Structure field-for-field:
/// id, code, state, reserved (all USHORT/ushort), info (ULONG/uint).
/// Total 12 bytes with natural alignment, no padding - same layout the
/// driver expects on the Python side, so it carries over exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KeyStroke
{
    public ushort Id;
    public ushort Code;
    public ushort State;
    public ushort Reserved;
    public uint Info;
}
