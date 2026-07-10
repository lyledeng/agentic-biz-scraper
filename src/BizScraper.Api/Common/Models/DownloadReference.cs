using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// A single downloadable file within a document entry.
/// </summary>
public sealed record DownloadReference
{
    [JsonPropertyName("label")]
    public required string Label { get; init; }

    [JsonPropertyName("proxyUrl")]
    public string? ProxyUrl { get; init; }

    [JsonPropertyName("fileName")]
    public required string FileName { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
