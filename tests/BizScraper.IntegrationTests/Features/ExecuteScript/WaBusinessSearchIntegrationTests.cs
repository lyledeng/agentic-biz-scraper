using System.Net;
using System.Net.Http.Json;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace BizScraper.IntegrationTests.Features.ExecuteScript;

public sealed class WaBusinessSearchIntegrationTests
{
    [Fact]
    public async Task PostExecuteScript_WaSearchRequest_Returns200WithEnvelope()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new[]
            {
                new
                {
                    name = "Starbucks Corporation",
                    identifier = "602014302",
                    status = "ACTIVE",
                    entityType = "WA PROFIT CORPORATION",
                    formationDate = (string?)null,
                    registeredAgentName = "CT Corporation System",
                    principalOfficeAddress = "2401 Utah Ave S Seattle WA 98134",
                    uniqueKey = "eyJzdGF0ZSI6IlVTLVdBIiwicGFyYW1zIjp7ImJ1c2luZXNzSWQiOiI2MDIwMTQzMDIiLCJidXNpbmVzc1R5cGUiOiJXQSBQUk9GSVQgQ09SUE9SQVRJT04ifX0",
                    state = "WA"
                }
            }, false));

        await using var factory = new WaTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-wa-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Starbucks" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-wa-business-search", payload.Definition);
        Assert.False(payload.Truncated);
    }

    [Fact]
    public async Task GetDefinitions_IncludesWaDefinitions()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        await using var factory = new WaTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v2/definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var definitions = await response.Content.ReadFromJsonAsync<DefinitionInfo[]>();
        Assert.NotNull(definitions);
        Assert.Contains(definitions, d => d.DefinitionSlug == "us-wa-business-search");
        Assert.Contains(definitions, d => d.DefinitionSlug == "us-wa-entity-details");
    }
}

public sealed class WaEntityDetailsIntegrationTests
{
    [Fact]
    public async Task PostExecuteScript_WaDetailRequest_Returns200WithEnvelope()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new
            {
                details = new
                {
                    name = "Starbucks Corporation",
                    ubiNumber = "602014302",
                    businessType = "WA PROFIT CORPORATION",
                    status = "ACTIVE"
                },
                registeredAgent = new { name = "CT Corporation System", streetAddress = "711 Capitol Way S Ste 204 Olympia WA 98501", mailingAddress = (string?)null },
                governors = Array.Empty<object>(),
                filings = Array.Empty<object>()
            }, false));

        await using var factory = new WaTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-wa-entity-details",
            parameters = new Dictionary<string, object?> { ["uniqueKey"] = "eyJzdGF0ZSI6IlVTLVdBIiwicGFyYW1zIjp7ImJ1c2luZXNzSWQiOiI2MDIwMTQzMDIiLCJidXNpbmVzc1R5cGUiOiJXQSBQUk9GSVQgQ09SUE9SQVRJT04ifX0" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-wa-entity-details", payload.Definition);
    }
}

internal sealed class WaTestApplicationFactory(GenericScriptScraper scraper) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<GenericScriptScraper>();
            services.AddSingleton(scraper);
            services.AddTestAuthentication();
        });
    }
}
