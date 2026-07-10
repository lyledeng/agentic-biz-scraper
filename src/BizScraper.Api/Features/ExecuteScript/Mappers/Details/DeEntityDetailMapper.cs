using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Details;

internal sealed class DeEntityDetailMapper : IEntityDetailMapper
{
    public string SlugPrefix => "de-de";

    public UnifiedEntityDetailResponse? Map(JsonElement output)
    {
        var name = output.GetStringOrDefault("name");
        var identifier = output.GetStringOrDefault("identifier");
        var status = output.GetStringOrDefault("status");
        var registeredOffice = output.GetStringOrDefault("registeredOffice");

        return new UnifiedEntityDetailResponse
        {
            Details = new DetailSection
            {
                Name = name,
                Identifier = identifier,
                Status = status,
                RegisteredOffice = registeredOffice is "" ? null : registeredOffice
            },
            Documents = MapDeDocuments(output)
        };
    }

    private static List<DocumentEntry>? MapDeDocuments(JsonElement output)
    {
        if (!output.TryGetProperty("documents", out var docs) || docs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var result = new List<DocumentEntry>();
        foreach (var doc in docs.EnumerateArray())
        {
            var title = doc.GetStringOrDefault("title");
            var date = doc.GetStringOrDefault("date") is "" ? null : doc.GetStringOrDefault("date");
            var downloads = new List<DownloadReference>();

            if (doc.TryGetProperty("downloads", out var dlArray) && dlArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var dl in dlArray.EnumerateArray())
                {
                    var proxyUrl = dl.GetStringOrDefault("proxyUrl");
                    var error = dl.GetStringOrDefault("error");
                    downloads.Add(new DownloadReference
                    {
                        Label = dl.GetStringOrDefault("label"),
                        ProxyUrl = proxyUrl is "" ? null : proxyUrl,
                        FileName = dl.GetStringOrDefault("fileName") is "" ? "document.pdf" : dl.GetStringOrDefault("fileName"),
                        Error = error is "" ? null : error
                    });
                }
            }

            result.Add(new DocumentEntry
            {
                Title = title,
                Date = date,
                Downloads = downloads
            });
        }

        return result;
    }
}
