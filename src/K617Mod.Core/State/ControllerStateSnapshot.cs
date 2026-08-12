namespace K617Mod.Core.State;

/// <summary>
/// A single tick's worth of computed controller state - everything the
/// Virtual Controller Output module (Part 4) and the UI (Part 7) need,
/// and nothing about how it was derived. Both of those modules should
/// depend only on this shape, never on InputState's internals.
/// </summary>
/// <param name="Steering">-1.0 (full left) to 1.0 (full right).</param>
/// <param name="Accelerate">0.0 to 1.0 - right trigger.</param>
/// <param name="Brake">0.0 to 1.0 - left trigger.</param>
/// <param name="DigitalStates">Controller action name -> pressed. Only
/// includes actions currently bound to a DIGITAL-type key.</param>
public readonly record struct ControllerStateSnapshot(
    double Steering,
    double Accelerate,
    double Brake,
    IReadOnlyDictionary<string, bool> DigitalStates);
