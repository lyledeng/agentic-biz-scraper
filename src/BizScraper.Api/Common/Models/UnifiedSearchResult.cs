using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// A single normalized business search result with a consistent shape across all jurisdictions.
/// </summary>
public sealed record UnifiedSearchResult
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("identifier")]
    public required string Identifier { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    [JsonPropertyName("formationDate")]
    public string? FormationDate { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("event")]
    public string? Event { get; init; }

    [JsonPropertyName("uniqueKey")]
    public required string UniqueKey { get; init; }

    [JsonPropertyName("standingTax")]
    public string? StandingTax { get; init; }

    [JsonPropertyName("standingRA")]
    public string? StandingRA { get; init; }

    [JsonPropertyName("registeredOffice")]
    public string? RegisteredOffice { get; init; }
}
