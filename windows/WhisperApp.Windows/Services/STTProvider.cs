using System.IO;

namespace WhisperApp.Services;

/// Cloud Speech-to-Text providers.
/// - ElevenLabs: multipart, header xi-api-key, language ISO 639-3
/// - OpenAI style: multipart, Bearer auth, language ISO 639-1 (also used by Groq / custom)
public enum STTStyle { ElevenLabs, OpenAI }

public class STTProvider
{
    public string Id { get; }
    public string Name { get; }
    public string DefaultEndpoint { get; }
    public string DefaultModel { get; }
    public string EnvKey { get; }
    public STTStyle Style { get; }
    public bool IsCustom { get; }

    public STTProvider(string id, string name, string defaultEndpoint, string defaultModel,
        string envKey, STTStyle style, bool isCustom = false)
    {
        Id = id;
        Name = name;
        DefaultEndpoint = defaultEndpoint;
        DefaultModel = defaultModel;
        EnvKey = envKey;
        Style = style;
        IsCustom = isCustom;
    }
}

public static class STTRegistry
{
    public static readonly IReadOnlyList<STTProvider> All = new List<STTProvider>
    {
        new("elevenlabs", "ElevenLabs Scribe",
            "https://api.elevenlabs.io/v1/speech-to-text", "scribe_v1",
            "ELEVENLABS_API_KEY", STTStyle.ElevenLabs),
        new("openai", "OpenAI",
            "https://api.openai.com/v1/audio/transcriptions", "gpt-4o-transcribe",
            "OPENAI_API_KEY", STTStyle.OpenAI),
        new("groq", "Groq (Whisper)",
            "https://api.groq.com/openai/v1/audio/transcriptions", "whisper-large-v3-turbo",
            "GROQ_API_KEY", STTStyle.OpenAI),
        new("stt_custom", "Custom (OpenAI-compatible)",
            "", "", "STT_API_KEY", STTStyle.OpenAI, isCustom: true),
    };

    public static STTProvider Provider(string id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}

/// Manages STT provider settings: selected provider + key/model/endpoint per provider.
public static class STTSettings
{
    private const string ProviderKey = "stt.provider";

    public static string ProviderId
    {
        get => AppSettingsStore.GetString(ProviderKey) ?? "groq";
        set => AppSettingsStore.SetString(ProviderKey, value);
    }

    public static STTProvider Current => STTRegistry.Provider(ProviderId);

    private static string KeyPath(STTProvider p) => Path.Combine(KeyStore.Dir, $"stt_{p.Id}.key");

    public static string? Key(STTProvider p)
    {
        try
        {
            var path = KeyPath(p);
            if (File.Exists(path))
            {
                var t = File.ReadAllText(path).Trim();
                if (!string.IsNullOrEmpty(t)) return t;
            }
        }
        catch { /* fall through to env */ }
        return KeyStore.EnvValue(p.EnvKey);
    }

    /// Key as saved to file by the user (for display in Settings — not env-derived).
    public static string SavedKeyFile(STTProvider p)
    {
        try
        {
            var path = KeyPath(p);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch { return ""; }
    }

    public static void SaveKey(string key, STTProvider p)
    {
        KeyStore.EnsureDir();
        File.WriteAllText(KeyPath(p), key.Trim());
    }

    public static string Model(STTProvider p)
    {
        var custom = AppSettingsStore.GetString($"stt.model.{p.Id}")?.Trim();
        return string.IsNullOrEmpty(custom) ? p.DefaultModel : custom;
    }

    public static void SaveModel(string model, STTProvider p) =>
        AppSettingsStore.SetString($"stt.model.{p.Id}", model.Trim());

    public static string EndpointString(STTProvider p)
    {
        var custom = AppSettingsStore.GetString($"stt.endpoint.{p.Id}")?.Trim();
        return string.IsNullOrEmpty(custom) ? p.DefaultEndpoint : custom;
    }

    public static Uri? Endpoint(STTProvider p) =>
        Uri.TryCreate(EndpointString(p), UriKind.Absolute, out var u) ? u : null;

    public static void SaveEndpoint(string endpoint, STTProvider p) =>
        AppSettingsStore.SetString($"stt.endpoint.{p.Id}", endpoint.Trim());

    public static bool IsConfigured(STTProvider p) =>
        Key(p) != null && !string.IsNullOrEmpty(EndpointString(p));
}
