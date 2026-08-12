using System.Windows;

namespace K617Mod.Ui;

/// <summary>
/// Application entry point. Empty for now - App.xaml's StartupUri opens
/// MainWindow directly. Once the orchestrator is wired in, this is where
/// startup/shutdown live, so the pipeline gets stopped and the keyboard
/// released even if the window is closed abruptly.
/// </summary>
public partial class App : Application
{
}
