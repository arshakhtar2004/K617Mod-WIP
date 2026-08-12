namespace K617Mod.Core.Hid;

/// <summary>
/// Contract for anything that can produce a stream of raw key reports.
/// Every other module in the pipeline (key mapping, input state, UI)
/// depends only on this interface, never on a concrete implementation -
/// so K617HidSource (the real device) can later be swapped for a
/// recorded replay source during development or automated testing
/// without any other module changing at all.
/// </summary>
public interface IHidKeySource : IDisposable
{
    /// <summary>
    /// Raised on every valid parsed report. Fired from a background read
    /// thread - subscribers that touch UI must marshal back to the UI
    /// thread themselves. This module deliberately has no opinion on
    /// threading model beyond "not the caller's thread."
    /// </summary>
    event EventHandler<RawKeyReport>? ReportReceived;

    /// <summary>True once a connection is open and reports are flowing.</summary>
    bool IsConnected { get; }

    /// <summary>Opens the device (or replay source) and begins raising events.</summary>
    void Start();

    /// <summary>Stops reading and releases the underlying connection.</summary>
    void Stop();
}
