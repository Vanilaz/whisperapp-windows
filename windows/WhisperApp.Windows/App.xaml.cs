using System.Windows;
using System.Windows.Threading;

namespace WhisperApp;

public partial class App : System.Windows.Application
{
    private TrayIconManager? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Surface unhandled exceptions instead of silently dying (this is a background
        // tray app with no window to show a crash dialog by default).
        DispatcherUnhandledException += (_, args) =>
        {
            System.Diagnostics.Debug.WriteLine($"[Whisper] Unhandled exception: {args.Exception}");
            args.Handled = true;
        };

        _tray = new TrayIconManager();
        _tray.Initialize();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        base.OnExit(e);
    }
}
