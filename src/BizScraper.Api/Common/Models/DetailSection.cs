using System.Text.Json.Serialization;

namespace BizScraper.Api.Common.Models;

/// <summary>
/// Core entity information present for all jurisdictions.
/// </summary>
public sealed record DetailSection
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("identifier")]
    public required string Identifier { get; init; }

    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("formationDate")]
    public string? FormationDate { get; init; }

    [JsonPropertyName("entityType")]
    public string? EntityType { get; init; }

    [JsonPropertyName("jurisdiction")]
    public string? Jurisdiction { get; init; }

    [JsonPropertyName("principalAddress")]
    public string? PrincipalAddress { get; init; }

    [JsonPropertyName("mailingAddress")]
    public string? MailingAddress { get; init; }

    [JsonPropertyName("periodicReportMonth")]
    public string? PeriodicReportMonth { get; init; }

    [JsonPropertyName("subStatus")]
    public string? SubStatus { get; init; }

    [JsonPropertyName("standingTax")]
    public string? StandingTax { get; init; }

    [JsonPropertyName("standingRA")]
    public string? StandingRA { get; init; }

    [JsonPropertyName("standingOther")]
    public string? StandingOther { get; init; }

    [JsonPropertyName("inactiveDate")]
    public string? InactiveDate { get; init; }

    [JsonPropertyName("termOfDuration")]
    public string? TermOfDuration { get; init; }

    [JsonPropertyName("formedIn")]
    public string? FormedIn { get; init; }

    [JsonPropertyName("latestAnnualReportYear")]
    public string? LatestAnnualReportYear { get; init; }

    [JsonPropertyName("annualReportExempt")]
    public string? AnnualReportExempt { get; init; }

    [JsonPropertyName("licenseTaxPaid")]
    public string? LicenseTaxPaid { get; init; }

    [JsonPropertyName("registeredOffice")]
    public string? RegisteredOffice { get; init; }

    /// <summary>Iowa SOS legal code/chapter designation. Null for non-Iowa entities.</summary>
    [JsonPropertyName("chapterCode")]
    public string? ChapterCode { get; init; }

    /// <summary>Iowa certificate of existence paywall note. Null for non-Iowa entities.</summary>
    [JsonPropertyName("certificateNote")]
    public string? CertificateNote { get; init; }

    /// <summary>Iowa-specific names list. Null for non-Iowa entities.</summary>
    [JsonPropertyName("iowaNames")]
    public IReadOnlyList<IowaNameEntry>? IowaNames { get; init; }
}
