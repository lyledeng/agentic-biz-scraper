using System.Text.Json;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Mappers.Details;

namespace BizScraper.UnitTests.Features.ExecuteScript.Mappers;

public sealed class EntityDetailMapperTests
{
    private static MapperRegistry CreateRegistry() => new(
        [],
        [new CoEntityDetailMapper(), new WyEntityDetailMapper(), new IaEntityDetailMapper(), new MoEntityDetailMapper(), new WaEntityDetailMapper(), new DeEntityDetailMapper()]);

    private readonly EntityDetailMapper _mapper = new(CreateRegistry());

    [Fact]
    public void MapToUnified_CoDetails_ProducesCorrectDetailSection()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Wendy's International",
                status = "Active",
                formationDate = "01/15/1990",
                idNumber = "20201849877",
                form = "Corporation",
                periodicReportMonth = "November",
                jurisdiction = "Colorado",
                principalOfficeStreetAddress = "123 Main St, Denver, CO",
                principalOfficeMailingAddress = "PO Box 100, Denver, CO"
            },
            registeredAgent = new
            {
                name = "CT Corporation",
                streetAddress = "1700 Broadway, Denver, CO",
                mailingAddress = (string?)null
            },
            certificateUrl = "/api/v1/documents/corr-1/cert.pdf"
        });

        var result = _mapper.MapToUnified(json, "us-co-entity-details");

        Assert.NotNull(result);
        Assert.Equal("Wendy's International", result.Details.Name);
        Assert.Equal("20201849877", result.Details.Identifier);
        Assert.Equal("Active", result.Details.Status);
        Assert.Equal("01/15/1990", result.Details.FormationDate);
        Assert.Equal("Corporation", result.Details.EntityType);
        Assert.Equal("Colorado", result.Details.Jurisdiction);
        Assert.Equal("123 Main St, Denver, CO", result.Details.PrincipalAddress);
        Assert.Equal("PO Box 100, Denver, CO", result.Details.MailingAddress);
        Assert.Equal("November", result.Details.PeriodicReportMonth);

        Assert.NotNull(result.RegisteredAgent);
        Assert.Equal("CT Corporation", result.RegisteredAgent.Name);

        Assert.NotNull(result.Certificate);
        Assert.True(result.Certificate.Available);
        Assert.NotNull(result.Certificate.Downloads);
        Assert.Single(result.Certificate.Downloads);
        Assert.Equal("/api/v1/documents/corr-1/cert.pdf", result.Certificate.Downloads[0].ProxyUrl);

        Assert.Null(result.Parties);
        Assert.Null(result.Documents);
    }

    [Fact]
    public void MapToUnified_WyDetails_ProducesPartyAndDocSections()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Wendy's Technology, LLC",
                status = "Active",
                formationDate = "06/13/2017",
                idNumber = "2017-000757675",
                form = "Limited Liability Company - Foreign",
                jurisdiction = "Delaware",
                principalOfficeStreetAddress = "One Dave Thomas Blvd",
                principalOfficeMailingAddress = "One Dave Thomas Blvd",
                subStatus = "Current",
                standingTax = "Good",
                standingRA = "Good",
                standingOther = "Good",
                inactiveDate = (string?)null,
                termOfDuration = "Perpetual",
                formedIn = "Delaware",
                latestAnnualReportYear = "11347360 / 2025",
                annualReportExempt = "No",
                licenseTaxPaid = "$60.00"
            },
            registeredAgent = new
            {
                name = "United Agent Group Inc.",
                streetAddress = "5830 E 2nd St, Casper, WY 82609",
                mailingAddress = (string?)null
            },
            parties = new[]
            {
                new { name = "John Smith", role = "Manager", organization = (string?)null, address = "123 Main St" }
            },
            historyDocuments = new[]
            {
                new
                {
                    eventTitle = "2025 Original Annual Report",
                    date = "05/20/2025",
                    storageUrl = "/api/v1/documents/corr-2/2025-report.pdf",
                    fileName = "2025-report.pdf",
                    error = (string?)null
                }
            }
        });

        var result = _mapper.MapToUnified(json, "us-wy-entity-details");

        Assert.NotNull(result);
        Assert.Equal("Wendy's Technology, LLC", result.Details.Name);
        Assert.Equal("2017-000757675", result.Details.Identifier);
        Assert.Equal("Good", result.Details.StandingTax);
        Assert.Equal("Good", result.Details.StandingRA);
        Assert.Equal("Good", result.Details.StandingOther);
        Assert.Equal("Perpetual", result.Details.TermOfDuration);
        Assert.Equal("$60.00", result.Details.LicenseTaxPaid);

        Assert.NotNull(result.RegisteredAgent);

        Assert.Null(result.Certificate);

        Assert.NotNull(result.Parties);
        Assert.Single(result.Parties);
        Assert.Equal("John Smith", result.Parties[0].Name);
        Assert.Equal("Manager", result.Parties[0].Role);

        Assert.NotNull(result.Documents);
        Assert.Single(result.Documents);
        Assert.Equal("2025 Original Annual Report", result.Documents[0].Title);
        Assert.Equal("05/20/2025", result.Documents[0].Date);
        Assert.Single(result.Documents[0].Downloads);
        Assert.Equal("Filing Document", result.Documents[0].Downloads[0].Label);
        Assert.Equal("/api/v1/documents/corr-2/2025-report.pdf", result.Documents[0].Downloads[0].ProxyUrl);
    }

    [Fact]
    public void MapToUnified_WyDetails_NullSections_HandledGracefully()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Test Entity",
                status = "Active",
                formationDate = "01/01/2020",
                idNumber = "123",
                form = "LLC",
                jurisdiction = "Wyoming"
            }
        });

        var result = _mapper.MapToUnified(json, "us-wy-entity-details");

        Assert.NotNull(result);
        Assert.Null(result.RegisteredAgent);
        Assert.Null(result.Certificate);
        Assert.Null(result.Parties);
        Assert.Null(result.Documents);
    }

    [Fact]
    public void MapToUnified_NonEntityDetailsSlug_ReturnsNull()
    {
        var json = JsonSerializer.SerializeToElement(new { });
        var result = _mapper.MapToUnified(json, "us-co-business-search");

        Assert.Null(result);
    }

    [Fact]
    public void MapToUnified_WyDetails_ReturnsCertificateSection_WhenCertificateUrlPresent()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Wendy's Technology, LLC",
                status = "Active",
                formationDate = "06/13/2017",
                idNumber = "2017-000757675",
                form = "LLC",
                jurisdiction = "Wyoming"
            },
            certificateUrl = "/api/v1/documents/corr-1/2017-000757675-certificateofgoodstanding.pdf"
        });

        var result = _mapper.MapToUnified(json, "us-wy-entity-details");

        Assert.NotNull(result);
        Assert.NotNull(result.Certificate);
        Assert.True(result.Certificate.Available);
        Assert.NotNull(result.Certificate.Downloads);
        Assert.Single(result.Certificate.Downloads);
        Assert.Equal("Certificate of Good Standing", result.Certificate.Downloads[0].Label);
        Assert.Equal("/api/v1/documents/corr-1/2017-000757675-certificateofgoodstanding.pdf", result.Certificate.Downloads[0].ProxyUrl);
        Assert.Equal("2017-000757675-certificateofgoodstanding.pdf", result.Certificate.Downloads[0].FileName);
        Assert.Null(result.Certificate.Downloads[0].Error);
    }

    [Fact]
    public void MapToUnified_WyDetails_ReturnsCertificateNotAvailable_WhenCertificateErrorPresent()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Wendy S. Jacobs, LLC",
                status = "Active",
                formationDate = "01/01/2015",
                idNumber = "2015-000679087",
                form = "LLC",
                jurisdiction = "Wyoming"
            },
            certificateError = "Entity is not in good standing. Certificates may only be printed when the entity Standing is Good."
        });

        var result = _mapper.MapToUnified(json, "us-wy-entity-details");

        Assert.NotNull(result);
        Assert.NotNull(result.Certificate);
        Assert.False(result.Certificate.Available);
        Assert.Null(result.Certificate.Downloads);
        Assert.Equal("Entity is not in good standing. Certificates may only be printed when the entity Standing is Good.", result.Certificate.Error);
    }

    [Fact]
    public void MapToUnified_WyDetails_ReturnsCertificateNull_WhenNoCertificateData()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Test Entity",
                status = "Active",
                formationDate = "01/01/2020",
                idNumber = "123",
                form = "LLC",
                jurisdiction = "Wyoming"
            }
        });

        var result = _mapper.MapToUnified(json, "us-wy-entity-details");

        Assert.NotNull(result);
        Assert.Null(result.Certificate);
    }

    [Fact]
    public void MapToUnified_WyDetails_ReturnsCertificateWithError_WhenBothUrlAndErrorPresent()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "Wendy's Technology, LLC",
                status = "Active",
                formationDate = "06/13/2017",
                idNumber = "2017-000757675",
                form = "LLC",
                jurisdiction = "Wyoming"
            },
            certificateUrl = "/api/v1/documents/corr-1/cert.pdf",
            certificateError = "Cloud storage upload failed"
        });

        var result = _mapper.MapToUnified(json, "us-wy-entity-details");

        Assert.NotNull(result);
        Assert.NotNull(result.Certificate);
        Assert.True(result.Certificate.Available);
        Assert.NotNull(result.Certificate.Downloads);
        Assert.Single(result.Certificate.Downloads);
        Assert.Equal("/api/v1/documents/corr-1/cert.pdf", result.Certificate.Downloads[0].ProxyUrl);
        Assert.Equal("Cloud storage upload failed", result.Certificate.Downloads[0].Error);
    }

    [Fact]
    public void MapToUnified_IaDetails_MapsCertificateNoteAndAgent()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            identifier = "457975",
            legalName = "Acme Incorporated",
            status = "Active",
            entityType = "Legal",
            stateOfIncorporation = "IA",
            filingDate = "2015-03-15",
            chapterCode = "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY",
            registeredAgent = new
            {
                fullName = "John Smith",
                address = "123 Main Street",
                cityStateZip = "Des Moines, IA, 50309"
            },
            principalOffice = new
            {
                address = "456 Oak Avenue",
                cityStateZip = "Cedar Rapids, IA, 52402"
            },
            names = new[]
            {
                new { type = "Legal", status = "Active", name = "Acme Incorporated" }
            },
            certificateNote = "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)"
        });

        var result = _mapper.MapToUnified(json, "us-ia-entity-details");

        Assert.NotNull(result);
        Assert.Equal("Acme Incorporated", result.Details.Name);
        Assert.Equal("457975", result.Details.Identifier);
        Assert.Equal("CODE 489 DOMESTIC LIMITED LIABILITY COMPANY", result.Details.ChapterCode);
        Assert.Null(result.Details.Jurisdiction);
        Assert.NotNull(result.RegisteredAgent);
        Assert.Equal("John Smith", result.RegisteredAgent.Name);
        Assert.Null(result.Certificate);
        Assert.Equal("Certificate of Existence available via Iowa SOS website (paywall - not available via this API)", result.Details.CertificateNote);
        Assert.Null(result.Parties);
        Assert.NotNull(result.Details.IowaNames);
        Assert.Single(result.Details.IowaNames);
        Assert.Equal("Acme Incorporated", result.Details.IowaNames[0].Name);
    }

    [Fact]
    public void MapToUnified_CoDetails_BothCertAndCertifiedDocs_ProducesTwoDownloads()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "SCHOLER WENDYS COLORADO LLC",
                status = "Active",
                formationDate = "01/15/2020",
                idNumber = "20201222644",
                form = "Limited Liability Company",
                jurisdiction = "Colorado"
            },
            registeredAgent = new
            {
                name = "CT Corporation",
                streetAddress = "1700 Broadway, Denver, CO",
                mailingAddress = (string?)null
            },
            certificateUrl = "/api/v1/documents/corr-1/20201222644-certificate.pdf",
            certifiedDocumentsUrl = "/api/v1/documents/corr-1/20201222644-alldocuments.pdf"
        });

        var result = _mapper.MapToUnified(json, "us-co-entity-details");

        Assert.NotNull(result);
        Assert.NotNull(result.Certificate);
        Assert.True(result.Certificate.Available);
        Assert.NotNull(result.Certificate.Downloads);
        Assert.Equal(2, result.Certificate.Downloads.Count);
        Assert.Equal("Certificate", result.Certificate.Downloads[0].Label);
        Assert.Equal("/api/v1/documents/corr-1/20201222644-certificate.pdf", result.Certificate.Downloads[0].ProxyUrl);
        Assert.Equal("Certified Documents", result.Certificate.Downloads[1].Label);
        Assert.Equal("/api/v1/documents/corr-1/20201222644-alldocuments.pdf", result.Certificate.Downloads[1].ProxyUrl);
    }

    [Fact]
    public void MapToUnified_CoDetails_OnlyCertifiedDocs_ProducesSingleDownload()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "SCHOLER WENDYS COLORADO LLC",
                status = "Active",
                formationDate = "01/15/2020",
                idNumber = "20201222644",
                form = "Limited Liability Company",
                jurisdiction = "Colorado"
            },
            certifiedDocumentsUrl = "/api/v1/documents/corr-1/20201222644-alldocuments.pdf"
        });

        var result = _mapper.MapToUnified(json, "us-co-entity-details");

        Assert.NotNull(result);
        Assert.NotNull(result.Certificate);
        Assert.True(result.Certificate.Available);
        Assert.NotNull(result.Certificate.Downloads);
        Assert.Single(result.Certificate.Downloads);
        Assert.Equal("Certified Documents", result.Certificate.Downloads[0].Label);
        Assert.Equal("/api/v1/documents/corr-1/20201222644-alldocuments.pdf", result.Certificate.Downloads[0].ProxyUrl);
        Assert.Contains("alldocuments.pdf", result.Certificate.Downloads[0].FileName, StringComparison.Ordinal);
    }

    [Fact]
    public void MapToUnified_CoDetails_NoCertificateOrCertifiedDocs_ReturnsNullCertificate()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            details = new
            {
                name = "SCHOLER WENDYS COLORADO LLC",
                status = "Active",
                formationDate = "01/15/2020",
                idNumber = "20201222644",
                form = "Limited Liability Company",
                jurisdiction = "Colorado"
            }
        });

        var result = _mapper.MapToUnified(json, "us-co-entity-details");

        Assert.NotNull(result);
        Assert.Null(result.Certificate);
    }
}
