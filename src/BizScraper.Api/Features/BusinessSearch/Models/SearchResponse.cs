namespace BizScraper.Api.Features.BusinessSearch.Models;

/// <summary>
/// Legacy Colorado-specific search response before normalization.
/// </summary>
public sealed record SearchResponse(
    IReadOnlyList<BusinessEntityResult> Results,
    int TotalCount,
    int SiteTotalCount,
    bool Truncated,
    int PagesScraped,
    string SearchTerm);
