namespace BizScraper.Api.Features.EntityDetails.Models;

/// <summary>
/// A party (officer, director, or agent) associated with a business entity.
/// </summary>
public sealed record PartyResult(
    string Name,
    string Role,
    string? Organization,
    string? Address);
