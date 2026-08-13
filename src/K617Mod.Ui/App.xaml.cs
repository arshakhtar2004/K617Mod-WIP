using System.Threading;
using System.Windows;

namespace K617Mod.Ui;

/// <summary>
/// Application entry point, and the whole of the startup story: launching
/// the exe shows no window, puts a status icon in the tray, and starts the
/// mod if that's the mode it was last left in. The config window is opened
/// from the tray when it's wanted.
///
/// Shutdown lives here too, so suppression is released and the virtual pad
/// disconnected however the app ends - tray Quit, a crash on the UI thread,
/// or Windows closing the session. Leaving suppression attached would leave
/// a keyboard that types nothing, which is the worst failure this app has.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Guards against a second copy running. Two instances would fight over
    /// the same HID interface and each create their own virtual pad, which
    /// surfaces as an opaque "device in use" error rather than anything
    /// that explains itself.
    /// </summary>
    private const string SingleInstanceMutexName = @"Global\K617Mod.Ui.SingleInstance";

    private Mutex? _instanceMutex;
    private ModController? _controller;
    private TrayIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _instanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "K617 Mod is already running - look for its icon in the system tray.",
                "K617 Mod",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            // Release the keyboard before anything else. A crash that left
            // suppression attached would look like broken hardware.
            _controller?.Stop();

            MessageBox.Show(
                "K617 Mod hit an unexpected error and has released the keyboard.\n\n" + args.Exception.Message,
                "K617 Mod",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            args.Handled = true;
            Shutdown();
        };

        _controller = new ModController();
        _tray = new TrayIcon(_controller);

        // Connect the profile editor to the running mod. This is the only
        // place the two know about each other: the session's job is
        // editing profiles, the controller's job is deciding what a
        // profile change means for a pipeline that may or may not be
        // running. Wiring them here rather than having the session call
        // the controller directly keeps the settings pages testable
        // without a HID device or a ViGEm driver anywhere in reach.
        //
        // Done before the auto-start below, so the first Start() already
        // has the right profile instead of starting on one and being
        // corrected a moment later.
        var session = ProfileSession.Current;
        ApplyLiveProfile(session);
        session.LiveProfileChanged += (_, _) => ApplyLiveProfile(session);

        // Auto-start only if that's the mode the person left it in last
        // time they explicitly toggled it - a fresh install with no saved
        // preference still starts, matching the original "clicking the
        // exe is the whole interaction" design.
        if (_controller.ShouldStartOnLaunch())
        {
            _controller.Start();
        }
    }

    /// <summary>
    /// Pushes the session's selected profile into the mod, and puts any
    /// failure back in front of the person rather than swallowing it.
    /// A profile that saves fine but won't load is the case worth
    /// catching: without this the editor would look like it had worked
    /// while the mod quietly carried on with the previous settings.
    /// </summary>
    private void ApplyLiveProfile(ProfileSession session)
    {
        var error = _controller?.ApplyProfile(session.SelectedProfileName);
        if (error is not null)
        {
            session.ReportError(error);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _controller?.Dispose();

        _instanceMutex?.Dispose();
        _instanceMutex = null;

        base.OnExit(e);
    }
}
