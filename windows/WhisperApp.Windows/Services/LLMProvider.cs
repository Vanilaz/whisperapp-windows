namespace WhisperApp.Services;

/// LLM providers for text correction. Most use the OpenAI-compatible chat/completions API
/// (differing only in endpoint/model/key); Anthropic (and Anthropic-compatible GLM) use a
/// separate request/response shape.
public enum LLMStyle { OpenAI, Anthropic }

public class LLMProvider
{
    public string Id { get; }
    public string Name { get; }
    public string DefaultEndpoint { get; }
    public string DefaultModel { get; }
    public string EnvKey { get; }
    public LLMStyle Style { get; }
    public bool IsCustom { get; }

    public LLMProvider(string id, string name, string defaultEndpoint, string defaultModel,
        string envKey, LLMStyle style, bool isCustom = false)
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

public static class LLMRegistry
{
    public static readonly IReadOnlyList<LLMProvider> All = new List<LLMProvider>
    {
        new("deepseek", "DeepSeek",
            "https://api.deepseek.com/chat/completions", "deepseek-chat",
            "DEEPSEEK_API_KEY", LLMStyle.OpenAI),
        new("openai", "OpenAI",
            "https://api.openai.com/v1/chat/completions", "gpt-4o-mini",
            "OPENAI_API_KEY", LLMStyle.OpenAI),
        new("groq", "Groq",
            "https://api.groq.com/openai/v1/chat/completions", "llama-3.3-70b-versatile",
            "GROQ_API_KEY", LLMStyle.OpenAI),
        new("openrouter", "OpenRouter",
            "https://openrouter.ai/api/v1/chat/completions", "google/gemini-2.0-flash-001",
            "OPENROUTER_API_KEY", LLMStyle.OpenAI),
        new("gemini", "Google Gemini",
            "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", "gemini-2.0-flash",
            "GEMINI_API_KEY", LLMStyle.OpenAI),
        new("anthropic", "Anthropic (Claude)",
            "https://api.anthropic.com/v1/messages", "claude-haiku-4-5",
            "ANTHROPIC_API_KEY", LLMStyle.Anthropic),
        // Z.AI GLM via Anthropic-compatible endpoint (same as used with Claude Code)
        new("glm", "GLM (Z.AI)",
            "https://api.z.ai/api/anthropic/v1/messages", "glm-5.2",
            "ZAI_API_KEY", LLMStyle.Anthropic),
        new("custom", "Custom (OpenAI-compatible)",
            "", "", "LLM_API_KEY", LLMStyle.OpenAI, isCustom: true),
    };

    public static LLMProvider Provider(string id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];
}

/// Manages provider settings: selected provider + key/model/endpoint per provider.
/// - key: file %AppData%\Whisper\llm_&lt;id&gt;.key → fallback to env var
/// - model / endpoint: settings store (if user overrides) → otherwise provider default
public static class LLMSettings
{
    private const string ProviderKey = "llm.provider";

    public static string ProviderId
    {
        get => AppSettingsStore.GetString(ProviderKey) ?? "groq";
        set => AppSettingsStore.SetString(ProviderKey, value);
    }

    public static LLMProvider Current => LLMRegistry.Provider(ProviderId);

    private static string KeyPath(LLMProvider p) => Path.Combine(KeyStore.Dir, $"llm_{p.Id}.key");

    public static string? Key(LLMProvider p)
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

    public static string SavedKeyFile(LLMProvider p)
    {
        try
        {
            var path = KeyPath(p);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
        }
        catch { return ""; }
    }

    public static void SaveKey(string key, LLMProvider p)
    {
        KeyStore.EnsureDir();
        File.WriteAllText(KeyPath(p), key.Trim());
    }

    public static string Model(LLMProvider p)
    {
        var custom = AppSettingsStore.GetString($"llm.model.{p.Id}")?.Trim();
        return string.IsNullOrEmpty(custom) ? p.DefaultModel : custom;
    }

    public static void SaveModel(string model, LLMProvider p) =>
        AppSettingsStore.SetString($"llm.model.{p.Id}", model.Trim());

    public static Uri Endpoint(LLMProvider p)
    {
        var custom = AppSettingsStore.GetString($"llm.endpoint.{p.Id}")?.Trim();
        if (!string.IsNullOrEmpty(custom) && Uri.TryCreate(custom, UriKind.Absolute, out var u)) return u;

        if (p.Id == "deepseek") return DeepSeekEndpoint();

        return Uri.TryCreate(p.DefaultEndpoint, UriKind.Absolute, out var d)
            ? d
            : new Uri("https://api.deepseek.com/chat/completions");
    }

    public static void SaveEndpoint(string endpoint, LLMProvider p) =>
        AppSettingsStore.SetString($"llm.endpoint.{p.Id}", endpoint.Trim());

    public static string EndpointString(LLMProvider p)
    {
        var custom = AppSettingsStore.GetString($"llm.endpoint.{p.Id}")?.Trim();
        if (!string.IsNullOrEmpty(custom)) return custom;
        if (p.Id == "deepseek") return DeepSeekEndpoint().ToString();
        return p.DefaultEndpoint;
    }

    public static bool IsConfigured(LLMProvider p) =>
        Key(p) != null && !string.IsNullOrEmpty(EndpointString(p));

    /// DeepSeek endpoint override from environment variables, mirroring the macOS behavior
    /// (supports both a full endpoint override and a base-URL override).
    private static Uri DeepSeekEndpoint()
    {
        foreach (var name in new[] { "DEEPSEEK_ENDPOINT", "DEEPSEEK_API_ENDPOINT" })
        {
            var v = KeyStore.EnvValue(name);
            if (v != null && Uri.TryCreate(v, UriKind.Absolute, out var u)) return u;
        }
        foreach (var name in new[] { "DEEPSEEK_API_BASE", "DEEPSEEK_BASE_URL" })
        {
            var v = KeyStore.EnvValue(name);
            if (v != null)
            {
                var baseUrl = v.TrimEnd('/');
                if (Uri.TryCreate(baseUrl + "/chat/completions", UriKind.Absolute, out var u)) return u;
            }
        }
        return new Uri("https://api.deepseek.com/chat/completions");
    }
}
