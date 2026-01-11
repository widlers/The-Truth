using System.Text;
using System.Text.Json;
using TheTruth.Core.Models;

namespace TheTruth.Core;

public class OllamaService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:11434";

    public OllamaService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
    }

    public async Task<bool> IsRunningAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GetBestModelAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync($"{BaseUrl}/api/tags");
            var data = JsonSerializer.Deserialize<OllamaModelListResponse>(response);

            if (data == null || data.Models.Count == 0) return "llama3"; // Fallback

            // Prefer llama3, mistral, gemma in that order
            var models = data.Models.Select(m => m.Name).ToList();
            if (models.Any(m => m.Contains("llama3"))) return models.First(m => m.Contains("llama3"));
            if (models.Any(m => m.Contains("mistral"))) return models.First(m => m.Contains("mistral"));
            if (models.Any(m => m.Contains("gemma"))) return models.First(m => m.Contains("gemma"));

            return models.First(); // Return first available
        }
        catch
        {
            return "llama3"; // Default fallback
        }
    }

    public async Task<string> AnalyzeAsync(string claim, List<SearchResultItem> sources, string language = "de")
    {
        string model = await GetBestModelAsync();

        // Build Prompt
        var sb = new StringBuilder();

        if (language == "en")
        {
            sb.AppendLine("You are an intelligent and objective Fact-Checker.");
            sb.AppendLine($"Claim: \"{claim}\"");
            sb.AppendLine();
            sb.AppendLine("RULES FOR ANALYSIS:");
            sb.AppendLine("1. Use ONLY the provided sources.");
            sb.AppendLine("2. Understand synonyms and context (e.g. 'Trump' = 'US President' -> actions of the president are often actions of the USA).");
            sb.AppendLine("3. Ignore obviously irrelevant sources (e.g. questions in forums without answers).");
            sb.AppendLine("4. If even ONE reputable source confirms the claim or presents it as possible ('considering', 'reviewing'), it is not 'FALSE', but 'PARTIALLY TRUE' or 'UNCLEAR'.");
            sb.AppendLine();
            sb.AppendLine("Search Results:");
        }
        else
        {
            sb.AppendLine("Du bist ein intelligenter und objektiver Fact-Checker.");
            sb.AppendLine($"Behauptung: \"{claim}\"");
            sb.AppendLine();
            sb.AppendLine("REGELN FÜR DIE ANALYSE:");
            sb.AppendLine("1. Nutze NUR die bereitgestellten Quellen.");
            sb.AppendLine("2. Verstehe Synonyme und Kontexte (z.B. 'Trump' = 'US-Präsident' -> Handlungen des Präsidenten sind oft Handlungen der USA).");
            sb.AppendLine("3. Ignoriere offensichtlich irrelevante Quellen (z.B. Fragen in Foren ohne Antworten).");
            sb.AppendLine("4. Wenn auch nur EINE seriöse Quelle die Behauptung bestätigt oder als möglich darstellt ('prüft', 'erwägt'), ist es nicht 'FALSCH', sondern 'TEILWEISE WAHR' oder 'UNKLAR'.");
            sb.AppendLine();
            sb.AppendLine("Suchergebnisse:");
        }

        int idx = 1;
        foreach (var s in sources)
        {
            sb.AppendLine($"{idx}. [{s.Title}]({s.Url}): {s.Snippet}");
            idx++;
        }
        sb.AppendLine();

        if (language == "en")
        {
            sb.AppendLine("Provide your verdict: [TRUE / FALSE / UNCLEAR / PARTIALLY TRUE].");
            sb.AppendLine("Justification (short): Summarize what the sources say. Explain contradictions if present.");
        }
        else
        {
            sb.AppendLine("Gib dein Urteil ab: [WAHR / FALSCH / UNKLAR / TEILWEISE WAHR].");
            sb.AppendLine("Begründung (kurz): Fasse zusammen, was die Quellen sagen. Erkläre Widersprüche falls vorhanden.");
        }

        var request = new OllamaGenerateRequest
        {
            Model = model,
            Prompt = sb.ToString(),
            Stream = false
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync($"{BaseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseString);

            return result?.Response ?? "Fehler: Keine Antwort von AI erhalten.";
        }
        catch (Exception ex)
        {
            return $"AI Fehler: {ex.Message}. (Läuft Ollama?)";
        }
    }
}
