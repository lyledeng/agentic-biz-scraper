using System.Text.Json;
using BizScraper.Api.Common;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Search;

/// <summary>
/// Maps Colorado (US-CO) business-search output to unified search results.
/// </summary>
internal sealed class CoSearchResultMapper : ISearchResultMapper
{
    public string SlugPrefix => "us-co";

    public UnifiedSearchResult[]? Map(JsonElement output)
    {
        var items = GetResultsArray(output);
        var results = new UnifiedSearchResult[items.GetArrayLength()];

        for (var i = 0; i < results.Length; i++)
        {
            var item = items[i];
            var detailsUrl = item.GetStringOrDefault("detailsUrl");
            var uniqueKeyParams = ExtractCoParamsFromUrl(detailsUrl);

            results[i] = new UnifiedSearchResult
            {
                Name = item.GetStringOrDefault("name"),
                Identifier = item.GetStringOrDefault("identifier"),
                Status = item.GetStringOrDefault("status"),
                EntityType = item.GetStringOrDefault("entityType"),
                FormationDate = item.GetStringOrDefault("formationDate"),
                State = "CO",
                Event = item.GetStringOrDefault("event"),
                UniqueKey = UniqueKeyEncoder.Encode("US-CO", uniqueKeyParams)
            };
        }

        return results;
    }

    private static Dictionary<string, string> ExtractCoParamsFromUrl(string detailsUrl)
    {
        var parameters = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(detailsUrl))
        {
            return parameters;
        }

        // Store the full detailsUrl — the CO entity-details definition navigates to it directly
        parameters["detailsUrl"] = detailsUrl;
        return parameters;
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
