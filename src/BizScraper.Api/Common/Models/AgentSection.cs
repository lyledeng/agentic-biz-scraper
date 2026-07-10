using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// Registered agent information for a business entity.
/// </summary>
public sealed record AgentSection
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("streetAddress")]
    public string? StreetAddress { get; init; }

    [JsonPropertyName("mailingAddress")]
    public string? MailingAddress { get; init; }
}
