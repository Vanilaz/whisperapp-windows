using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using WhisperApp.Models;
using WhisperApp.Services;
using WhisperApp.Views;
using WpfApplication = System.Windows.Application;

namespace WhisperApp;

/// Owns the tray icon + context menu and wires the DictationController, HotkeyManager, and
/// windows together. Direct analogue of the macOS AppDelegate.
public class TrayIconManager : IDisposable
{
    public DictationController Controller { get; } = new();

    private NotifyIcon? _notifyIcon;
    private ToolStripMenuItem _toggleItem = null!;
    private ToolStripMenuItem _correctionItem = null!;
    private ToolStripMenuItem _thItem = null!;
    private ToolStripMenuItem _autoItem = null!;
    private ToolStripMenuItem _enItem = null!;

    private SettingsWindow? _settingsWindow;
    private AboutWindow? _aboutWindow;
    private FloatingStatusWindow? _floatingWindow;

    public void Initialize()
    {
        KeyStore.Prewarm();

        BuildTrayIcon();
        _floatingWindow = new FloatingStatusWindow(Controller);

        Controller.StageChanged += OnStageChanged;
        Controller.StatusChanged += OnStatusChanged;

        SetupHotkey();
    }

    private void BuildTrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Visible = true,
            Text = "Whisper",
        };

        var menu = new ContextMenuStrip();

        var hk = HotkeyManager.Shared.CurrentConfig.DisplayString;
        _toggleItem = new ToolStripMenuItem($"Start Speaking ({hk})", null, (_, _) => Controller.Toggle());
        menu.Items.Add(_toggleItem);
        menu.Items.Add(new ToolStripSeparator());

        _correctionItem = new ToolStripMenuItem("AI Correction", null, (_, _) =>
        {
            Controller.UseCorrection = !Controller.UseCorrection;
            UpdateStates();
        });
        menu.Items.Add(_correctionItem);
        menu.Items.Add(new ToolStripSeparator());

        var langMenu = new ToolStripMenuItem("Language");
        _thItem = new ToolStripMenuItem("Thai", null, (_, _) => { Controller.Language = "th"; UpdateStates(); });
        _autoItem = new ToolStripMenuItem("Auto", null, (_, _) => { Controller.Language = "auto"; UpdateStates(); });
        _enItem = new ToolStripMenuItem("English", null, (_, _) => { Controller.Language = "en"; UpdateStates(); });
        langMenu.DropDownItems.Add(_thItem);
        langMenu.DropDownItems.Add(_autoItem);
        langMenu.DropDownItems.Add(_enItem);
        menu.Items.Add(langMenu);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()));
        menu.Items.Add(new ToolStripMenuItem("About Whisper", null, (_, _) => OpenAbout()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => WpfApplication.Current.Shutdown()));

        menu.Opening += (_, _) => UpdateStates();

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) Controller.Toggle();
        };

        UpdateStates();
    }

    private void SetupHotkey()
    {
        var mgr = HotkeyManager.Shared;

        mgr.OnKeyDown = () => WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (mgr.CurrentConfig.IsHoldMode) Controller.Start();
            else Controller.Toggle();
        });

        mgr.OnKeyUp = () => WpfApplication.Current.Dispatcher.Invoke(() => Controller.Stop());
        mgr.IsActive = () => Controller.IsRecording;

        mgr.Start();
    }

    private void OnStageChanged(Stage stage)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var hk = HotkeyManager.Shared.CurrentConfig.DisplayString;
            _toggleItem.Text = stage.Kind == StageKind.Recording
                ? $"Stop Speaking ({hk})"
                : $"Start Speaking ({hk})";
        });
    }

    private void OnStatusChanged(string status)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (_notifyIcon == null) return;
            var text = string.IsNullOrEmpty(status) ? "Whisper" : status;
            // NotifyIcon.Text is limited to 63 characters by the Windows shell API.
            _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
        });
    }

    private void UpdateStates()
    {
        _correctionItem.Checked = Controller.UseCorrection;
        _correctionItem.Text = $"AI Correction ({LLMSettings.Current.Name})";

        var hk = HotkeyManager.Shared.CurrentConfig.DisplayString;
        _toggleItem.Text = Controller.IsRecording ? $"Stop Speaking ({hk})" : $"Start Speaking ({hk})";

        _thItem.Checked = Controller.Language == "th";
        _autoItem.Checked = Controller.Language == "auto";
        _enItem.Checked = Controller.Language == "en";
    }

    private void OpenSettings()
    {
        if (_settingsWindow == null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OpenAbout()
    {
        if (_aboutWindow == null)
        {
            _aboutWindow = new AboutWindow();
            _aboutWindow.Closed += (_, _) => _aboutWindow = null;
        }
        _aboutWindow.Show();
        _aboutWindow.Activate();
    }

    private static Icon LoadIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/icon.ico");
            var info = WpfApplication.GetResourceStream(uri);
            if (info != null) return new Icon(info.Stream);
        }
        catch
        {
            // Fall through to system default below.
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        HotkeyManager.Shared.Stop();
        _notifyIcon?.Dispose();
        _floatingWindow?.Close();
    }
}
