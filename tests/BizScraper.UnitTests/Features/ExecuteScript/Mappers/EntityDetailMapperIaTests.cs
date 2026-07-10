using System.Text.Json;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Mappers.Details;

namespace BizScraper.UnitTests.Features.ExecuteScript.Mappers;

public sealed class EntityDetailMapperIaTests
{
    private static MapperRegistry CreateRegistry() => new(
        [],
        [new CoEntityDetailMapper(), new WyEntityDetailMapper(), new IaEntityDetailMapper(), new MoEntityDetailMapper(), new WaEntityDetailMapper(), new DeEntityDetailMapper()]);

    private readonly EntityDetailMapper _mapper = new(CreateRegistry());

    [Fact]
    public void MapIaDetails_PopulatesIowaSpecificFields()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            legalName = "AACME LLC",
            identifier = "586027",
            status = "Active",
            filingDate = "11/13/2013",
            entityType = "LLC - Domestic Limited Liability Company",
            chapterCode = "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY",
            stateOfIncorporation = "Iowa",
            principalOffice = new
            {
                address = "1234 Main St",
                cityStateZip = "Des Moines, IA 50309"
            },
            registeredAgent = new
            {
                fullName = "JOHN DOE",
                address = "5678 Elm St",
                cityStateZip = "Cedar Rapids, IA 52401"
            },
            certificateNote = "Certificate of Good Standing is available via the paywall.",
            names = new[]
            {
                new { name = "AACME LLC", type = "Legal", status = "Active", modified = false },
                new { name = "AACME HOLDINGS", type = "Fictitious Name", status = "Active", modified = true }
            }
        });

        var result = _mapper.MapToUnified(json, "us-ia-entity-details");

        Assert.NotNull(result);

        // Iowa-specific fields on DetailSection
        Assert.Equal("CODE 489 DOMESTIC LIMITED LIABILITY COMPANY", result.Details.ChapterCode);
        Assert.Equal("Certificate of Good Standing is available via the paywall.", result.Details.CertificateNote);

        // IowaNames populated as IowaNameEntry list (not PartyEntry)
        Assert.NotNull(result.Details.IowaNames);
        Assert.Equal(2, result.Details.IowaNames.Count);
        Assert.Equal("AACME LLC", result.Details.IowaNames[0].Name);
        Assert.Equal("Legal", result.Details.IowaNames[0].Type);
        Assert.Equal("Active", result.Details.IowaNames[0].Status);
        Assert.False(result.Details.IowaNames[0].Modified);
        Assert.Equal("AACME HOLDINGS", result.Details.IowaNames[1].Name);
        Assert.True(result.Details.IowaNames[1].Modified);

        // Jurisdiction should NOT be populated with chapterCode
        Assert.Null(result.Details.Jurisdiction);

        // Certificate should NOT contain the note as Error
        Assert.Null(result.Certificate);

        // Parties should be empty (names go to IowaNames, not PartyEntry)
        Assert.Null(result.Parties);

        // Standard fields still populated
        Assert.Equal("AACME LLC", result.Details.Name);
        Assert.Equal("586027", result.Details.Identifier);
        Assert.Equal("Active", result.Details.Status);
        Assert.Equal("11/13/2013", result.Details.FormationDate);
        Assert.Equal("Iowa", result.Details.FormedIn);
        Assert.Equal("1234 Main St", result.Details.PrincipalAddress);

        // Registered agent still populated
        Assert.NotNull(result.RegisteredAgent);
        Assert.Equal("JOHN DOE", result.RegisteredAgent.Name);
    }

    [Fact]
    public void MapIaDetails_NullOptionalFields_HandledGracefully()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            legalName = "Test Entity",
            identifier = "123456",
            status = "Active",
            filingDate = "",
            entityType = "",
            chapterCode = "",
            stateOfIncorporation = ""
        });

        var result = _mapper.MapToUnified(json, "us-ia-entity-details");

        Assert.NotNull(result);
        Assert.Equal("Test Entity", result.Details.Name);
        Assert.Null(result.Details.ChapterCode);
        Assert.Null(result.Details.CertificateNote);
        Assert.Null(result.Details.IowaNames);
        Assert.Null(result.Details.Jurisdiction);
        Assert.Null(result.Certificate);
        Assert.Null(result.Parties);
        Assert.Null(result.RegisteredAgent);
    }
}
