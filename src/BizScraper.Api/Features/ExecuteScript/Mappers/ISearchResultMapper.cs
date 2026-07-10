using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Abstraction for normalizing raw search results from a specific state's format
/// into the unified API response shape.
/// </summary>
public interface ISearchResultMapper
{
    /// <summary>
    /// The definition slug prefix this mapper handles (e.g., "us-co", "de-de").
    /// </summary>
    string SlugPrefix { get; }

    /// <summary>
    /// Map raw flow output to an array of unified search results.
    /// Returns null if the output cannot be mapped.
    /// </summary>
    UnifiedSearchResult[]? Map(JsonElement output);
}
