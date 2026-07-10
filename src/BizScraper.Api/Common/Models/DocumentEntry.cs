using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// A single document associated with an entity (WY filing or DE hardcopy).
/// </summary>
public sealed record DocumentEntry
{
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("date")]
    public string? Date { get; init; }

    [JsonPropertyName("downloads")]
    public required IReadOnlyList<DownloadReference> Downloads { get; init; }
}
