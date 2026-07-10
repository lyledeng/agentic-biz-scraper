using System.Text.Json.Serialization;

namespace BizScraper.Api.Features.ExecuteScript.Models;

/// <summary>
/// Response envelope for a completed script execution, containing the result data.
/// </summary>
public sealed record ExecuteScriptResponse
{
    [JsonPropertyName("definition")]
    public required string Definition { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("truncated")]
    public required bool Truncated { get; init; }

    /// <summary>
    /// Number of search results returned. Present only for business-search definitions;
    /// omitted from JSON for all other definition types.
    /// </summary>
    [JsonPropertyName("resultCount")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ResultCount { get; init; }

    [JsonPropertyName("data")]
    public object? Data { get; init; }
}
