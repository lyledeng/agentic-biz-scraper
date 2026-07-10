namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// Internal scrape result containing raw entity detail data before response assembly.
/// </summary>
public sealed record EntityDetailScrapeResult(
    EntityDetailResult Details,
    RegisteredAgentResult? RegisteredAgent,
    bool CertificateAvailable,
    string? CertificateLocalPath,
    IReadOnlyList<PartyResult>? Parties = null,
    IReadOnlyList<HistoryDocumentResult>? HistoryDocuments = null);
