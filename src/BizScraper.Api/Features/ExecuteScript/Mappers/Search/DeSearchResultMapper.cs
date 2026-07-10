using System.Text.Json;
using BizScraper.Api.Common;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Search;

/// <summary>
/// Maps Germany (DE-DE) business-search output to unified search results.
/// </summary>
internal sealed class DeSearchResultMapper : ISearchResultMapper
{
    public string SlugPrefix => "de-de";

    public UnifiedSearchResult[]? Map(JsonElement output)
    {
        var items = GetResultsArray(output);
        var results = new UnifiedSearchResult[items.GetArrayLength()];

        for (var i = 0; i < results.Length; i++)
        {
            var item = items[i];
            var registrationId = item.GetStringOrDefault("registrationId");
            var companyName = item.GetStringOrDefault("companyName");

            results[i] = new UnifiedSearchResult
            {
                Name = companyName,
                Identifier = registrationId,
                Status = item.GetStringOrDefault("status"),
                State = "DE",
                UniqueKey = UniqueKeyEncoder.Encode("DE-DE", new Dictionary<string, string>
                {
                    ["searchTerm"] = companyName,
                    ["registrationId"] = registrationId
                }),
                RegisteredOffice = item.GetStringOrDefault("registeredOffice")
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
