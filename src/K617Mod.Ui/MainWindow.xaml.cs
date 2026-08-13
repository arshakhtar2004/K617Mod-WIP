using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace K617Mod.Ui;

/// <summary>
/// The single window the whole config app lives in.
///
/// Closing it hides it rather than destroying it: the mod keeps running,
/// and reopening from the tray returns to the same window with any pending
/// edits intact instead of a fresh one. Quitting for real is the tray menu's
/// job, which is the one place it's unambiguous.
///
/// Also a second, in-window view over ModController alongside the tray -
/// the mod switch here and the tray's "Start mod"/"Stop mod" item are two
/// controls over the same Start()/Stop(), so either always agrees with
/// the other.
/// </summary>
public partial class MainWindow : Window
{
    private readonly ModController _controller;

    public MainWindow(ModController controller)
    {
        _controller = controller;
        InitializeComponent();

        _controller.StatusChanged += OnStatusChanged;
        RefreshStatus();
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        // StatusChanged fires from the watchdog thread; this is a WPF
        // control and has to be touched from the UI thread - same
        // reasoning as TrayIcon.OnStatusChanged.
        if (Dispatcher.CheckAccess())
        {
            RefreshStatus();
        }
        else
        {
            Dispatcher.BeginInvoke(new Action(RefreshStatus));
        }
    }

    private void RefreshStatus()
    {
        ModToggle.IsChecked = _controller.IsRunning;

        // Intentionally duplicated from TrayIcon.Refresh()'s switch rather
        // than shared - WPF and WinForms use different Color types, so a
        // shared helper would need to hand back something more abstract
        // than either. Worth factoring out if a third view ever wants the
        // same text; not worth it for two.
        var (dotColor, headline) = _controller.Status switch
        {
            ModStatus.Running => (Colors.LimeGreen, "Running - K617 keys are controller input only."),
            ModStatus.SuppressionFailed => (Colors.Orange, "Running, but suppression FAILED - keys also type."),
            ModStatus.DeviceAsleep => (Colors.Orange, "Stopped - keyboard needs waking."),
            ModStatus.Error => (Colors.OrangeRed, "Could not start."),
            _ => (Colors.Gray, "Stopped - keyboard types normally."),
        };

        StatusDot.Fill = new SolidColorBrush(dotColor);
        StatusText.Text = string.IsNullOrWhiteSpace(_controller.StatusDetail) ? headline : _controller.StatusDetail;
    }

    private void ModToggle_Click(object sender, RoutedEventArgs e)
    {
        // Reads IsRunning (the pipeline's actual state), not the button's
        // own IsChecked - WPF flips IsChecked before Click fires, so
        // trusting the button here would double-toggle. If Start()/Stop()
        // doesn't end up where the click expected (e.g. Start() fails),
        // RefreshStatus() snaps the switch back to match reality rather
        // than leaving it showing a state the pipeline isn't actually in.
        if (_controller.IsRunning)
        {
            _controller.Stop();
        }
        else
        {
            _controller.Start();
        }

        RefreshStatus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
