using System.Windows;
using System.Windows.Controls;
using K617Mod.Core.State;

namespace K617Mod.Ui;

/// <summary>
/// Pick an axis on the left, reshape its response curve on the right.
/// </summary>
public partial class CurvePage : UserControl
{
    private readonly ProfileSession _session = ProfileSession.Current;

    /// <summary>
    /// Set while the editor is being refilled programmatically, so the
    /// resulting CurveChanged doesn't get mistaken for the person
    /// editing and mark the profile dirty.
    /// </summary>
    private bool _loadingCurve;

    public CurvePage()
    {
        InitializeComponent();
        DataContext = _session;

        AxisList.ItemsSource = CurveAxes.All;
        AxisList.SelectedIndex = 0;

        // Switching profile replaces the curve set underneath us, so the
        // editor has to be refilled from the newly loaded profile.
        _session.ProfileReloaded += (_, _) => LoadSelectedAxis();

        LoadSelectedAxis();
    }

    private CurveAxis? SelectedAxis => AxisList.SelectedItem as CurveAxis;

    private void LoadSelectedAxis()
    {
        var axis = SelectedAxis;
        if (axis is null) return;

        AxisDescription.Text = axis.Description;

        _loadingCurve = true;
        Editor.IsReadOnly = !_session.IsEditable;
        Editor.LoadCurve(_session.GetCurve(axis.Id));
        _loadingCurve = false;
    }

    private void AxisList_SelectionChanged(object sender, SelectionChangedEventArgs e) => LoadSelectedAxis();

    private void Editor_CurveChanged(object? sender, EventArgs e)
    {
        if (_loadingCurve || SelectedAxis is null) return;
        _session.SetCurve(SelectedAxis.Id, Editor.BuildCurve());
    }

    private void ApplyPreset(ResponseCurve curve)
    {
        if (SelectedAxis is null || !_session.IsEditable) return;

        Editor.LoadCurve(curve);
        _session.SetCurve(SelectedAxis.Id, curve);
    }

    private void PresetLinear_Click(object sender, RoutedEventArgs e) =>
        ApplyPreset(ResponseCurve.Linear());

    private void PresetSoft_Click(object sender, RoutedEventArgs e) =>
        ApplyPreset(ResponseCurve.FromExponent(2.0));

    /// <summary>Rises fast then flattens - full output reached earlier.</summary>
    private void PresetSharp_Click(object sender, RoutedEventArgs e) =>
        ApplyPreset(ResponseCurve.FromExponent(0.5));

    /// <summary>Nothing happens until the key is 15% down, then linear from there.</summary>
    private void PresetDeadzone_Click(object sender, RoutedEventArgs e) =>
        ApplyPreset(new ResponseCurve(new[]
        {
            new CurvePoint(0.0, 0.0),
            new CurvePoint(0.15, 0.0),
            new CurvePoint(1.0, 1.0),
        }));

    private void Apply_Click(object sender, RoutedEventArgs e) => _session.Apply();

    private void Revert_Click(object sender, RoutedEventArgs e) => _session.Revert();
}
