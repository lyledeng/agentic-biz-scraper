using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Details;

internal sealed class WyEntityDetailMapper : IEntityDetailMapper
{
    public string SlugPrefix => "us-wy";

    public UnifiedEntityDetailResponse? Map(JsonElement output)
    {
        var details = GetSection(output, "details");
        var agent = GetSection(output, "registeredAgent");
        var parties = GetSection(output, "parties");
        var historyDocs = GetSection(output, "historyDocuments");

        var certificateUrl = output.GetStringOrDefault("certificateUrl");
        var certificateError = output.GetStringOrDefault("certificateError");

        return new UnifiedEntityDetailResponse
        {
            Details = MapDetailSection(details),
            RegisteredAgent = agent.ValueKind != JsonValueKind.Undefined && agent.ValueKind != JsonValueKind.Null
                ? MapAgentSection(agent)
                : null,
            Certificate = !string.IsNullOrEmpty(certificateUrl)
                ? new CertificateSection
                {
                    Available = true,
                    Downloads = [new DownloadReference
                    {
                        Label = "Certificate of Good Standing",
                        ProxyUrl = certificateUrl,
                        FileName = certificateUrl.Contains('/', StringComparison.Ordinal)
                            ? certificateUrl[(certificateUrl.LastIndexOf('/') + 1)..]
                            : "certificate.pdf",
                        Error = string.IsNullOrEmpty(certificateError) ? null : certificateError
                    }]
                }
                : !string.IsNullOrEmpty(certificateError)
                    ? new CertificateSection { Available = false, Error = certificateError }
                    : null,
            Parties = parties.ValueKind == JsonValueKind.Array
                ? MapParties(parties)
                : null,
            Documents = historyDocs.ValueKind == JsonValueKind.Array
                ? MapHistoryDocuments(historyDocs)
                : null
        };
    }

    private static DetailSection MapDetailSection(JsonElement details)
    {
        if (details.ValueKind == JsonValueKind.Undefined || details.ValueKind == JsonValueKind.Null)
        {
            return new DetailSection { Name = string.Empty, Identifier = string.Empty, Status = string.Empty };
        }

        return new DetailSection
        {
            Name = details.GetStringOrDefault("name"),
            Identifier = details.GetStringOrDefault("idNumber"),
            Status = details.GetStringOrDefault("status"),
            FormationDate = details.GetStringOrDefault("formationDate") is "" ? null : details.GetStringOrDefault("formationDate"),
            EntityType = details.GetStringOrDefault("form") is "" ? null : details.GetStringOrDefault("form"),
            Jurisdiction = details.GetStringOrDefault("jurisdiction") is "" ? null : details.GetStringOrDefault("jurisdiction"),
            PrincipalAddress = details.GetStringOrDefault("principalOfficeStreetAddress") is "" ? null : details.GetStringOrDefault("principalOfficeStreetAddress"),
            MailingAddress = details.GetStringOrDefault("principalOfficeMailingAddress") is "" ? null : details.GetStringOrDefault("principalOfficeMailingAddress"),
            PeriodicReportMonth = details.GetStringOrDefault("periodicReportMonth") is "" ? null : details.GetStringOrDefault("periodicReportMonth"),
            SubStatus = details.GetStringOrDefault("subStatus") is "" ? null : details.GetStringOrDefault("subStatus"),
            StandingTax = details.GetStringOrDefault("standingTax") is "" ? null : details.GetStringOrDefault("standingTax"),
            StandingRA = details.GetStringOrDefault("standingRA") is "" ? null : details.GetStringOrDefault("standingRA"),
            StandingOther = details.GetStringOrDefault("standingOther") is "" ? null : details.GetStringOrDefault("standingOther"),
            InactiveDate = details.GetStringOrDefault("inactiveDate") is "" ? null : details.GetStringOrDefault("inactiveDate"),
            TermOfDuration = details.GetStringOrDefault("termOfDuration") is "" ? null : details.GetStringOrDefault("termOfDuration"),
            FormedIn = details.GetStringOrDefault("formedIn") is "" ? null : details.GetStringOrDefault("formedIn"),
            LatestAnnualReportYear = details.GetStringOrDefault("latestAnnualReportYear") is "" ? null : details.GetStringOrDefault("latestAnnualReportYear"),
            AnnualReportExempt = details.GetStringOrDefault("annualReportExempt") is "" ? null : details.GetStringOrDefault("annualReportExempt"),
            LicenseTaxPaid = details.GetStringOrDefault("licenseTaxPaid") is "" ? null : details.GetStringOrDefault("licenseTaxPaid")
        };
    }

    private static AgentSection MapAgentSection(JsonElement agent) =>
        new()
        {
            Name = agent.GetStringOrDefault("name") is "" ? null : agent.GetStringOrDefault("name"),
            StreetAddress = agent.GetStringOrDefault("streetAddress") is "" ? null : agent.GetStringOrDefault("streetAddress"),
            MailingAddress = agent.GetStringOrDefault("mailingAddress") is "" ? null : agent.GetStringOrDefault("mailingAddress")
        };

    private static List<PartyEntry> MapParties(JsonElement parties)
    {
        var result = new List<PartyEntry>();
        foreach (var party in parties.EnumerateArray())
        {
            result.Add(new PartyEntry
            {
                Name = party.GetStringOrDefault("name"),
                Role = party.GetStringOrDefault("role"),
                Organization = party.GetStringOrDefault("organization") is "" ? null : party.GetStringOrDefault("organization"),
                Address = party.GetStringOrDefault("address") is "" ? null : party.GetStringOrDefault("address")
            });
        }

        return result;
    }

    private static List<DocumentEntry> MapHistoryDocuments(JsonElement historyDocs)
    {
        var result = new List<DocumentEntry>();
        foreach (var doc in historyDocs.EnumerateArray())
        {
            var storageUrl = doc.GetStringOrDefault("storageUrl");
            var fileName = doc.GetStringOrDefault("fileName");
            var error = doc.GetStringOrDefault("error");

            result.Add(new DocumentEntry
            {
                Title = doc.GetStringOrDefault("eventTitle"),
                Date = doc.GetStringOrDefault("date") is "" ? null : doc.GetStringOrDefault("date"),
                Downloads =
                [
                    new DownloadReference
                    {
                        Label = "Filing Document",
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
