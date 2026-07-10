namespace BizScraper.Api.Features.BusinessSearch.Models;

/// <summary>
/// A single normalized business search result with state-agnostic fields.
/// </summary>
public sealed record NormalizedSearchResult(
    string Name,
    string Identifier,
    string Status,
    string EntityType,
    string FormationDate,
    string DetailsUrl,
    string State,
    string? Event = null,
    string? StandingTax = null,
    string? StandingRA = null,
    string? RegistrationId = null);

/// <summary>
/// Internal scrape result containing normalized search results and pagination metadata.
/// </summary>
public sealed record NormalizedSearchScrapeResult(
    IReadOnlyList<NormalizedSearchResult> Results,
    int SiteTotalCount,
    bool Truncated,
    int PagesScraped,
    string? WarningMessage = null);
