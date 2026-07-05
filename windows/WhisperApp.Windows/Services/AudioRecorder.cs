using System.IO;
using NAudio.Wave;

namespace WhisperApp.Services;

/// Records the default microphone directly at 16kHz mono 16-bit PCM (the format Whisper-based
/// STT expects), using NAudio's waveIn (mme) API — the driver's wave mapper handles the format
/// conversion, so no manual resampling is needed (unlike macOS, which has to resample from the
/// hardware's native format via AVAudioConverter).
public class AudioRecorder
{
    public bool IsRecording { get; private set; }

    /// Raised with the finished WAV file's path once recording stops.
    public event Action<string>? RecordingStopped;
    /// Real-time 0..1 audio level for the waveform display.
    public event Action<float>? LevelChanged;

    private static readonly WaveFormat TargetFormat = new(16000, 16, 1);

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _writer;
    private string? _tempFilePath;

    public void StartRecording()
    {
        if (IsRecording) return;

        try
        {
            _tempFilePath = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid():N}.wav");

            _waveIn = new WaveInEvent
            {
                WaveFormat = TargetFormat,
                BufferMilliseconds = 50,
            };
            _writer = new WaveFileWriter(_tempFilePath, TargetFormat);

            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnNAudioRecordingStopped;

            _waveIn.StartRecording();
            IsRecording = true;
            System.Diagnostics.Debug.WriteLine($"[AudioRecorder] Recording started -> {_tempFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioRecorder] Failed to start: {ex.Message}");
            CleanupAfterFailure();
        }
    }

    public void StopRecording()
    {
        if (!IsRecording || _waveIn == null) return;
        // Actual teardown + RecordingStopped event happen in OnNAudioRecordingStopped.
        _waveIn.StopRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        try
        {
            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioRecorder] Write error: {ex.Message}");
        }

        int sampleCount = e.BytesRecorded / 2; // 16-bit samples
        if (sampleCount <= 0) return;

        double sumSquares = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(e.Buffer, i * 2);
            double norm = sample / 32768.0;
            sumSquares += norm * norm;
        }
        float rms = (float)Math.Sqrt(sumSquares / sampleCount);
        float level = Math.Min(1f, rms * 8f); // scale for visibility, matches macOS
        LevelChanged?.Invoke(level);
    }

    private void OnNAudioRecordingStopped(object? sender, StoppedEventArgs e)
    {
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
        IsRecording = false;
        LevelChanged?.Invoke(0f);

        if (e.Exception != null)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioRecorder] Recording stopped with error: {e.Exception.Message}");
        }

        var path = _tempFilePath;
        _tempFilePath = null;

        if (path != null && File.Exists(path))
        {
            System.Diagnostics.Debug.WriteLine($"[AudioRecorder] Recording stopped -> {path}");
            RecordingStopped?.Invoke(path);
        }
    }

    private void CleanupAfterFailure()
    {
        _writer?.Dispose();
        _writer = null;
        _waveIn?.Dispose();
        _waveIn = null;
        _tempFilePath = null;
        IsRecording = false;
    }
}
