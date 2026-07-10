namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// Registered agent information for a business entity.
/// </summary>
public sealed record RegisteredAgentResult(
    string? Name,
    string? StreetAddress,
    string? MailingAddress);
