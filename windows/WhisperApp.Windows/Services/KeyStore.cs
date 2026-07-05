using System.IO;

namespace WhisperApp.Services;

/// Manages the app's local data directory and environment-variable lookups.
/// Unlike macOS (which had to shell out to zsh to read ~/.zshrc because GUI apps launched
/// from Finder don't inherit terminal env vars), Windows GUI apps normally do inherit the
/// user's environment variables directly — no shell round-trip needed.
public static class KeyStore
{
    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Whisper");

    public static void EnsureDir() => Directory.CreateDirectory(Dir);

    public static string? EnvValue(string name)
    {
        var v = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process)
                ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
                ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
        v = v?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    /// Called at app launch (background) to warm the settings cache off the UI thread.
    public static void Prewarm()
    {
        Task.Run(() =>
        {
            _ = EnvValue(LLMSettings.Current.EnvKey);
            _ = EnvValue(STTSettings.Current.EnvKey);
        });
    }
}
