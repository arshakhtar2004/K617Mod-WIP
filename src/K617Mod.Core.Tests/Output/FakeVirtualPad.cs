using K617Mod.Core.Output;
using K617Mod.Core.State;

namespace K617Mod.Core.Tests.Output;

/// <summary>
/// Records what was sent to it instead of touching ViGEm at all. Lets
/// anything that depends on IVirtualPad be tested without ViGEmBus
/// installed - used here for Part 4's own tests, and reusable later for
/// testing Part 8 (the orchestrator) the same way.
/// </summary>
public sealed class FakeVirtualPad : IVirtualPad
{
    public ControllerStateSnapshot? LastApplied { get; private set; }
    public int ApplyCallCount { get; private set; }
    public bool WasReset { get; private set; }

    public void Apply(ControllerStateSnapshot snapshot)
    {
        LastApplied = snapshot;
        ApplyCallCount++;
    }

    public void Reset()
    {
        WasReset = true;
        LastApplied = null;
    }

    public void Dispose() { }
}
