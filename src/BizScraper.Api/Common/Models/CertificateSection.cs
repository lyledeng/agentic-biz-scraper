using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// Certificate of good standing information.
/// </summary>
public sealed record CertificateSection
{
    [JsonPropertyName("available")]
    public required bool Available { get; init; }

    [JsonPropertyName("downloads")]
    public IReadOnlyList<DownloadReference>? Downloads { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}
