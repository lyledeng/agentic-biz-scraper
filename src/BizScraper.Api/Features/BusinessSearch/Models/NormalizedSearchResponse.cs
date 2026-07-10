namespace BizScraper.Api.Features.BusinessSearch.Models;

/// <summary>
/// Normalized response envelope for multi-state business search results.
/// </summary>
public sealed record NormalizedSearchResponse(
    IReadOnlyList<NormalizedSearchResult> Results,
    int TotalCount,
    int SiteTotalCount,
    bool Truncated,
    int PagesScraped,
    string SearchTerm,
    string State);
