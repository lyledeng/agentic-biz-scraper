using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Maps raw script output to UnifiedSearchResult[] based on the definition slug.
/// Delegates to per-state mappers via <see cref="MapperRegistry"/>.
/// </summary>
internal sealed class SearchResultMapper(MapperRegistry registry)
{
    /// <summary>
    /// Map raw JSON output from a *-business-search definition to a unified search result array.
    /// Returns null if the slug is not a business-search definition.
    /// </summary>
    public UnifiedSearchResult[]? MapToUnified(JsonElement output, string definitionSlug)
    {
        if (!definitionSlug.EndsWith("-business-search", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return registry.GetSearchMapper(definitionSlug).Map(output);
    }
}

internal static class JsonElementExtensions
{
    public static string GetStringOrDefault(this JsonElement element, string propertyName, string defaultValue = "")
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString() ?? defaultValue;
        }

        return defaultValue;
    }
}
