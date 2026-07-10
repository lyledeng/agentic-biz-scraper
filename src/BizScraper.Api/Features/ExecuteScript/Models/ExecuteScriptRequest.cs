using System.Text.Json.Serialization;

namespace BizScraper.Api.Features.ExecuteScript.Models;

/// <summary>
/// Request payload for executing a named scraping flow definition.
/// </summary>
public sealed record ExecuteScriptRequest
{
    [JsonPropertyName("definition")]
    public required string Definition { get; init; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; init; }
}
