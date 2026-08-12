using K617Mod.Core.Suppression;

namespace K617Mod.Core.Tests.Orchestration;

/// <summary>
/// Test double for IKeySuppressor - records Start/Stop calls, and can be
/// configured to throw on Start() to exercise AppOrchestrator's
/// fail-open handling without needing the real Interception driver.
/// </summary>
public sealed class FakeKeySuppressor : IKeySuppressor
{
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }

    /// <summary>Set this before calling Start() to simulate an attach failure.</summary>
    public Exception? ThrowOnStart { get; set; }

    public void Start()
    {
        StartCalled = true;
        if (ThrowOnStart is not null) throw ThrowOnStart;
    }

    public void Stop() => StopCalled = true;

    public void Dispose() { }
}
