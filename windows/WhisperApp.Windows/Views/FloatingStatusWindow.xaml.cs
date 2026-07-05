using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using WhisperApp.Models;

namespace WhisperApp.Views;

/// Floating overlay that shows the current processing stage — live waveform while recording,
/// or an icon + status pill while transcribing/correcting/done/erroring. Borderless, click-through,
/// never steals focus (mirrors the macOS NSPanel with .nonactivatingPanel + ignoresMouseEvents).
public partial class FloatingStatusWindow : Window
{
    private readonly DictationController _controller;
    private readonly ObservableCollection<double> _bars = new();
    private readonly DispatcherTimer _waveformTimer;
    private readonly Random _rng = new();
    private float _currentLevel;
    private DispatcherTimer? _hideTimer;

    public FloatingStatusWindow(DictationController controller)
    {
        InitializeComponent();
        _controller = controller;

        for (int i = 0; i < 32; i++) _bars.Add(4);
        WaveformBars.ItemsSource = _bars;

        _waveformTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _waveformTimer.Tick += (_, _) => AdvanceWaveform();

        _controller.Recorder.LevelChanged += level => _currentLevel = level;
        _controller.StageChanged += stage => Dispatcher.Invoke(() => Render(stage));

        // Starts hidden (both panels are Collapsed in XAML) — no need to call Hide() before
        // the window has ever been shown.
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = GetWindowLong(hwnd, GwlExStyle);
        // Click-through + never activate, so it never steals focus from the app you're dictating into.
        SetWindowLong(hwnd, GwlExStyle, exStyle | WsExTransparent | WsExNoActivate);
    }

    private void Render(Stage stage)
    {
        _hideTimer?.Stop();

        switch (stage.Kind)
        {
            case StageKind.Recording:
                WaveformBars.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Collapsed;
                _waveformTimer.Start();
                ShowPanel();
                break;

            case StageKind.Transcribing:
                ShowStatus(spin: true, icon: "", text: "Transcribing…");
                break;

            case StageKind.Correcting:
                ShowStatus(spin: true, icon: "", text: "Fixing text…");
                break;

            case StageKind.Done:
                ShowStatus(spin: false, icon: "✓", text: string.IsNullOrEmpty(stage.Message) ? "Done" : stage.Message);
                break;

            case StageKind.Error:
                ShowStatus(spin: false, icon: "⚠", text: stage.Message);
                break;

            case StageKind.Idle:
            default:
                _waveformTimer.Stop();
                HideDelayed();
                break;
        }
    }

    private void ShowStatus(bool spin, string icon, string text)
    {
        _waveformTimer.Stop();
        WaveformBars.Visibility = Visibility.Collapsed;
        StatusPanel.Visibility = Visibility.Visible;

        Spinner.Visibility = spin ? Visibility.Visible : Visibility.Collapsed;
        StageIcon.Visibility = spin ? Visibility.Collapsed : Visibility.Visible;
        StageIcon.Text = icon;
        StatusText.Text = text;

        if (spin) StartSpin(); else StopSpin();

        ShowPanel();
    }

    private void StartSpin()
    {
        var anim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        SpinnerRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, anim);
    }

    private void StopSpin()
    {
        SpinnerRotation.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
    }

    private void AdvanceWaveform()
    {
        double level = _currentLevel;
        _bars.RemoveAt(0);
        double jitter = 0.5 + _rng.NextDouble() * 0.6; // 0.5..1.1
        double h = Math.Min(1, Math.Max(0.05, level * jitter)) * 56;
        _bars.Add(Math.Max(4, h));
    }

    private void ShowPanel()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Bottom - 130 - Height;
        if (!IsVisible) Show();
    }

    /// Slight delay before hiding so the waveform can fade/settle, matching macOS.
    private void HideDelayed()
    {
        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer!.Stop();
            if (!_controller.IsRecording) Hide();
        };
        _hideTimer.Start();
    }

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExNoActivate = 0x08000000;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
