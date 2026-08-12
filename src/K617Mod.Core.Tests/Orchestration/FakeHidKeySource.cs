using K617Mod.Core.Hid;

namespace K617Mod.Core.Tests.Orchestration;

/// <summary>
/// Test double for IHidKeySource - lets a test manually raise
/// ReportReceived events with synthetic data instead of reading a real
/// device. This is effectively a minimal early version of the
/// "Simulated Input Source" idea discussed for UI development later -
/// same shape, just built here first to make AppOrchestrator testable.
/// </summary>
public sealed class FakeHidKeySource : IHidKeySource
{
    public event EventHandler<RawKeyReport>? ReportReceived;

    public bool IsConnected { get; private set; }
    public bool StartCalled { get; private set; }
    public bool StopCalled { get; private set; }

    public void Start()
    {
        StartCalled = true;
        IsConnected = true;
    }

    public void Stop()
    {
        StopCalled = true;
        IsConnected = false;
    }

    /// <summary>Simulates a real report arriving from hardware.</summary>
    public void RaiseReport(int row, int col, int depth, ReportMode mode = ReportMode.Live) =>
        ReportReceived?.Invoke(this, new RawKeyReport(row, col, depth, mode, DateTime.UtcNow));

    public void Dispose() { }
}
