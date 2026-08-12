namespace K617Mod.Core.Persistence;

/// <summary>
/// Contract for saving/loading profiles and app-wide settings. Part 7
/// (UI) and Part 8 (orchestrator) should depend only on this interface,
/// never on JsonProfileStore directly - same independence pattern as
/// every hardware-facing module before it, applied here so the storage
/// format itself stays swappable later (a database, cloud sync, whatever)
/// without touching anything that consumes profiles.
/// </summary>
public interface IProfileStore
{
    IReadOnlyList<string> ListProfileNames();
    ProfileDocument LoadProfile(string name);
    void SaveProfile(ProfileDocument profile);
    void DeleteProfile(string name);

    string? GetLastActiveProfileName();
    void SetLastActiveProfileName(string name);
}
