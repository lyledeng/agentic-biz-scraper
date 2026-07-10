using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Abstraction for normalizing raw entity details from a specific state's format
/// into the unified API response shape.
/// </summary>
public interface IEntityDetailMapper
{
    /// <summary>
    /// The definition slug prefix this mapper handles (e.g., "us-co", "de-de").
    /// </summary>
    string SlugPrefix { get; }

    /// <summary>
    /// Map raw flow output to a unified entity detail response.
    /// Returns null if the output cannot be mapped.
    /// </summary>
    UnifiedEntityDetailResponse? Map(JsonElement output);
}
