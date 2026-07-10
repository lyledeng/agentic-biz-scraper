using System.Text.Json.Serialization;

namespace BizScraper.Api.Features.ExecuteScript.Models;

/// <summary>
/// Describes an available scraping flow definition and its required parameters.
/// </summary>
public sealed record DefinitionInfo
{
    [JsonPropertyName("definitionSlug")]
    public required string DefinitionSlug { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("state")]
    public required string State { get; init; }

    [JsonPropertyName("requiredParameters")]
    public required IReadOnlyList<ParameterInfo> RequiredParameters { get; init; }
}

/// <summary>
/// Describes a single required parameter for a scraping flow definition.
/// </summary>
public sealed record ParameterInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}
