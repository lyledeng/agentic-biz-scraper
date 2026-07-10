using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// A party (officer, director, agent) associated with a business entity.
/// </summary>
public sealed record PartyEntry
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("organization")]
    public string? Organization { get; init; }

    [JsonPropertyName("address")]
    public string? Address { get; init; }
}
