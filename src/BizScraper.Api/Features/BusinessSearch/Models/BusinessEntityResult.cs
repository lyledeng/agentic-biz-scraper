namespace BizScraper.Api.Features.BusinessSearch.Models;

/// <summary>
/// Legacy Colorado-specific search result before normalization across states.
/// </summary>
public sealed record BusinessEntityResult(
    string DocumentNumber,
    string Name,
    string Event,
    string Status,
    string Form,
    string FormationDate,
    string DetailsUrl);
