using System.IO;
using System.Text.Json;

namespace WhisperApp.Services;

/// Simple JSON-backed key/value store under %AppData%\Whisper\settings.json.
/// Stands in for macOS's UserDefaults (selected provider, per-provider model/endpoint
/// overrides, hotkey config).
public static class AppSettingsStore
{
    private static readonly string FilePath = Path.Combine(KeyStore.Dir, "settings.json");
    private static readonly object Lock = new();
    private static Dictionary<string, string> _values = new();
    private static bool _loaded;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (Lock)
        {
            if (_loaded) return;
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _values = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            }
            catch
            {
                _values = new();
            }
            _loaded = true;
        }
    }

    public static string? GetString(string key)
    {
        EnsureLoaded();
        lock (Lock)
        {
            return _values.TryGetValue(key, out var v) ? v : null;
        }
    }

    public static void SetString(string key, string value)
    {
        EnsureLoaded();
        lock (Lock)
        {
            _values[key] = value;
            Save();
        }
    }

    public static byte[]? GetBytes(string key)
    {
        var s = GetString(key);
        if (s == null) return null;
        try { return Convert.FromBase64String(s); }
        catch { return null; }
    }

    public static void SetBytes(string key, byte[] value) => SetString(key, Convert.ToBase64String(value));

    private static void Save()
    {
        try
        {
            KeyStore.EnsureDir();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_values));
        }
        catch
        {
            // Best-effort persistence — a failed save shouldn't crash the app.
        }
    }
}
