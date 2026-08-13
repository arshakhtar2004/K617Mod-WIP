using System.IO;
using K617Mod.Core.Hid;
using K617Mod.Core.Mapping;
using K617Mod.Core.Orchestration;
using K617Mod.Core.Output;
using K617Mod.Core.Persistence;
using K617Mod.Core.State;
using K617Mod.Core.Suppression;

namespace K617Mod.Ui;

/// <summary>
/// What the mod is currently doing. The tray icon is a direct rendering of
/// this, so every state here has to be one a person can act on.
/// </summary>
public enum ModStatus
{
    /// <summary>Not running. Keyboard types normally.</summary>
    Stopped,

    /// <summary>Running, suppression active, analog data flowing. Everything is fine.</summary>
    Running,

    /// <summary>Running, but suppression failed to attach - the K617 is ALSO still typing.</summary>
    SuppressionFailed,

    /// <summary>
    /// Stopped because the analog interface was open but silent while keys
    /// were being pressed - the once-per-boot wake step hasn't been done.
    /// </summary>
    DeviceAsleep,

    /// <summary>Could not start at all. See StatusDetail.</summary>
    Error,
}

/// <summary>
/// Owns the running pipeline for the whole application: builds it, starts
/// it, stops it, and reports what state it's in. This is the composition
/// root - the one place allowed to know about concrete types like
/// K617HidSource and VigemVirtualPad, exactly as K617Mod.App's Program.cs
/// was for the console build.
///
/// Kept apart from the tray icon on purpose. The tray is a view of this;
/// it holds no lifecycle logic of its own, so the pipeline can be started
/// and stopped from a window, a hotkey or a test without any of that
/// behaviour living inside a UI widget. MainWindow's mod switch is the
/// second such view, wired the same way.
/// </summary>
public sealed class ModController : IDisposable
{
    /// <summary>
    /// How many K617 keystrokes have to be swallowed with zero analog
    /// reports before the device is declared asleep. More than one, so a
    /// single stray keypress during startup can't trip it.
    /// </summary>
    private const int AsleepKeystrokeThreshold = 3;

    private readonly object _gate = new();
    private readonly IProfileStore _store;

    private AppOrchestrator? _orchestrator;
    private K617HidSource? _hidSource;
    private K617KeySuppressor? _suppressor;
    private System.Threading.Timer? _watchdog;

    /// <summary>
    /// The tuning of the running pipeline. Held rather than being a local
    /// in Start() so a curve change can be pushed into a pipeline that is
    /// already going - previously this reference was dropped on the floor
    /// the moment Start() returned, which is why saving a curve did
    /// nothing until the mod was toggled off and on again.
    /// </summary>
    private TuningSource? _tuning;

    private volatile bool _sawAnalogData;

    public ModController()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K617Mod");
        var defaultProfilePath = Path.Combine(
            AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");

        _store = new JsonProfileStore(appDataRoot);

        // Bootstrap here rather than inside Start(). It has to happen
        // before any profile can be loaded, it is idempotent, and doing
        // it once at construction means a start/stop cycle isn't
        // re-listing the profile directory every time.
        try
        {
            ActiveProfileName =
                ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, defaultProfilePath);
        }
        catch
        {
            // Leave ActiveProfileName at Default. Start() will fail with
            // the real reason if the profile genuinely can't be read;
            // there's nothing useful to report from a constructor.
            ActiveProfileName = ProfileBootstrapper.DefaultProfileName;
        }
    }

    /// <summary>
    /// The profile the mod is running, or would run if started. Changed
    /// only through <see cref="ApplyProfile"/>.
    /// </summary>
    public string ActiveProfileName { get; private set; } = ProfileBootstrapper.DefaultProfileName;

    public ModStatus Status { get; private set; } = ModStatus.Stopped;

    /// <summary>Human-readable detail for the current status. May be empty.</summary>
    public string StatusDetail { get; private set; } = string.Empty;

    /// <summary>Raised whenever Status or StatusDetail changes. Fires on a background thread.</summary>
    public event EventHandler? StatusChanged;

    public bool IsRunning => Status is ModStatus.Running or ModStatus.SuppressionFailed;

    /// <summary>
    /// Whether the mod should auto-start when the app launches: whichever
    /// state it was last explicitly put into via Start()/Stop() (tray
    /// toggle or the in-window switch). Read once by App.xaml.cs at
    /// startup. Deliberately NOT the same as "was it running when the app
    /// last closed" - the sleeping-device watchdog and Dispose() both stop
    /// the pipeline without touching this saved preference, so an
    /// unrelated shutdown or a wake-step problem doesn't silently flip
    /// next launch to off.
    /// </summary>
    public bool ShouldStartOnLaunch() => _store.GetLastModeActive();

    /// <summary>
    /// Make a profile the live one. Safe to call whether the mod is
    /// running or stopped, and the only supported way to change which
    /// profile is in effect.
    ///
    /// While running, this swaps both halves in place rather than
    /// restarting the pipeline. A restart would drop suppression and the
    /// virtual pad for a moment, which the game sees as the controller
    /// disconnecting and reconnecting - not acceptable for something a
    /// person does mid-session.
    ///
    /// While stopped, it records the choice and persists it; the next
    /// Start() builds from it.
    ///
    /// Returns null on success, or a message describing why the profile
    /// could not be applied. Deliberately does not throw and does not
    /// change Status: a profile that fails to load leaves the mod running
    /// correctly on the previous one, which is not an error state for the
    /// mod itself, only for the thing that asked for the change.
    /// </summary>
    public string? ApplyProfile(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName)) return "No profile name given.";

        lock (_gate)
        {
            ProfileDocument profile;
            try
            {
                profile = _store.LoadProfile(profileName);
            }
            catch (Exception ex)
            {
                return $"Could not load '{profileName}': {ex.Message}";
            }

            if (IsRunning)
            {
                try
                {
                    // Curves first, bindings second. Both are safe on their
                    // own; doing tuning first means the worst case of a
                    // failure between them is the new curves applied to the
                    // old bindings, which is a tuning oddity rather than
                    // keys doing the wrong thing.
                    _tuning?.Apply(profile.ToTuning());
                    _orchestrator?.ApplyKeyMap(KeyMapLoader.FromDocument(profile.KeyMapping));
                }
                catch (Exception ex)
                {
                    return $"Could not apply '{profileName}' to the running mod: {ex.Message}";
                }
            }

            ActiveProfileName = profileName;

            // Persisted on every change, not just when the profile editor
            // saves. Selecting a profile and closing the app used to lose
            // the choice, and the read-only Default could never be stored
            // as active at all, because the only call site was inside a
            // save path that refuses to run for read-only profiles.
            try { _store.SetLastActiveProfileName(profileName); }
            catch { /* the profile is live; only the memory of it didn't save */ }

            return null;
        }
    }

    /// <summary>
    /// Re-read the active profile from disk and apply it. Called after the
    /// editor saves, so edits to the profile that is already live take
    /// effect without the person having to reselect it.
    /// </summary>
    public string? ReloadActiveProfile() => ApplyProfile(ActiveProfileName);

    public void Start()
    {
        lock (_gate)
        {
            if (IsRunning) return;

            StopInternal();
            _sawAnalogData = false;

            try
            {
                var profile = _store.LoadProfile(ActiveProfileName);
                var keyMap = KeyMapLoader.FromDocument(profile.KeyMapping);
                var tuning = new TuningSource(profile.ToTuning());
                _tuning = tuning;

                _hidSource = new K617HidSource();
                _suppressor = new K617KeySuppressor();

                IVirtualPad pad = new VigemVirtualPad();

                _hidSource.ReportReceived += OnReportReceived;

                _orchestrator = new AppOrchestrator(_hidSource, keyMap, pad, _suppressor, tuning);
                _orchestrator.Start();
            }
            catch (Exception ex)
            {
                StopInternal();
                SetStatus(ModStatus.Error, Describe(ex));
                return;
            }

            // Not null here: the only path that leaves it unset returns from
            // the catch above.
            var started = _orchestrator!;

            if (started.SuppressionActive)
            {
                SetStatus(ModStatus.Running, $"Interface found by {_hidSource!.SelectionMethod}.");
            }
            else
            {
                SetStatus(ModStatus.SuppressionFailed,
                    started.SuppressionError ?? "Suppression is off - the K617 will also type normally.");
            }

            // A real, person-initiated start (the try block above didn't
            // throw) - remember it for next launch. Wrapped so a disk
            // hiccup here can't undo an otherwise-successful start.
            TryPersistMode(active: true);

            // Only worth watching while suppression is actually swallowing
            // keys; without it, a silent device is merely useless rather
            // than a keyboard that appears broken.
            if (started.SuppressionActive)
            {
                _watchdog = new System.Threading.Timer(
                    CheckForSleepingDevice, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            }
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopInternal();
            SetStatus(ModStatus.Stopped, string.Empty);
            TryPersistMode(active: false);
        }
    }

    /// <summary>
    /// The un-woken-device trap, and the reason this class has a watchdog
    /// at all. Suppression blocks every K617 key, so if the analog
    /// interface is open but silent, the keyboard does nothing at all: no
    /// typing, no controller. With a tray-only launch there is no window to
    /// notice it in either.
    ///
    /// "No analog reports yet" on its own is not evidence - nobody may have
    /// touched the keyboard. Suppressed keystrokes WITH no analog reports
    /// is evidence, because it means keys are being pressed and swallowed
    /// while the device sends nothing. That combination only happens when
    /// the wake step hasn't been done.
    ///
    /// Deliberately calls StopInternal() + SetStatus() directly rather than
    /// the public Stop() - this is the pipeline failing on its own, not a
    /// person choosing to turn the mod off, so it must not overwrite the
    /// saved on/off preference Stop() persists.
    /// </summary>
    private void CheckForSleepingDevice(object? _)
    {
        if (_sawAnalogData || _suppressor is null) return;
        if (_suppressor.SuppressedKeyCount < AsleepKeystrokeThreshold) return;

        lock (_gate)
        {
            if (_sawAnalogData) return; // raced with a report arriving - device is fine

            StopInternal();
            SetStatus(ModStatus.DeviceAsleep,
                "Keys were pressed but the keyboard sent no analog data, so the mod stopped and " +
                "typing is back to normal. The K617's analog interface needs waking once per boot: " +
                "open iLumiPC's Travel Test page, close it, then start the mod again.");
        }
    }

    private void OnReportReceived(object? sender, RawKeyReport e) => _sawAnalogData = true;

    /// <summary>Tears the pipeline down. Caller holds the lock.</summary>
    private void StopInternal()
    {
        _watchdog?.Dispose();
        _watchdog = null;

        if (_hidSource is not null)
        {
            _hidSource.ReportReceived -= OnReportReceived;
        }

        // Disposing the orchestrator releases suppression and the virtual
        // pad as well as the HID source, so typing comes back even if
        // shutdown was abrupt.
        if (_orchestrator is not null)
        {
            try { _orchestrator.Dispose(); }
            catch { /* nothing useful to do while tearing down */ }
        }
        else
        {
            // Start() failed partway - the orchestrator that would have
            // owned these was never constructed, so they're disposed here
            // instead of being left dangling.
            try { _hidSource?.Dispose(); } catch { }
            try { _suppressor?.Dispose(); } catch { }
        }

        _orchestrator = null;
        _hidSource = null;
        _suppressor = null;

        // Belongs to the pipeline that just went away. Left dangling it
        // would accept curve changes that nothing is reading, and the
        // next Start() would replace it anyway.
        _tuning = null;
    }

    /// <summary>
    /// Saving the on/off preference must never be why a Start()/Stop() call
    /// itself fails - a disk error here is a lost "remember for next time",
    /// not a reason to leave the pipeline half-changed.
    /// </summary>
    private void TryPersistMode(bool active)
    {
        try { _store.SetLastModeActive(active); }
        catch { /* the mode itself already changed; only the memory of it didn't save */ }
    }

    private static string Describe(Exception ex) => ex switch
    {
        // The two failures worth naming, because both have a specific fix
        // and both otherwise surface as an opaque COM/driver message.
        _ when ex.Message.Contains("ViGEm", StringComparison.OrdinalIgnoreCase)
            => "Could not reach the ViGEmBus driver. Is it installed? " + ex.Message,
        _ when ex.Message.Contains("Interception", StringComparison.OrdinalIgnoreCase)
            => "Could not attach the key suppressor. " + ex.Message,
        _ => ex.Message,
    };

    private void SetStatus(ModStatus status, string detail)
    {
        if (Status == status && StatusDetail == detail) return;

        Status = status;
        StatusDetail = detail;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            StopInternal();
        }
    }
}
