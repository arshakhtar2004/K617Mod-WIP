namespace K617Mod.Core.State;

/// <summary>
/// Contract for tracking live per-key depth and producing controller
/// state snapshots from it. Same independence pattern as the previous
/// two parts: Part 4 (Output) and Part 7 (UI) should depend only on this
/// interface, never on InputState directly.
/// </summary>
public interface IInputState
{
    /// <summary>
    /// Feed a raw depth reading for a physical key. Safe to call from a
    /// different thread than Snapshot() - matches the real pipeline,
    /// where Part 1's HID read thread calls this and a separate
    /// fixed-rate tick thread calls Snapshot().
    /// </summary>
    void Update(string keyName, int rawDepth);

    /// <summary>Read every current value atomically and compute one tick's controller state.</summary>
    ControllerStateSnapshot Snapshot();
}
