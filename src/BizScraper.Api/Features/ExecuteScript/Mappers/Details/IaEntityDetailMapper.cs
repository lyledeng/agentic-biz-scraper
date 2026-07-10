using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Details;

internal sealed class IaEntityDetailMapper : IEntityDetailMapper
{
    public string SlugPrefix => "us-ia";

    public UnifiedEntityDetailResponse? Map(JsonElement output)
    {
        var names = GetSection(output, "names");
        var chapterCode = output.GetStringOrDefault("chapterCode") is "" ? null : output.GetStringOrDefault("chapterCode");
        var certificateNote = output.GetStringOrDefault("certificateNote") is "" ? null : output.GetStringOrDefault("certificateNote");

        List<IowaNameEntry>? iowaNames = null;
        if (names.ValueKind == JsonValueKind.Array)
        {
            iowaNames = [];
            foreach (var name in names.EnumerateArray())
            {
                iowaNames.Add(new IowaNameEntry
                {
                    Name = name.GetStringOrDefault("name"),
                    Type = name.GetStringOrDefault("type"),
                    Status = name.GetStringOrDefault("status"),
                    Modified = name.TryGetProperty("modified", out var mod) && mod.ValueKind == JsonValueKind.True
                });
            }
        }

        var details = new DetailSection
        {
            Name = output.GetStringOrDefault("legalName"),
            Identifier = output.GetStringOrDefault("identifier"),
            Status = output.GetStringOrDefault("status"),
            FormationDate = output.GetStringOrDefault("filingDate") is "" ? null : output.GetStringOrDefault("filingDate"),
            EntityType = output.GetStringOrDefault("entityType") is "" ? null : output.GetStringOrDefault("entityType"),
            ChapterCode = chapterCode,
            CertificateNote = certificateNote,
            IowaNames = iowaNames,
            PrincipalAddress = output.TryGetProperty("principalOffice", out var principal)
                ? principal.GetStringOrDefault("address")
                : null,
            MailingAddress = output.TryGetProperty("principalOffice", out var principalOffice)
                ? principalOffice.GetStringOrDefault("cityStateZip")
                : null,
            FormedIn = output.GetStringOrDefault("stateOfIncorporation") is "" ? null : output.GetStringOrDefault("stateOfIncorporation")
        };

        var agent = output.TryGetProperty("registeredAgent", out var registeredAgent)
            ? new AgentSection
            {
                Name = registeredAgent.GetStringOrDefault("fullName") is "" ? null : registeredAgent.GetStringOrDefault("fullName"),
                StreetAddress = registeredAgent.GetStringOrDefault("address") is "" ? null : registeredAgent.GetStringOrDefault("address"),
                MailingAddress = registeredAgent.GetStringOrDefault("cityStateZip") is "" ? null : registeredAgent.GetStringOrDefault("cityStateZip")
            }
            : null;

        return new UnifiedEntityDetailResponse
        {
            Details = details,
            RegisteredAgent = agent,
            Certificate = null,
            Parties = null,
            Documents = null
        };
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
