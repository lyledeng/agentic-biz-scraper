using System.Text.Json;
using BizScraper.Api.Common.Models;

namespace BizScraper.Api.Features.ExecuteScript.Mappers.Details;

internal sealed class CoEntityDetailMapper : IEntityDetailMapper
{
    public string SlugPrefix => "us-co";

    public UnifiedEntityDetailResponse? Map(JsonElement output)
    {
        var details = GetSection(output, "details");
        var agent = GetSection(output, "registeredAgent");

        // CO stores certificate and certified documents as proxy URL strings
        var certificateUrl = output.GetStringOrDefault("certificateUrl");
        var certifiedDocumentsUrl = output.GetStringOrDefault("certifiedDocumentsUrl");

        var downloads = new List<DownloadReference>();

        if (!string.IsNullOrEmpty(certificateUrl))
        {
            downloads.Add(new DownloadReference
            {
                Label = "Certificate",
                ProxyUrl = certificateUrl,
                FileName = certificateUrl.Contains('/', StringComparison.Ordinal) ? certificateUrl[(certificateUrl.LastIndexOf('/') + 1)..] : "certificate.pdf",
            });
        }

        if (!string.IsNullOrEmpty(certifiedDocumentsUrl))
        {
            downloads.Add(new DownloadReference
            {
                Label = "Certified Documents",
                ProxyUrl = certifiedDocumentsUrl,
                FileName = certifiedDocumentsUrl.Contains('/', StringComparison.Ordinal) ? certifiedDocumentsUrl[(certifiedDocumentsUrl.LastIndexOf('/') + 1)..] : "alldocuments.pdf",
            });
        }

        return new UnifiedEntityDetailResponse
        {
            Details = MapDetailSection(details),
            RegisteredAgent = agent.ValueKind != JsonValueKind.Undefined && agent.ValueKind != JsonValueKind.Null
                ? MapAgentSection(agent)
                : null,
            Certificate = downloads.Count > 0
                ? new CertificateSection
                {
                    Available = true,
                    Downloads = downloads
                }
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

    private static JsonElement GetSection(JsonElement output, string sectionName)
    {
        if (output.ValueKind == JsonValueKind.Object && output.TryGetProperty(sectionName, out var section))
        {
            return section;
        }

        return default;
    }
}
