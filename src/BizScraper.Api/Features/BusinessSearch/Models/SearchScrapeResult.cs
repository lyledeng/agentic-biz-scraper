namespace BizScraper.Api.Features.BusinessSearch.Models;

/// <summary>
/// Legacy Colorado-specific scrape result before normalization.
/// </summary>
public sealed record SearchScrapeResult(
    IReadOnlyList<BusinessEntityResult> Results,
    int SiteTotalCount,
    bool Truncated,
    int PagesScraped);
