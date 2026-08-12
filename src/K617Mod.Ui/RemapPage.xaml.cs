using System.Windows;
using System.Windows.Controls;

namespace K617Mod.Ui;

/// <summary>
/// The Xbox pad diagram, with a clickable key button per control.
/// Clicking one opens a shared picker listing every physical key the
/// profile knows about.
/// </summary>
public partial class RemapPage : UserControl
{
    private readonly ProfileSession _session = ProfileSession.Current;

    /// <summary>Which control the open picker is about to rebind.</summary>
    private ControlBinding? _pickerTarget;

    /// <summary>
    /// Set while the picker's selection is being primed to show the
    /// current key. Without it, priming the list would immediately fire
    /// SelectionChanged and register a no-op edit as an unsaved change.
    /// </summary>
    private bool _primingPicker;

    public RemapPage()
    {
        InitializeComponent();
        DataContext = _session;
    }

    private void KeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not ControlBinding binding)
        {
            return;
        }

        _pickerTarget = binding;
        PickerHeader.Text = $"Bind {binding.DisplayName} to:";

        _primingPicker = true;
        KeyList.SelectedItem = binding.KeyName;
        _primingPicker = false;

        KeyPicker.PlacementTarget = button;
        KeyPicker.IsOpen = true;
    }

    private void KeyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_primingPicker || _pickerTarget is null) return;
        if (KeyList.SelectedItem is not string keyName) return;

        _session.Assign(_pickerTarget, keyName);

        KeyPicker.IsOpen = false;
        _pickerTarget = null;
    }

    private void Apply_Click(object sender, RoutedEventArgs e) => _session.Apply();

    private void Revert_Click(object sender, RoutedEventArgs e) => _session.Revert();
}
