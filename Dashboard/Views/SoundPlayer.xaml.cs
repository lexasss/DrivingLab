using System.Windows.Controls;

namespace Dashboard.Views;

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
