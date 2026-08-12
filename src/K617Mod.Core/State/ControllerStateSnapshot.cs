namespace K617Mod.Core.State;

/// <summary>
/// A single tick's worth of computed controller state - everything the
/// Virtual Controller Output module needs, and nothing about how it was
/// derived. Output and UI depend only on this shape, never on
/// InputState's internals.
///
/// Originally this carried only Steering/Accelerate/Brake, because the
/// racing mapping was the only thing bound. It now covers both sticks,
/// so every axis a real pad has can be driven.
/// </summary>
/// <param name="LeftStickX">-1.0 (full left) to 1.0 (full right). Steering, in racing terms.</param>
/// <param name="LeftStickY">-1.0 (full down) to 1.0 (full up).</param>
/// <param name="RightStickX">-1.0 (full left) to 1.0 (full right).</param>
/// <param name="RightStickY">-1.0 (full down) to 1.0 (full up).</param>
/// <param name="Accelerate">0.0 to 1.0 - right trigger.</param>
/// <param name="Brake">0.0 to 1.0 - left trigger.</param>
/// <param name="DigitalStates">Controller action name -> pressed. Only
/// includes actions currently bound to a DIGITAL-type key.</param>
public readonly record struct ControllerStateSnapshot(
    double LeftStickX,
    double LeftStickY,
    double RightStickX,
    double RightStickY,
    double Accelerate,
    double Brake,
    IReadOnlyDictionary<string, bool> DigitalStates)
{
    /// <summary>
    /// Kept so the existing tests and harnesses that talk about
    /// "steering" still read naturally - it is the same value as
    /// LeftStickX, just named for what it does on a car.
    /// </summary>
    public double Steering => LeftStickX;

    /// <summary>
    /// Convenience constructor for the racing-shaped case: left stick
    /// horizontal plus the two triggers, everything else centred. Keeps
    /// the many existing tests that construct snapshots this way
    /// working unchanged.
    /// </summary>
    public ControllerStateSnapshot(
        double steering,
        double accelerate,
        double brake,
        IReadOnlyDictionary<string, bool> digitalStates)
        : this(steering, 0.0, 0.0, 0.0, accelerate, brake, digitalStates)
    {
    }
}
