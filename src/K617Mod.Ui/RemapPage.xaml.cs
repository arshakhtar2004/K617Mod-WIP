using System.Windows.Controls;

namespace K617Mod.Ui;

/// <summary>
/// The Xbox pad diagram with a callout per control showing the key
/// currently bound to it.
/// </summary>
public partial class RemapPage : UserControl
{
    public RemapPage()
    {
        InitializeComponent();
        DataContext = new RemapViewModel();
    }
}
