using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Details;

internal sealed class MoEntityDetailMapper : IEntityDetailMapper
{
    public string SlugPrefix => "us-mo";

    public UnifiedEntityDetailResponse? Map(JsonElement output)
    {
        var details = GetSection(output, "details");
        var filings = GetSection(output, "filings");
        var addresses = GetSection(output, "addresses");

        var agentName = details.GetStringOrDefault("registeredAgentName");
        var agentAddress = details.GetStringOrDefault("registeredAgentAddress");

        // Build principal address from addresses array (first entry)
        string? principalAddress = null;
        if (addresses.ValueKind == JsonValueKind.Array)
        {
            foreach (var addr in addresses.EnumerateArray())
            {
                var fullAddress = addr.GetStringOrDefault("address");
                if (!string.IsNullOrEmpty(fullAddress))
                {
                    principalAddress = fullAddress;
                    break;
                }
            }
        }

        return new UnifiedEntityDetailResponse
        {
            Details = new DetailSection
            {
                Name = details.GetStringOrDefault("name"),
                Identifier = details.GetStringOrDefault("charterNumber"),
                Status = details.GetStringOrDefault("status"),
                EntityType = details.GetStringOrDefault("entityType") is "" ? null : details.GetStringOrDefault("entityType"),
                FormationDate = details.GetStringOrDefault("dateFormed") is "" ? null : details.GetStringOrDefault("dateFormed"),
                FormedIn = details.GetStringOrDefault("homeState") is "" ? null : details.GetStringOrDefault("homeState"),
                PrincipalAddress = principalAddress
            },
            RegisteredAgent = !string.IsNullOrEmpty(agentName)
                ? new AgentSection
                {
                    Name = agentName,
                    StreetAddress = string.IsNullOrEmpty(agentAddress) ? null : agentAddress
                }
                : null,
            Documents = filings.ValueKind == JsonValueKind.Array ? MapMoFilings(filings) : null
        };
    }

    private static List<DocumentEntry> MapMoFilings(JsonElement filings)
    {
        var result = new List<DocumentEntry>();
        foreach (var filing in filings.EnumerateArray())
        {
            var storageUrl = filing.GetStringOrDefault("storageUrl");
            var fileName = filing.GetStringOrDefault("fileName");
            var error = filing.GetStringOrDefault("error");

            result.Add(new DocumentEntry
            {
                Title = filing.GetStringOrDefault("documentType"),
                Date = filing.GetStringOrDefault("dateFiled") is "" ? null : filing.GetStringOrDefault("dateFiled"),
                Downloads =
                [
                    new DownloadReference
                    {
                        Label = filing.GetStringOrDefault("action") is "" ? "View Document" : filing.GetStringOrDefault("action"),
                        ProxyUrl = string.IsNullOrEmpty(storageUrl) ? null : storageUrl,
                        FileName = !string.IsNullOrEmpty(fileName) ? fileName : "document.pdf",
                        Error = string.IsNullOrEmpty(error) ? null : error
                    }
                ]
            });
        }

        return result;
    }

    private static JsonElement GetSection(JsonElement output, string sectionName)
    {
        if (output.ValueKind == JsonValueKind.Object && output.TryGetProperty(sectionName, out var section))
        {
            return section;
        }

        return default;
    }
}
