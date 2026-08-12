using K617Mod.Core.State;

namespace K617Mod.Core.Output;

/// <summary>
/// Contract for anything that can drive a virtual controller from a
/// ControllerStateSnapshot. The future orchestrator (Part 8) should
/// depend only on this interface, never on VigemVirtualPad directly -
/// same pattern as IHidKeySource and IInputState before it. Lets the
/// real ViGEm implementation be swapped for a test double with no
/// ViGEmBus driver involved at all.
/// </summary>
public interface IVirtualPad : IDisposable
{
    /// <summary>Push one tick's worth of state to the controller.</summary>
    void Apply(ControllerStateSnapshot snapshot);

    /// <summary>Center all axes and release every mapped button - called on shutdown.</summary>
    void Reset();
}
