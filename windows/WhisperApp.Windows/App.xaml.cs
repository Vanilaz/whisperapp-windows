using System.Windows;
using System.Windows.Threading;

namespace WhisperApp;

public partial class App : System.Windows.Application
{
    private TrayIconManager? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // This is a background tray app with no window — without this, any unhandled
        // exception (here or later, e.g. inside an event handler) kills the process with
        // no dialog and no console output, and it just silently vanishes from the taskbar.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Whisper hit an unexpected error:\n\n{args.Exception}",
                "Whisper — error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            _tray = new TrayIconManager();
            _tray.Initialize();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Whisper failed to start:\n\n{ex}",
                "Whisper — startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
