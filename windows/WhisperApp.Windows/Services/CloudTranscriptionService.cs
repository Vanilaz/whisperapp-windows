using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WhisperApp.Services;

/// Transcribe audio via cloud STT — supports multiple providers (ElevenLabs Scribe / OpenAI /
/// Groq / Custom) via STTSettings — see STTProvider.cs.
public class CloudTranscriptionService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    private STTProvider Provider => STTSettings.Current;

    public bool IsAvailable => STTSettings.Key(Provider) != null;

    /// Convert app language -> language code based on provider style.
    /// - elevenlabs uses ISO 639-3 (tha/eng) · openAI style uses ISO 639-1 (th/en)
    /// - "auto" -> null (let the provider auto-detect)
    private static string? LangCode(string language, STTStyle style) => (language, style) switch
    {
        ("th", STTStyle.ElevenLabs) => "tha",
        ("en", STTStyle.ElevenLabs) => "eng",
        ("th", STTStyle.OpenAI) => "th",
        ("en", STTStyle.OpenAI) => "en",
        _ => null,
    };

    public async Task<string?> TranscribeAsync(string filePath, string language)
    {
        var p = Provider;
        var key = STTSettings.Key(p);
        if (key == null)
        {
            System.Diagnostics.Debug.WriteLine($"[STT] No key found for {p.Name} (configure in Settings or set env {p.EnvKey})");
            return null;
        }
        var endpoint = STTSettings.Endpoint(p);
        if (endpoint == null)
        {
            System.Diagnostics.Debug.WriteLine($"[STT] Invalid endpoint for {p.Name}");
            return null;
        }

        byte[] fileData;
        try
        {
            fileData = await File.ReadAllBytesAsync(filePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STT] Could not read audio file: {ex.Message}");
            return null;
        }

        using var form = new MultipartFormDataContent();
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);

        switch (p.Style)
        {
            case STTStyle.ElevenLabs:
                req.Headers.Add("xi-api-key", key);
                break;
            case STTStyle.OpenAI:
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                break;
        }

        // Field names differ: ElevenLabs = model_id/language_code, OpenAI-style = model/language
        string modelField = p.Style == STTStyle.ElevenLabs ? "model_id" : "model";
        string langField = p.Style == STTStyle.ElevenLabs ? "language_code" : "language";

        form.Add(new StringContent(STTSettings.Model(p)), modelField);
        var lang = LangCode(language, p.Style);
        if (lang != null) form.Add(new StringContent(lang), langField);

        var fileContent = new ByteArrayContent(fileData);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "audio.wav");
        req.Content = form;

        try
        {
            using var resp = await Http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            // Both styles return { "text": "..." }
            if (doc.RootElement.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                var text = textEl.GetString()?.Trim();
                return string.IsNullOrEmpty(text) ? null : text;
            }

            System.Diagnostics.Debug.WriteLine($"[STT] {p.Name} response: {body}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[STT] {p.Name} error: {ex.Message}");
            return null;
        }
    }
}
