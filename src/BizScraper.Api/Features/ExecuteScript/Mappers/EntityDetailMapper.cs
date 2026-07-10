using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Maps raw script output to UnifiedEntityDetailResponse based on the definition slug.
/// Delegates to per-state mappers via <see cref="MapperRegistry"/>.
/// </summary>
internal sealed class EntityDetailMapper(MapperRegistry registry)
{
    /// <summary>
    /// Map raw JSON output from a *-entity-details definition to a unified entity detail response.
    /// Returns null if the slug is not an entity-details definition.
    /// </summary>
    public UnifiedEntityDetailResponse? MapToUnified(JsonElement output, string definitionSlug)
    {
        if (!definitionSlug.EndsWith("-entity-details", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return registry.GetDetailMapper(definitionSlug).Map(output);
    }
}
