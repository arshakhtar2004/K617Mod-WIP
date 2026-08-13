using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using K617Mod.Core.Mapping;
using K617Mod.Core.Persistence;
using K617Mod.Core.State;

namespace K617Mod.Ui;

/// <summary>
/// One Xbox control and the physical key currently bound to it.
/// </summary>
public sealed class ControlBinding : INotifyPropertyChanged
{
    private string _keyName = ProfileSession.Unassigned;

    public ControlBinding(XboxControl control)
    {
        ActionId = control.ActionId;
        DisplayName = control.DisplayName;
        Kind = control.Kind;
    }

    public string ActionId { get; }
    public string DisplayName { get; }
    public InputType Kind { get; }

    public string KeyName
    {
        get => _keyName;
        set
        {
            if (_keyName == value) return;
            _keyName = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeyName)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// The single edit session shared by every settings page: which profile
/// is selected, its key bindings, its response curves, and whether there
/// are pending changes.
///
/// Deliberately one shared instance rather than a view model per page.
/// With separate instances the remap page could sit on "Profile 2" while
/// the curve page sat on "Default", and Apply on one would silently
/// overwrite pending edits from the other. Sharing the session means
/// "the selected profile" is one fact, and Apply commits mapping and
/// curves together in a single write.
/// </summary>
public sealed class ProfileSession : INotifyPropertyChanged
{
    public const string Unassigned = "—";

    /// <summary>The one instance every page binds to.</summary>
    public static ProfileSession Current { get; } = new();

    private readonly IProfileStore _store;
    private string _selectedProfileName = ProfileBootstrapper.DefaultProfileName;
    private bool _isEditable;
    private bool _hasUnsavedChanges;
    private string _loadError = string.Empty;

    private ProfileSession()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K617Mod");
        var defaultProfilePath = Path.Combine(
            AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");

        _store = new JsonProfileStore(appDataRoot);

        Bindings = XboxControls.All.ToDictionary(
            control => control.ActionId,
            control => new ControlBinding(control),
            StringComparer.OrdinalIgnoreCase);

        ProfileNames = ProfileBootstrapper.AllProfileNames;

        try
        {
            _selectedProfileName =
                ProfileBootstrapper.EnsureBootstrappedAndGetStartupProfileName(_store, defaultProfilePath);
        }
        catch (Exception ex)
        {
            LoadError = $"Could not set up profiles: {ex.Message}";
        }

        LoadSelectedProfile();
    }

    public IReadOnlyList<string> ProfileNames { get; }

    /// <summary>Action id -> binding. Stable for the app's lifetime, so XAML bindings stay valid.</summary>
    public Dictionary<string, ControlBinding> Bindings { get; }

    /// <summary>Working copy of the profile's curves, keyed by CurveAxes id.</summary>
    public Dictionary<string, ResponseCurve> Curves { get; private set; } = CurveAxes.Defaults();

    public ObservableCollection<string> AvailableKeys { get; } = new();

    public string SelectedProfileName
    {
        get => _selectedProfileName;
        set
        {
            if (_selectedProfileName == value || value is null) return;
            _selectedProfileName = value;
            OnPropertyChanged();
            LoadSelectedProfile();

            // Selecting a profile makes it the live one immediately.
            // Raised here rather than inside LoadSelectedProfile() because
            // that method also runs on Revert and at construction, and
            // neither of those changes what is on disk under the selected
            // name - so neither should disturb a running mod.
            //
            // Note this discards unsaved edits to the profile being left,
            // exactly as it did before. Worth a confirmation prompt
            // eventually; it is a bigger change than it looks, because
            // "cancel" has to put the combo box back without re-entering
            // this setter.
            LiveProfileChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsEditable
    {
        get => _isEditable;
        private set { _isEditable = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusNote)); }
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set { _hasUnsavedChanges = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusNote)); }
    }

    public string LoadError
    {
        get => _loadError;
        private set { _loadError = value; OnPropertyChanged(); }
    }

    public string StatusNote => !IsEditable
        ? "Default is read-only. Pick another profile to make changes."
        : HasUnsavedChanges
            ? "Unsaved changes."
            : "No pending changes.";

    /// <summary>Raised after a profile is loaded, so open editors can re-read their values.</summary>
    public event EventHandler? ProfileReloaded;

    /// <summary>
    /// Raised when a different profile is selected, or when the selected
    /// one is saved - in other words, whenever what is on disk under the
    /// selected name should become what the mod is running.
    ///
    /// An event rather than this class calling ModController directly.
    /// The session's job is editing profiles; whether anything is
    /// currently running them is not its business, and wiring it that way
    /// keeps the pages testable without a HID device or a ViGEm driver
    /// anywhere in reach. App.xaml.cs owns the connection between the
    /// two, which is where the rest of the composition already lives.
    /// </summary>
    public event EventHandler? LiveProfileChanged;

    /// <summary>
    /// Surface a problem that happened outside this class - specifically,
    /// the mod failing to take a profile the editor loaded and saved
    /// perfectly well. Shown in the same place as load and save errors,
    /// because from the person's side it is the same kind of news.
    /// </summary>
    public void ReportError(string message) => LoadError = message;

    public void Assign(ControlBinding target, string keyName)
    {
        if (!IsEditable || target.KeyName == keyName) return;

        // One physical key can only drive one control, so binding a key
        // already in use releases it from wherever it was. Silent for
        // now - telling the person about the clash is a later job.
        if (keyName != Unassigned)
        {
            foreach (var other in Bindings.Values.Where(b => b != target && b.KeyName == keyName))
            {
                other.KeyName = Unassigned;
            }
        }

        target.KeyName = keyName;
        HasUnsavedChanges = true;
    }

    public ResponseCurve GetCurve(string axisId) =>
        Curves.TryGetValue(axisId, out var curve) ? curve : ResponseCurve.Linear();

    public void SetCurve(string axisId, ResponseCurve curve)
    {
        if (!IsEditable) return;
        Curves[axisId] = curve;
        HasUnsavedChanges = true;
    }

    /// <summary>Writes mapping and curves into the selected profile in one save.</summary>
    public void Apply()
    {
        if (!IsEditable) return;

        try
        {
            var profile = _store.LoadProfile(SelectedProfileName);

            profile.KeyMapping.ControllerMap.Clear();
            foreach (var binding in Bindings.Values.Where(b => b.KeyName != Unassigned))
            {
                profile.KeyMapping.ControllerMap[binding.KeyName] = new KeyBindingEntry
                {
                    Action = binding.ActionId,
                    Kind = binding.Kind == InputType.Analog ? "analog" : "digital",
                };
            }

            profile.Curves = Curves.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Clone(),
                StringComparer.OrdinalIgnoreCase);

            _store.SaveProfile(profile);
            HasUnsavedChanges = false;
            LoadError = string.Empty;

            // What was just written is now what should be running.
            // SetLastActiveProfileName used to be called here too; it
            // moved to ModController.ApplyProfile, so that selecting a
            // profile is remembered even without a save - and so the
            // read-only Default, which never reaches this line, can be
            // the remembered one.
            LiveProfileChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            LoadError = $"Could not save '{SelectedProfileName}': {ex.Message}";
        }
    }

    public void Revert() => LoadSelectedProfile();

    private void LoadSelectedProfile()
    {
        foreach (var binding in Bindings.Values)
        {
            binding.KeyName = Unassigned;
        }

        AvailableKeys.Clear();
        AvailableKeys.Add(Unassigned);
        Curves = CurveAxes.Defaults();

        try
        {
            var profile = _store.LoadProfile(SelectedProfileName);

            IsEditable = !profile.IsReadOnly && ProfileBootstrapper.IsEditable(SelectedProfileName);

            foreach (var keyName in profile.KeyMapping.KeyHidMap.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                AvailableKeys.Add(keyName);
            }

            // Stored key -> action because that's how the HID pipeline
            // looks it up; the pages need action -> key, so invert once.
            foreach (var (keyName, entry) in profile.KeyMapping.ControllerMap)
            {
                if (!string.IsNullOrWhiteSpace(entry.Action)
                    && Bindings.TryGetValue(entry.Action, out var binding))
                {
                    binding.KeyName = keyName;
                }
            }

            // Missing axes keep their default curve rather than failing,
            // so a profile saved before an axis existed still loads.
            if (profile.Curves is not null)
            {
                foreach (var (axisId, curve) in profile.Curves)
                {
                    if (curve is null) continue;
                    curve.Normalize();
                    Curves[axisId] = curve.Clone();
                }
            }

            HasUnsavedChanges = false;
            LoadError = string.Empty;
        }
        catch (Exception ex)
        {
            IsEditable = false;
            LoadError = $"Could not load '{SelectedProfileName}': {ex.Message}";
        }

        ProfileReloaded?.Invoke(this, EventArgs.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
