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

    private async Task<List<SearchResultItem>> SearchAsync(string query, string category, string language)
    {
        string scriptPath = Path.Combine(_enginePath, PythonScript);
        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException($"Python script not found at {scriptPath}");
        }

        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"\"{scriptPath}\" \"{query}\" \"{category}\" \"{language}\"",
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
}
