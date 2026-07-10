using System.Text.Json;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Mappers.Details;

namespace BizScraper.UnitTests.Features.ExecuteScript.Mappers;

public sealed class DeEntityDetailMapperTests
{
    private static MapperRegistry CreateRegistry() => new(
        [],
        [new CoEntityDetailMapper(), new WyEntityDetailMapper(), new IaEntityDetailMapper(), new MoEntityDetailMapper(), new WaEntityDetailMapper(), new DeEntityDetailMapper()]);

    private readonly EntityDetailMapper _mapper = new(CreateRegistry());

    [Fact]
    public void MapToUnified_DeDetails_ProducesDocumentsWithOriginalAndTranslated()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            name = "Rohde & Schwarz GmbH",
            identifier = "Amtsgericht München HRB 73851",
            status = "aktuell",
            registeredOffice = "München",
            documents = new[]
            {
                new
                {
                    title = "Current Hardcopy Printout",
                    date = (string?)null,
                    downloads = new[]
                    {
                        new { label = "Original", proxyUrl = "/api/v1/documents/corr-1/de-hardcopy-original.pdf", fileName = "SN-Leipzig_HRB_30226+AD.pdf", error = (string?)null },
                        new { label = "Translated", proxyUrl = "/api/v1/documents/corr-1/de-hardcopy.pdf", fileName = "SN-Leipzig_HRB_30226+AD-translated.pdf", error = (string?)null }
                    }
                }
            }
        });

        var result = _mapper.MapToUnified(json, "de-de-entity-details");

        Assert.NotNull(result);
        Assert.Equal("Rohde & Schwarz GmbH", result.Details.Name);
        Assert.Equal("Amtsgericht München HRB 73851", result.Details.Identifier);
        Assert.Equal("aktuell", result.Details.Status);
        Assert.Equal("München", result.Details.RegisteredOffice);
        Assert.Null(result.Details.FormationDate);
        Assert.Null(result.Details.EntityType);

        Assert.Null(result.RegisteredAgent);
        Assert.Null(result.Certificate);
        Assert.Null(result.Parties);

        Assert.NotNull(result.Documents);
        Assert.Single(result.Documents);
        var doc = result.Documents[0];
        Assert.Equal("Current Hardcopy Printout", doc.Title);
        Assert.Null(doc.Date);
        Assert.Equal(2, doc.Downloads.Count);

        var original = doc.Downloads[0];
        Assert.Equal("Original", original.Label);
        Assert.Equal("/api/v1/documents/corr-1/de-hardcopy-original.pdf", original.ProxyUrl);
        Assert.Equal("SN-Leipzig_HRB_30226+AD.pdf", original.FileName);
        Assert.Null(original.Error);

        var translated = doc.Downloads[1];
        Assert.Equal("Translated", translated.Label);
        Assert.Equal("/api/v1/documents/corr-1/de-hardcopy.pdf", translated.ProxyUrl);
        Assert.Equal("SN-Leipzig_HRB_30226+AD-translated.pdf", translated.FileName);
        Assert.Null(translated.Error);
    }

    [Fact]
    public void MapToUnified_DeDetails_PartialSuccess_TranslationFailed()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            name = "Test GmbH",
            identifier = "Amtsgericht Berlin HRB 12345",
            status = "aktuell",
            registeredOffice = "Berlin",
            documents = new[]
            {
                new
                {
                    title = "Current Hardcopy Printout",
                    date = (string?)null,
                    downloads = new[]
                    {
                        new { label = "Original", proxyUrl = (string?)"/api/v1/documents/corr-2/de-hardcopy-original.pdf", fileName = "original.pdf", error = (string?)null },
                        new { label = "Translated", proxyUrl = (string?)null, fileName = "translated.pdf", error = (string?)"Translation service unavailable" }
                    }
                }
            }
        });

        var result = _mapper.MapToUnified(json, "de-de-entity-details");

        Assert.NotNull(result);
        Assert.NotNull(result.Documents);
        Assert.Single(result.Documents);
        var doc = result.Documents[0];
        Assert.Equal(2, doc.Downloads.Count);

        var original = doc.Downloads[0];
        Assert.Equal("Original", original.Label);
        Assert.NotNull(original.ProxyUrl);
        Assert.Null(original.Error);

        var translated = doc.Downloads[1];
        Assert.Equal("Translated", translated.Label);
        Assert.Null(translated.ProxyUrl);
        Assert.Equal("Translation service unavailable", translated.Error);
    }

    [Fact]
    public void MapToUnified_DeDetails_NullSections_AreNull()
    {
        var json = JsonSerializer.SerializeToElement(new
        {
            name = "Test GmbH",
            identifier = "Amtsgericht Berlin HRB 12345",
            status = "aktuell",
            registeredOffice = "Berlin",
            documents = new[]
            {
                new
                {
                    title = "Current Hardcopy Printout",
                    date = (string?)null,
                    downloads = new[]
                    {
                        new { label = "Original", proxyUrl = "/api/v1/documents/corr-3/original.pdf", fileName = "original.pdf", error = (string?)null }
                    }
                }
            }
        });

        var result = _mapper.MapToUnified(json, "de-de-entity-details");

        Assert.NotNull(result);
        Assert.Null(result.RegisteredAgent);
        Assert.Null(result.Certificate);
        Assert.Null(result.Parties);
    }
}
