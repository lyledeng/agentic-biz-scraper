namespace BizScraper.Api.Features.BusinessSearch.Models;

/// <summary>
/// Legacy request model for the deprecated business search endpoint.
/// </summary>
public sealed record SearchRequest(string Name)
{
    public string TrimmedName => Name.Trim();

    public bool IsValid => !string.IsNullOrWhiteSpace(Name);
}
