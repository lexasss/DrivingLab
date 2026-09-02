using System.Windows.Controls;

namespace ClientExample.Widgets;

public partial class SoundPlayer : UserControl
{
    public SoundPlayer()
    {
        InitializeComponent();
    }

    private async void rdbPlayFile_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        await Task.Delay(300);
        txtFilename.Focus();
    }
}
