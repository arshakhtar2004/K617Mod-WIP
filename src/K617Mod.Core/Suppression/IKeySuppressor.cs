namespace K617Mod.Core.Suppression;

/// <summary>
/// Contract for blocking the K617 HE's keystrokes from reaching Windows
/// while leaving every other keyboard untouched. The future orchestrator
/// (Part 8) should depend only on this interface, never on
/// K617KeySuppressor directly.
/// </summary>
public interface IKeySuppressor : IDisposable
{
    /// <summary>Attaches to the Interception driver and begins suppressing. Throws if the driver isn't installed or attach fails.</summary>
    void Start();

    /// <summary>Detaches - typing on the K617 returns to normal immediately.</summary>
    void Stop();
}
