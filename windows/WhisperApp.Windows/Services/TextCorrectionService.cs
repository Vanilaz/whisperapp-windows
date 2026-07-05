using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace WhisperApp.Services;

/// Send raw transcription text to an LLM (cloud) to fix typos, punctuation, and sentence
/// structure. Supports multiple providers (DeepSeek, OpenAI, Groq, OpenRouter, Gemini,
/// Anthropic, GLM, Custom) via LLMSettings — see LLMProvider.cs.
public class TextCorrectionService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(60) };

    public bool IsCorrecting { get; private set; }

    private LLMProvider Provider => LLMSettings.Current;

    public bool IsAvailable => LLMSettings.Key(Provider) != null;

    public async Task<string?> CorrectAsync(string text, string language)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var p = Provider;
        var key = LLMSettings.Key(p);
        if (key == null)
        {
            System.Diagnostics.Debug.WriteLine($"[LLM] No key found for {p.Name} (configure in Settings or set env {p.EnvKey})");
            return null;
        }

        string langHint = language switch
        {
            "th" => "The text is in Thai",
            "en" => "The text is in English",
            _ => "The text may be in Thai or English — keep the original language",
        };

        string systemPrompt =
            "You are a text correction assistant for speech-to-text output, which often contains\n" +
            "misheard words and missing punctuation.\n" +
            "Your tasks:\n" +
            "- Fix misheard/garbled words based on context\n" +
            "- Add punctuation and spacing to improve readability\n" +
            "- Do NOT add new content, summarize, translate, or change word endings/speaker gender\n" +
            "- Return ONLY the corrected text — no explanations, no quotation marks\n" +
            langHint;

        var endpoint = LLMSettings.Endpoint(p);
        var model = LLMSettings.Model(p);

        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
        object body;

        switch (p.Style)
        {
            case LLMStyle.OpenAI:
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
                body = new
                {
                    model,
                    temperature = 0.2,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = text },
                    },
                };
                break;

            case LLMStyle.Anthropic:
                req.Headers.Add("x-api-key", key);
                req.Headers.Add("anthropic-version", "2023-06-01");
                var dict = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["max_tokens"] = 8192,
                    ["temperature"] = 0.2,
                    ["system"] = systemPrompt,
                    ["messages"] = new object[] { new { role = "user", content = text } },
                };
                // GLM enables thinking by default -> disable for fast text correction
                if (model.Contains("glm", StringComparison.OrdinalIgnoreCase))
                {
                    dict["thinking"] = new { type = "disabled" };
                }
                body = dict;
                break;

            default:
                return null;
        }

        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        IsCorrecting = true;
        try
        {
            using var resp = await Http.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(respBody);
            var content = ExtractText(doc.RootElement, p.Style);
            if (content == null)
            {
                System.Diagnostics.Debug.WriteLine($"[LLM] Correction response: {respBody}");
                return null;
            }

            var cleaned = content.Trim().Trim('"', '\'');
            return string.IsNullOrEmpty(cleaned) ? null : cleaned;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLM] Correction error: {ex.Message}");
            return null;
        }
        finally
        {
            IsCorrecting = false;
        }
    }

    private static string? ExtractText(JsonElement root, LLMStyle style)
    {
        try
        {
            switch (style)
            {
                case LLMStyle.OpenAI:
                    return root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                case LLMStyle.Anthropic:
                    var sb = new StringBuilder();
                    foreach (var block in root.GetProperty("content").EnumerateArray())
                    {
                        if (block.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                            sb.Append(t.GetString());
                    }
                    return sb.ToString();

                default:
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
}
