using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using System.Windows.Threading;
using WhisperApp.Models;
using WhisperApp.Services;

namespace WhisperApp.Views;

/// Groq-only settings window (mirrors the current simplified macOS SettingsView — one shared
/// key drives both STT and AI correction) plus the global hotkey recorder.
public partial class SettingsWindow : Window
{
    private static readonly HttpClient Http = new();

    private readonly STTProvider _sttProvider = STTRegistry.Provider("groq");
    private readonly LLMProvider _llmProvider = LLMRegistry.Provider("groq");
    private HotkeyConfig _hotkeyConfig = HotkeyManager.Shared.CurrentConfig;
    private DispatcherTimer? _messageClearTimer;

    public SettingsWindow()
    {
        InitializeComponent();

        GroqDescription.Text =
            $"Used for both transcription ({_sttProvider.DefaultModel}) and AI correction ({_llmProvider.DefaultModel})";

        HotkeyRecorder.SetConfig(_hotkeyConfig);
        HoldModeCheckBox.IsChecked = _hotkeyConfig.IsHoldMode;
        HotkeyRecorder.Captured += OnHotkeyCaptured;
        HotkeyRecorder.RecordingCancelled += () => ChangeButton.Content = "Change";

        LoadKey();
    }

    private void OnChangeHotkeyClick(object sender, RoutedEventArgs e)
    {
        ChangeButton.Content = "Listening…";
        HotkeyRecorder.BeginRecording();
    }

    private void OnResetHotkeyClick(object sender, RoutedEventArgs e)
    {
        _hotkeyConfig = HotkeyConfig.Default;
        HotkeyRecorder.SetConfig(_hotkeyConfig);
        HoldModeCheckBox.IsChecked = _hotkeyConfig.IsHoldMode;
        HotkeyManager.Shared.UpdateConfig(_hotkeyConfig);
    }

    private void OnHotkeyCaptured(HotkeyConfig config)
    {
        ChangeButton.Content = "Change";
        _hotkeyConfig = config;
        HotkeyManager.Shared.UpdateConfig(_hotkeyConfig);
    }

    private void OnHoldModeChanged(object sender, RoutedEventArgs e)
    {
        _hotkeyConfig.IsHoldMode = HoldModeCheckBox.IsChecked == true;
        HotkeyManager.Shared.UpdateConfig(_hotkeyConfig);
    }

    private void LoadKey()
    {
        var key = STTSettings.SavedKeyFile(_sttProvider);
        if (string.IsNullOrEmpty(key)) key = LLMSettings.SavedKeyFile(_llmProvider);
        GroqKeyBox.Password = key;
    }

    private void ApplyKey()
    {
        STTSettings.ProviderId = "groq";
        LLMSettings.ProviderId = "groq";
        var t = GroqKeyBox.Password.Trim();
        if (!string.IsNullOrEmpty(t))
        {
            STTSettings.SaveKey(t, _sttProvider);
            LLMSettings.SaveKey(t, _llmProvider);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        ApplyKey();
        ShowMessage("✓ Saved");
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        ApplyKey();
        var key = STTSettings.Key(_sttProvider);
        if (key == null)
        {
            ShowMessage("Enter API key first");
            return;
        }

        ShowMessage("Testing…", autoClear: false);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.groq.com/openai/v1/models");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var resp = await Http.SendAsync(req);
            ShowMessage(resp.IsSuccessStatusCode ? "✓ Key is valid" : $"Invalid key (code {(int)resp.StatusCode})");
        }
        catch (Exception ex)
        {
            ShowMessage($"Request failed: {ex.Message}");
        }
    }

    private void ShowMessage(string text, bool autoClear = true)
    {
        GroqMessage.Text = text;
        _messageClearTimer?.Stop();
        if (!autoClear) return;

        _messageClearTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _messageClearTimer.Tick += (_, _) =>
        {
            _messageClearTimer!.Stop();
            GroqMessage.Text = "";
        };
        _messageClearTimer.Start();
    }
}
