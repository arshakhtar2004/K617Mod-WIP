namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// Matches interception.py's KeyFilter IntFlag exactly. Only `All` is
/// actually used by this project - full suppression of every K617
/// keystroke - the rest are carried over for completeness.
/// </summary>
[Flags]
internal enum KeyFilter : ushort
{
    None = 0x0000,
    Down = 0x0001,
    Up = 0x0002,
    E0 = 0x0004,
    E1 = 0x0008,
    TermSrvSetLed = 0x0010,
    TermSrvShadow = 0x0020,
    TermSrvVkPacket = 0x0040,
    All = 0xFFFF,
}
