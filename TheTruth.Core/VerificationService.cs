using System.Diagnostics;
using System.Text.Json;
using TheTruth.Core.Models;

namespace TheTruth.Core;

public class VerificationService
{
    private const string PythonScript = "search_engine.py";
    // Adjust this path logic to be dynamic relative to the executable
    private readonly string _enginePath;
    private readonly OllamaService _ollamaService;

    public VerificationService()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _enginePath = Path.GetFullPath(Path.Combine(baseDir, "../../../../TheTruth.Engine"));
        _ollamaService = new OllamaService();
    }

    public async Task<VerificationResult> VerifyAsync(string claim, string category = "general", string language = "de")
    {
        var result = new VerificationResult { Query = claim };

        // 1. Web Search
        try
        {
            result.Sources = await SearchAsync(claim, category, language);
        }
        catch (Exception ex)
        {
            // If search fails, we can't really verify, but we report it
            result.AiSummary = "Fehler bei der Suche / Search Error: " + ex.Message;
            return result;
        }

        // 2. AI Analysis
        if (result.Sources.Any())
        {
            // NEW: Translate sources if German is selected
            if (language == "de")
            {
                // We fire and forget? No, we need it for display. 
                // But we should maybe do it BEFORE Analysis? 
                // Actually, let's do it BEFORE analysis so the USER sees German text. 
                // The AI is smart enough to analyze German text or we could keep originals. 
                // For now: Overwrite with translation.
                await _ollamaService.TranslateSourcesAsync(result.Sources);
            }

            result.AiSummary = await _ollamaService.AnalyzeAsync(claim, result.Sources, language);
            result.IsVerified = true; // Flag that analysis ran
        }
        else
        {
            result.AiSummary = language == "de"
                ? "Keine relevanten Quellen gefunden. AI-Analyse übersprungen."
                : "No relevant sources found. AI analysis skipped.";
        }

        return result;
    }

    public async Task<List<SearchResultItem>> GetLiveFeedAsync(string language = "en", string source = "all", int offset = 0)
    {
        try
        {
            // If German is selected, use native German sources instead of translating NYT/Guardian
            // unless specific source is requested
            string effectiveSource = source;
            if (language == "de" && source == "all")
            {
                effectiveSource = "de_all";
            }

            // Call Python with FEED_MODE and source
            var feed = await SearchAsync($"FEED_MODE {effectiveSource} {offset}", "general", "en");

            // Translate if German AND we are using non-German sources (e.g. if we kept NYT)
            // But if we switched to de_all, we don't need translation.
            // If the user manually requested "from NYT" but in German mode, we should translate.
            // For now, simplify: Only translate if language is de AND source is NOT de_all
            if (language == "de" && effectiveSource != "de_all" && feed != null && feed.Count > 0)
            {
                // Check if it's an error message
                if (feed.Count == 1 && feed[0].Title == "" && feed[0].Url == "")
                    return feed; // Error case

                await _ollamaService.TranslateSourcesAsync(feed);
            }
            return feed ?? new List<SearchResultItem>();
        }
        catch (Exception ex)
        {
            return new List<SearchResultItem> { new SearchResultItem { Snippet = "Error fetching feed: " + ex.Message } };
        }
    }

    public async Task<string> AnalyzeMetadataAsync(string imagePath)
    {
        return await RunPythonAsync($"METADATA_MODE \"{imagePath}\"");
    }

    private async Task<List<SearchResultItem>> SearchAsync(string query, string category, string language)
    {
        string args = $"\"{query}\" \"{category}\" \"{language}\"";
        string output = await RunPythonAsync(args);

        try
        {
            var results = JsonSerializer.Deserialize<List<SearchResultItem>>(output);
            return results ?? new List<SearchResultItem>();
        }
        catch (JsonException ex)
        {
            throw new Exception($"Failed to parse python output. Raw: {output.Take(100)}...", ex);
        }
    }

    private async Task<string> RunPythonAsync(string args)
    {
        string scriptPath = Path.Combine(_enginePath, PythonScript);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Python script not found at {scriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{scriptPath}\" {args}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Python script failed: {error}");
        }

        return output;
    }

    public async Task<string> AnalyzeImageAsync(string imagePath, string language = "en")
    {
        return await _ollamaService.AnalyzeImageAsync(imagePath, language);
    }

    public async Task<List<SearchResultItem>> DeepScanAsync(string imagePath)
    {
        // Call Python with LENS_MODE
        // We pass imagePath quoted
        return await SearchAsync($"LENS_MODE \"{imagePath}\"", "general", "en");
    }
}
