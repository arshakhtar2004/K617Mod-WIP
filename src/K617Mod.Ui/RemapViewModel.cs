using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using K617Mod.Core.Mapping;
using K617Mod.Core.Persistence;

namespace K617Mod.Ui;

/// <summary>
/// One Xbox control and the physical key currently bound to it. Raises
/// change notifications so reassigning a key updates the button on the
/// diagram without the page rebuilding itself.
/// </summary>
public sealed class ControlBinding : INotifyPropertyChanged
{
    private string _keyName = RemapViewModel.Unassigned;

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
/// Backs the remap page: which profile is selected, what each Xbox
/// control is bound to in it, and which physical keys are available to
/// bind.
///
/// Edits live only in memory. Nothing is written to the profile until
/// Apply is pressed, which is what makes it safe to change several
/// bindings and then commit them together - or walk away and lose
/// nothing but the pending edits.
/// </summary>
public sealed class RemapViewModel : INotifyPropertyChanged
{
    public const string Unassigned = "—";

    private readonly IProfileStore _store;
    private string _selectedProfileName = ProfileBootstrapper.DefaultProfileName;
    private bool _isEditable;
    private bool _hasUnsavedChanges;
    private string _loadError = string.Empty;

    public RemapViewModel()
    {
        var appDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "K617Mod");
        var defaultProfilePath = Path.Combine(
            AppContext.BaseDirectory, "Mapping", "Data", "profile.default.json");

        _store = new JsonProfileStore(appDataRoot);

        // Every control exists up front, unmapped ones holding a dash,
        // so a binding on the diagram can never miss and render blank.
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

    /// <summary>Action id -> current binding. Stable for the lifetime of the page.</summary>
    public Dictionary<string, ControlBinding> Bindings { get; }

    /// <summary>Physical keys this profile knows a HID position for, plus an unassign option.</summary>
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
        }
    }

    /// <summary>False while the read-only Default profile is selected.</summary>
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

    /// <summary>Points a control at a different key, or at nothing.</summary>
    public void Assign(ControlBinding target, string keyName)
    {
        if (!IsEditable || target.KeyName == keyName) return;

        // A physical key can only drive one control, so binding a key
        // that's already in use releases it from wherever it was. Doing
        // this silently for now - surfacing the clash to the person is a
        // later job.
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

    /// <summary>Writes pending edits into the selected profile.</summary>
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

            _store.SaveProfile(profile);
            _store.SetLastActiveProfileName(SelectedProfileName);
            HasUnsavedChanges = false;
            LoadError = string.Empty;
        }
        catch (Exception ex)
        {
            LoadError = $"Could not save '{SelectedProfileName}': {ex.Message}";
        }
    }

    /// <summary>Throws away pending edits and re-reads the profile from disk.</summary>
    public void Revert() => LoadSelectedProfile();

    private void LoadSelectedProfile()
    {
        foreach (var binding in Bindings.Values)
        {
            binding.KeyName = Unassigned;
        }

        AvailableKeys.Clear();
        AvailableKeys.Add(Unassigned);

        try
        {
            var profile = _store.LoadProfile(SelectedProfileName);

            IsEditable = !profile.IsReadOnly && ProfileBootstrapper.IsEditable(SelectedProfileName);

            foreach (var keyName in profile.KeyMapping.KeyHidMap.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                AvailableKeys.Add(keyName);
            }

            // The profile stores key -> action because that's the
            // direction the HID pipeline looks things up. The page needs
            // action -> key, so invert it once here rather than searching
            // the map for every control on screen.
            foreach (var (keyName, entry) in profile.KeyMapping.ControllerMap)
            {
                if (!string.IsNullOrWhiteSpace(entry.Action)
                    && Bindings.TryGetValue(entry.Action, out var binding))
                {
                    binding.KeyName = keyName;
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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
