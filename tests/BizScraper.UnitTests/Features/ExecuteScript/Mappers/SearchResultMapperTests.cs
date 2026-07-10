using System.Text.Json;
using BizScraper.Api.Common;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Mappers.Search;

namespace BizScraper.UnitTests.Features.ExecuteScript.Mappers;

public sealed class SearchResultMapperTests
{
    private static MapperRegistry CreateRegistry() => new(
        [new CoSearchResultMapper(), new WySearchResultMapper(), new IaSearchResultMapper(), new MoSearchResultMapper(), new WaSearchResultMapper(), new DeSearchResultMapper()],
        []);

    private readonly SearchResultMapper _mapper = new(CreateRegistry());

    [Fact]
    public void MapToUnified_CoSearchResults_ProducesCorrectFields()
    {
        var json = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                name = "Wendy's of Limon",
                identifier = "20201849877",
                status = "Effective",
                entityType = "FPC",
                formationDate = "09/30/2020",
                state = "CO",
                @event = "Trade Name",
                detailsUrl = "https://www.sos.state.co.us/biz/ViewImage.do?masterFileId=20201849877&fileId=20201849877"
            }
        });

        var results = _mapper.MapToUnified(json, "us-co-business-search");

        Assert.NotNull(results);
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Wendy's of Limon", result.Name);
        Assert.Equal("20201849877", result.Identifier);
        Assert.Equal("Effective", result.Status);
        Assert.Equal("FPC", result.EntityType);
        Assert.Equal("09/30/2020", result.FormationDate);
        Assert.Equal("CO", result.State);
        Assert.Equal("Trade Name", result.Event);
        Assert.NotNull(result.UniqueKey);
        Assert.NotEmpty(result.UniqueKey);
        Assert.Null(result.StandingTax);
        Assert.Null(result.StandingRA);
        Assert.Null(result.RegisteredOffice);
    }

    [Fact]
    public void MapToUnified_CoSearchResults_UniqueKeyDecodesCorrectly()
    {
        var json = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                name = "Test Entity",
                identifier = "123",
                status = "Active",
                entityType = "LLC",
                formationDate = "01/01/2020",
                state = "CO",
                @event = (string?)null,
                detailsUrl = "https://www.sos.state.co.us/biz/ViewImage.do?masterFileId=123&fileId=456&entityId2=789"
            }
        });

        var results = _mapper.MapToUnified(json, "us-co-business-search");

        Assert.NotNull(results);
        Assert.True(UniqueKeyEncoder.TryDecode(results[0].UniqueKey, out var state, out var parameters, out _));
        Assert.Equal("US-CO", state);
        Assert.True(parameters.ContainsKey("masterFileId") || parameters.ContainsKey("detailsUrl"));
    }

    [Fact]
    public void MapToUnified_WySearchResults_ProducesCorrectFields()
    {
        // WY mapper now expects raw text blocks — the same format the extract JS produces
        var json = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                text = "Wendy's Technology, LLC - 2017-000757675 (Profit Corporation) Status: Active Standing - Tax: Good Standing - RA: Good Filed On: 06/13/2017",
                detailsUrl = "https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=095211136"
            }
        });

        var results = _mapper.MapToUnified(json, "us-wy-business-search");

        Assert.NotNull(results);
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Wendy's Technology, LLC", result.Name);
        Assert.Equal("2017-000757675", result.Identifier);
        Assert.Equal("Active", result.Status);
        Assert.Equal("WY", result.State);
        Assert.Equal("Good", result.StandingTax);
        Assert.Equal("Good", result.StandingRA);
        Assert.NotNull(result.UniqueKey);
    }

    [Fact]
    public void MapToUnified_DeSearchResults_ProducesCorrectFields()
    {
        // DE results come nested in data.results from the definition
        var json = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                companyName = "Rohde & Schwarz GmbH",
                registeredOffice = "München",
                status = "aktuell",
                registrationId = "Amtsgericht München HRB 73851"
            }
        });

        var results = _mapper.MapToUnified(json, "de-de-business-search");

        Assert.NotNull(results);
        Assert.Single(results);
        var result = results[0];
        Assert.Equal("Rohde & Schwarz GmbH", result.Name);
        Assert.Equal("Amtsgericht München HRB 73851", result.Identifier);
        Assert.Equal("aktuell", result.Status);
        Assert.Equal("DE", result.State);
        Assert.Equal("München", result.RegisteredOffice);
        Assert.Null(result.EntityType);
        Assert.Null(result.FormationDate);
        Assert.Null(result.Event);
        Assert.NotNull(result.UniqueKey);
    }

    [Fact]
    public void MapToUnified_DeNestedResults_FlattensToArray()
    {
        // DE data may come as { results: [...] } from the flow engine
        var json = JsonSerializer.SerializeToElement(new
        {
            results = new[]
            {
                new
                {
                    companyName = "Company A",
                    registeredOffice = "Berlin",
                    status = "aktuell",
                    registrationId = "HRB 1111"
                },
                new
                {
                    companyName = "Company B",
                    registeredOffice = "Hamburg",
                    status = "gelöscht",
                    registrationId = "HRB 2222"
                }
            }
        });

        var results = _mapper.MapToUnified(json, "de-de-business-search");

        Assert.NotNull(results);
        Assert.Equal(2, results.Length);
        Assert.Equal("Company A", results[0].Name);
        Assert.Equal("Company B", results[1].Name);
    }

    [Fact]
    public void MapToUnified_SearchResults_NoDetailsUrl()
    {
        var json = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                name = "Test",
                identifier = "123",
                status = "Active",
                entityType = "LLC",
                formationDate = "01/01/2020",
                state = "CO",
                @event = (string?)null,
                detailsUrl = "https://example.com"
            }
        });

        var results = _mapper.MapToUnified(json, "us-co-business-search");

        Assert.NotNull(results);
        // Verify the unified result does NOT have a detailsUrl property
        var resultJson = JsonSerializer.Serialize(results[0]);
        Assert.DoesNotContain("detailsUrl", resultJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapToUnified_NonSearchSlug_ReturnsNull()
    {
        var json = JsonSerializer.SerializeToElement(new { });
        var results = _mapper.MapToUnified(json, "us-co-entity-details");

        Assert.Null(results);
    }

    [Fact]
    public void MapToUnified_IaSearchResults_ProducesUniqueKeyAndState()
    {
        var json = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                identifier = "457975",
                name = "Acme Iowa LLC",
                status = "Active",
                entityType = "Legal",
                formationDate = "2015-03-15",
                detailsUrl = "https://sos.iowa.gov/search/business/Entity.aspx?BusinessNumber=457975",
                searchResultType = "legal-name"
            }
        });

        var results = _mapper.MapToUnified(json, "us-ia-business-search");

        Assert.NotNull(results);
        Assert.Single(results);
        Assert.Equal("IA", results[0].State);
        Assert.Equal("legal-name", results[0].Event);
        Assert.True(UniqueKeyEncoder.TryDecode(results[0].UniqueKey, out var state, out var parameters, out _));
        Assert.Equal("US-IA", state);
        Assert.Contains("identifier", parameters.Keys);
        Assert.Equal("457975", parameters["identifier"]);
    }
}
