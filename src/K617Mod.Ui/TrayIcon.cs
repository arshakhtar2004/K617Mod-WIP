using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace K617Mod.Ui;

/// <summary>
/// The tray icon, and with a silent launch the only thing the person sees.
/// A pure view over ModController: it renders status and forwards clicks,
/// and holds no pipeline logic of its own.
///
/// Colour scheme carried over from the Python build, because it was
/// already learned there and there's no reason to make people relearn it:
/// green = running and suppressing, orange = running but keys ALSO type,
/// red = stopped or broken.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    /// <summary>
    /// Windows tray tooltips hard-cap at 128 characters. The Python build
    /// hit this: pystray crashed mid-setup on a longer string rather than
    /// truncating. Enforced here for the same reason.
    /// </summary>
    private const int TooltipLimit = 127;

    private readonly ModController _controller;
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _toggleItem;
    private readonly List<Icon> _ownedIcons = new();

    private ModStatus _lastNotifiedStatus = ModStatus.Stopped;

    public TrayIcon(ModController controller)
    {
        _controller = controller;

        _toggleItem = new WinForms.ToolStripMenuItem("Stop mod", null, (_, _) => ToggleMod());

        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(new WinForms.ToolStripMenuItem("Open config", null, (_, _) => ShowWindow()));
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Quit", null, (_, _) => QuitApp()));

        _icon = new WinForms.NotifyIcon
        {
            Icon = MakeStatusIcon(Color.Red),
            ContextMenuStrip = menu,
            Visible = true,
            Text = "K617 Mod",
        };

        // Double-click is what people try first on a tray icon, so it does
        // the obvious thing rather than nothing.
        _icon.DoubleClick += (_, _) => ShowWindow();

        _controller.StatusChanged += OnStatusChanged;
        Refresh();
    }

    private void OnStatusChanged(object? sender, EventArgs e)
    {
        // StatusChanged fires from the watchdog thread; the tray icon is a
        // WinForms control and has to be touched from the UI thread.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            Refresh();
        }
        else
        {
            // Wrapped in an explicit Action rather than passed as a method
            // group: BeginInvoke takes a Delegate, and a method group won't
            // convert to that on its own.
            dispatcher.BeginInvoke(new Action(Refresh));
        }
    }

    private void Refresh()
    {
        var (colour, headline) = _controller.Status switch
        {
            ModStatus.Running => (Color.LimeGreen, "Running - K617 keys are controller input only."),
            ModStatus.SuppressionFailed => (Color.Orange, "Running, but suppression FAILED - keys also type."),
            ModStatus.DeviceAsleep => (Color.Orange, "Stopped - keyboard needs waking."),
            ModStatus.Error => (Color.Red, "Could not start."),
            _ => (Color.Red, "Stopped - keyboard types normally."),
        };

        var previous = _icon.Icon;
        _icon.Icon = MakeStatusIcon(colour);
        DisposeIcon(previous);

        _icon.Text = Truncate($"K617 Mod - {headline}");
        _toggleItem.Text = _controller.IsRunning ? "Stop mod" : "Start mod";

        // A balloon only for states the person has to act on, and only on
        // entering them - a silent launch means the tray colour alone is
        // easy to miss, but a notification on every tick would be worse.
        if (_controller.Status != _lastNotifiedStatus)
        {
            _lastNotifiedStatus = _controller.Status;

            if (_controller.Status is ModStatus.DeviceAsleep or ModStatus.Error or ModStatus.SuppressionFailed)
            {
                _icon.ShowBalloonTip(
                    10000,
                    "K617 Mod",
                    Truncate(string.IsNullOrWhiteSpace(_controller.StatusDetail)
                        ? headline
                        : _controller.StatusDetail),
                    WinForms.ToolTipIcon.Warning);
            }
        }
    }

    private void ToggleMod()
    {
        if (_controller.IsRunning)
        {
            _controller.Stop();
        }
        else
        {
            _controller.Start();
        }

        Refresh();
    }

    private void ShowWindow()
    {
        // The window is created on demand rather than at startup: a launch
        // that shows nothing shouldn't pay to build a UI nobody asked for.
        var window = Application.Current.MainWindow;

        if (window is null)
        {
            window = new MainWindow(_controller);
            Application.Current.MainWindow = window;
        }

        window.Show();

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    private static void QuitApp() => Application.Current.Shutdown();

    /// <summary>
    /// Draws the status dot at runtime instead of shipping .ico files, so
    /// adding a state is a colour rather than an asset. Same approach as
    /// the Python build's _make_icon_image().
    /// </summary>
    private Icon MakeStatusIcon(Color colour)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(colour);
            g.FillEllipse(brush, 3, 3, 26, 26);
        }

        var handle = bitmap.GetHicon();
        try
        {
            // Icon.FromHandle does not own the handle, so the clone is what
            // makes this safe to keep after DestroyIcon runs. Without it
            // the icon becomes a dangling handle the moment it's freed.
            var icon = (Icon)Icon.FromHandle(handle).Clone();
            _ownedIcons.Add(icon);
            return icon;
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private void DisposeIcon(Icon? icon)
    {
        if (icon is null) return;
        _ownedIcons.Remove(icon);
        icon.Dispose();
    }

    private static string Truncate(string text) =>
        text.Length <= TooltipLimit ? text : text[..(TooltipLimit - 3)] + "...";

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public void Dispose()
    {
        _controller.StatusChanged -= OnStatusChanged;

        _icon.Visible = false;
        _icon.Dispose();

        foreach (var icon in _ownedIcons)
        {
            icon.Dispose();
        }

        _ownedIcons.Clear();
    }
}
