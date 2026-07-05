using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WhisperApp.Models;

namespace WhisperApp.Views;

/// Captures a key combination (or a lone modifier key, like Right Ctrl) and displays it.
/// Windows delivers modifier keys through ordinary KeyDown/KeyUp — no special-casing needed
/// like macOS's separate flagsChanged handler.
public partial class HotkeyRecorderControl : System.Windows.Controls.UserControl
{
    public event Action<HotkeyConfig>? Captured;
    public event Action? RecordingCancelled;

    private bool _isRecording;
    private HotkeyConfig _current = HotkeyConfig.Default;

    public HotkeyRecorderControl()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    public void SetConfig(HotkeyConfig config)
    {
        _current = config;
        UpdateDisplay();
    }

    public void BeginRecording()
    {
        _isRecording = true;
        Focus();
        Keyboard.Focus(this);
        UpdateDisplay();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.Escape)
        {
            _isRecording = false;
            UpdateDisplay();
            RecordingCancelled?.Invoke();
            return;
        }

        int vk = KeyInterop.VirtualKeyFromKey(key);
        bool isModifierOnly = VK.ModifierKeyCodes.Contains(vk);

        uint mods = 0;
        if (!isModifierOnly)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods |= (uint)HotkeyConfig.Mod.Control;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods |= (uint)HotkeyConfig.Mod.Alt;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods |= (uint)HotkeyConfig.Mod.Shift;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods |= (uint)HotkeyConfig.Mod.Win;
        }

        var config = new HotkeyConfig
        {
            KeyCode = vk,
            Modifiers = mods,
            IsModifierOnly = isModifierOnly,
            IsHoldMode = _current.IsHoldMode,
        };

        _isRecording = false;
        _current = config;
        UpdateDisplay();
        Captured?.Invoke(config);
    }

    private void UpdateDisplay()
    {
        if (_isRecording)
        {
            DisplayText.Text = "Press keys…";
            Root.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(38, 0, 122, 255));
            Root.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
            Root.BorderThickness = new Thickness(2);
        }
        else
        {
            DisplayText.Text = _current.DisplayString;
            Root.ClearValue(Border.BackgroundProperty);
            Root.ClearValue(Border.BorderBrushProperty);
            Root.BorderThickness = new Thickness(1);
        }
    }
}
