using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace WhisperApp.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version != null ? $"Version {version.Major}.{version.Minor}" : "Version 1.0";

        try
        {
            AppIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/icon.ico"));
        }
        catch
        {
            // Fall back silently — a missing icon shouldn't block showing the About window.
        }
    }

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
