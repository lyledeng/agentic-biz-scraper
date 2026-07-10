using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Details;

internal sealed class WaEntityDetailMapper : IEntityDetailMapper
{
    public string SlugPrefix => "us-wa";

    public UnifiedEntityDetailResponse? Map(JsonElement output)
    {
        var details = GetSection(output, "details");
        var agent = GetSection(output, "registeredAgent");
        var governors = GetSection(output, "governors");
        var filings = GetSection(output, "filings");

        return new UnifiedEntityDetailResponse
        {
            Details = new DetailSection
            {
                Name = details.GetStringOrDefault("name"),
                Identifier = details.GetStringOrDefault("ubiNumber"),
                Status = details.GetStringOrDefault("status"),
                EntityType = details.GetStringOrDefault("businessType") is "" ? null : details.GetStringOrDefault("businessType"),
                FormationDate = details.GetStringOrDefault("formationDate") is "" ? null : details.GetStringOrDefault("formationDate"),
                FormedIn = details.GetStringOrDefault("jurisdiction") is "" ? null : details.GetStringOrDefault("jurisdiction"),
                PrincipalAddress = details.GetStringOrDefault("principalOfficeStreetAddress") is "" ? null : details.GetStringOrDefault("principalOfficeStreetAddress"),
                MailingAddress = details.GetStringOrDefault("principalOfficeMailingAddress") is "" ? null : details.GetStringOrDefault("principalOfficeMailingAddress")
            },
            RegisteredAgent = agent.ValueKind == JsonValueKind.Object
                ? new AgentSection
                {
                    Name = agent.GetStringOrDefault("name") is "" ? null : agent.GetStringOrDefault("name"),
                    StreetAddress = agent.GetStringOrDefault("streetAddress") is "" ? null : agent.GetStringOrDefault("streetAddress"),
                    MailingAddress = agent.GetStringOrDefault("mailingAddress") is "" ? null : agent.GetStringOrDefault("mailingAddress")
                }
                : null,
            Parties = governors.ValueKind == JsonValueKind.Array ? MapWaGovernors(governors) : null,
            Documents = filings.ValueKind == JsonValueKind.Array ? MapWaFilings(filings) : null
        };
    }

    private static List<PartyEntry> MapWaGovernors(JsonElement governors)
    {
        var result = new List<PartyEntry>();
        foreach (var gov in governors.EnumerateArray())
        {
            result.Add(new PartyEntry
            {
                Name = BuildGovernorName(gov),
                Role = gov.GetStringOrDefault("title") is "" ? (gov.GetStringOrDefault("governorType") ?? "Unknown") : gov.GetStringOrDefault("title")!,
                Organization = gov.GetStringOrDefault("entityName") is "" ? null : gov.GetStringOrDefault("entityName")
            });
        }
        return result;
    }

    private static string BuildGovernorName(JsonElement gov)
    {
        var entityName = gov.GetStringOrDefault("entityName");
        if (!string.IsNullOrEmpty(entityName))
        {
            return entityName;
        }

        var first = gov.GetStringOrDefault("firstName") ?? "";
        var last = gov.GetStringOrDefault("lastName") ?? "";
        return $"{first} {last}".Trim();
    }

    private static List<DocumentEntry> MapWaFilings(JsonElement filings)
    {
        var result = new List<DocumentEntry>();
        foreach (var filing in filings.EnumerateArray())
        {
            var docs = new List<DownloadReference>();
            if (filing.TryGetProperty("documents", out var docsArray) && docsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var doc in docsArray.EnumerateArray())
                {
                    docs.Add(new DownloadReference
                    {
                        Label = doc.GetStringOrDefault("fileName") is { Length: > 0 } lbl ? lbl : "View Document",
                        ProxyUrl = doc.GetStringOrDefault("storageUrl") is "" ? null : doc.GetStringOrDefault("storageUrl"),
                        FileName = doc.GetStringOrDefault("fileName") is { Length: > 0 } fn ? fn : "document.pdf",
                        Error = doc.GetStringOrDefault("error") is "" ? null : doc.GetStringOrDefault("error")
                    });
                }
            }

            result.Add(new DocumentEntry
            {
                Title = filing.GetStringOrDefault("filingType") is { Length: > 0 } ft ? ft : "Unknown",
                Date = filing.GetStringOrDefault("filingDateTime") is "" ? null : filing.GetStringOrDefault("filingDateTime"),
                Downloads = docs.Count > 0 ? docs : []
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
