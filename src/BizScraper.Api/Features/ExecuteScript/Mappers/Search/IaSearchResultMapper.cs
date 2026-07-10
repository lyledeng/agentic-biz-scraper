using System.Text.Json;
using BizScraper.Api.Common;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Search;

/// <summary>
/// Maps Iowa (US-IA) business-search output to unified search results.
/// </summary>
internal sealed class IaSearchResultMapper : ISearchResultMapper
{
    public string SlugPrefix => "us-ia";

    public UnifiedSearchResult[]? Map(JsonElement output)
    {
        var items = GetResultsArray(output);
        var results = new UnifiedSearchResult[items.GetArrayLength()];

        for (var i = 0; i < results.Length; i++)
        {
            var item = items[i];

            results[i] = new UnifiedSearchResult
            {
                Name = item.GetStringOrDefault("name"),
                Identifier = item.GetStringOrDefault("identifier"),
                Status = item.GetStringOrDefault("status"),
                EntityType = item.GetStringOrDefault("entityType"),
                FormationDate = item.GetStringOrDefault("formationDate"),
                State = "IA",
                Event = item.GetStringOrDefault("searchResultType"),
                UniqueKey = UniqueKeyEncoder.Encode("US-IA", new Dictionary<string, string>
                {
                    ["identifier"] = item.GetStringOrDefault("identifier")
                })
            };
        }

        return results;
    }

    private static JsonElement GetResultsArray(JsonElement output)
    {
        if (output.ValueKind == JsonValueKind.Array)
        {
            return output;
        }

        if (output.ValueKind == JsonValueKind.Object && output.TryGetProperty("results", out var nested) && nested.ValueKind == JsonValueKind.Array)
        {
            return nested;
        }

        return JsonSerializer.SerializeToElement(Array.Empty<object>());
    }
}
