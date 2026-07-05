using System.IO;
using System.Text.RegularExpressions;
using WhisperApp.Models;
using WhisperApp.Services;

namespace WhisperApp;

/// Orchestrates everything: record -> transcribe (cloud) -> correct (LLM) -> paste into the
/// focused app. Direct port of the macOS DictationController; local whisper.cpp fallback is
/// dropped since the current (v1.2) app is Groq-cloud-only — see CLAUDE.md.
///
/// Events may fire from background threads (NAudio callbacks, HTTP continuations) — subscribers
/// must marshal to the UI thread themselves.
public class DictationController
{
    public event Action<Stage>? StageChanged;
    public event Action<string>? StatusChanged;

    public bool IsRecording => _recorder.IsRecording;
    public bool UseCorrection { get; set; } = true;
    public string Language { get; set; } = "th";

    public Stage CurrentStage { get; private set; } = Stage.Idle;
    public string Status { get; private set; } = "";

    public AudioRecorder Recorder => _recorder;

    private readonly AudioRecorder _recorder = new();
    private readonly CloudTranscriptionService _cloud = new();
    private readonly TextCorrectionService _correction = new();
    private volatile bool _processing;

    public DictationController()
    {
        _recorder.RecordingStopped += path => _ = HandleAudioAsync(path);
    }

    public void Toggle()
    {
        if (_recorder.IsRecording) Stop(); else Start();
    }

    public void Start()
    {
        if (_processing || _recorder.IsRecording) return;
        _recorder.StartRecording();
        if (_recorder.IsRecording)
        {
            SetStatus("Listening…");
            SetStage(Stage.Recording);
        }
        else
        {
            const string msg = "Microphone unavailable";
            SetStatus(msg);
            var errorStage = Stage.Error(msg);
            SetStage(errorStage);
            // Start() isn't async, so fire-and-forget the revert - otherwise this overlay
            // would stay stuck forever (unlike every other error/done stage, which already
            // schedules its own return to idle).
            _ = RevertToIdleAfterDelay(errorStage, TimeSpan.FromMilliseconds(1500));
        }
    }

    public void Stop()
    {
        if (!_recorder.IsRecording) return;
        _recorder.StopRecording();
        SetStatus("Processing…");
        SetStage(Stage.Transcribing);
    }

    private async Task HandleAudioAsync(string filePath)
    {
        _processing = true;
        try
        {
            await RunPipelineAsync(filePath);
        }
        catch (Exception ex)
        {
            // Belt and suspenders: TranscribeAsync/CorrectAsync already catch their own
            // errors and return null, but if anything unexpected still throws here, we must
            // not leave _processing stuck true forever - that would permanently disable the
            // hotkey ("Start" no-ops while _processing is true) until the app is restarted.
            System.Diagnostics.Debug.WriteLine($"[DictationController] Unexpected error: {ex}");
            const string msg = "Unexpected error";
            SetStatus(msg);
            var errorStage = Stage.Error(msg);
            SetStage(errorStage);
            await RevertToIdleAfterDelay(errorStage, TimeSpan.FromMilliseconds(1500));
        }
        finally
        {
            _processing = false;
        }
    }

    private async Task RunPipelineAsync(string filePath)
    {
        var lang = Language;

        SetStatus("Transcribing…");
        SetStage(Stage.Transcribing);

        string? raw;
        try
        {
            raw = await _cloud.TranscribeAsync(filePath, lang);
        }
        finally
        {
            try { File.Delete(filePath); } catch { /* best effort cleanup */ }
        }

        var text = raw != null ? StripSoundAnnotations(raw) : "";
        if (string.IsNullOrWhiteSpace(text))
        {
            const string msg = "No audio detected";
            SetStatus(msg);
            var errorStage = Stage.Error(msg);
            SetStage(errorStage);
            await RevertToIdleAfterDelay(errorStage, TimeSpan.FromMilliseconds(1500));
            return;
        }

        string finalText = text;
        if (UseCorrection)
        {
            SetStatus("AI correction…");
            SetStage(Stage.Correcting);
            var corrected = await _correction.CorrectAsync(text, lang);
            finalText = corrected ?? text;
        }

        var snippet = finalText.Length > 28 ? finalText[..28] : finalText;
        SetStatus(snippet);
        var doneStage = Stage.Done(snippet);
        SetStage(doneStage);
        Paster.Paste(finalText);

        await RevertToIdleAfterDelay(doneStage, TimeSpan.FromMilliseconds(1200));
    }

    /// Waits, then returns to idle only if the stage hasn't already moved on to something
    /// else in the meantime (e.g. a new recording started before the delay elapsed).
    private async Task RevertToIdleAfterDelay(Stage expected, TimeSpan delay)
    {
        await Task.Delay(delay);
        if (CurrentStage == expected) SetStage(Stage.Idle);
    }

    private void SetStage(Stage s)
    {
        CurrentStage = s;
        StageChanged?.Invoke(s);
    }

    private void SetStatus(string s)
    {
        Status = s;
        StatusChanged?.Invoke(s);
    }

    /// Strip sound/event captions STT sometimes injects, e.g. (wind noise) [applause] *laughs*.
    private static string StripSoundAnnotations(string text)
    {
        string[] patterns =
        {
            @"\([^\)]*\)",   // ( ... )   ASCII
            "（[^）]*）",         // （ ... ） fullwidth
            @"\[[^\]]*\]",   // [ ... ]
            "【[^】]*】",         // 【 ... 】
            @"\*[^*]*\*",     // * ... *
            "‹[^›]*›",           // ‹ ... ›
            "«[^»]*»",           // « ... »
        };

        var result = text;
        foreach (var p in patterns) result = Regex.Replace(result, p, " ");

        result = Regex.Replace(result, @"\s{2,}", " ");
        result = Regex.Replace(result, @"\s+([,.!?])", "$1");
        return result.Trim();
    }
}
