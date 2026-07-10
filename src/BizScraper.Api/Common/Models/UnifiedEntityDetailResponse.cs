using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// Top-level response envelope for entity detail lookups with five nullable sections.
/// </summary>
public sealed record UnifiedEntityDetailResponse
{
    [JsonPropertyName("details")]
    public required DetailSection Details { get; init; }

    [JsonPropertyName("registeredAgent")]
    public AgentSection? RegisteredAgent { get; init; }

    [JsonPropertyName("certificate")]
    public CertificateSection? Certificate { get; init; }

    [JsonPropertyName("parties")]
    public IReadOnlyList<PartyEntry>? Parties { get; init; }

    [JsonPropertyName("documents")]
    public IReadOnlyList<DocumentEntry>? Documents { get; init; }
}
