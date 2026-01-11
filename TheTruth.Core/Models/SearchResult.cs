using System.Text.Json.Serialization;

namespace TheTruth.Core.Models;

public class SearchResultItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("href")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Snippet { get; set; } = string.Empty;
}

public class VerificationResult
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResultItem> Sources { get; set; } = new();
    public string AiSummary { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}
