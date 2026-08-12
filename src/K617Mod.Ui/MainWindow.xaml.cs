using System.Windows;

namespace K617Mod.Ui;

/// <summary>
/// The single window the whole config app lives in. Empty at this stage
/// - it exists to prove the WPF build works before anything depends on
/// it.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
