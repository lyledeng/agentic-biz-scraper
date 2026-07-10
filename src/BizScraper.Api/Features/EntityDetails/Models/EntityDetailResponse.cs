namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// Complete entity detail response including core details, agent, certificate, parties, and history documents.
/// </summary>
public sealed record EntityDetailResponse(
    EntityDetailResult Details,
    RegisteredAgentResult? RegisteredAgent,
    CertificateResult? Certificate,
    IReadOnlyList<PartyResult>? Parties = null,
    IReadOnlyList<HistoryDocumentResult>? HistoryDocuments = null);
