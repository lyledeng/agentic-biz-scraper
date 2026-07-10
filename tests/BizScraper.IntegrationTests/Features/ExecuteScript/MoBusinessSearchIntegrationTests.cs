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

public sealed class MoBusinessSearchIntegrationTests
{
    [Fact]
    public async Task PostExecuteScript_MoSearchRequest_Returns200WithEnvelope()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new[]
            {
                new
                {
                    name = "Smith Industries LLC",
                    identifier = "FL1234567",
                    status = "Active",
                    entityType = "Limited Liability Company",
                    formationDate = "01/15/2020",
                    registeredAgentName = "John Smith",
                    previousNames = Array.Empty<string>(),
                    uniqueKey = "eyJzdGF0ZSI6IlVTLU1PIiwicGFyYW1zIjp7ImVudGl0eUlkIjoiMTIzNDU2NyJ9fQ",
                    state = "MO"
                }
            }, false));

        await using var factory = new MoTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-mo-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Smith" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-mo-business-search", payload.Definition);
        Assert.False(payload.Truncated);
    }

    [Fact]
    public async Task GetDefinitions_IncludesMoDefinitions()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        await using var factory = new MoTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v2/definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var definitions = await response.Content.ReadFromJsonAsync<DefinitionInfo[]>();
        Assert.NotNull(definitions);
        Assert.Contains(definitions, d => d.DefinitionSlug == "us-mo-business-search");
        Assert.Contains(definitions, d => d.DefinitionSlug == "us-mo-entity-details");
    }
}

public sealed class MoEntityDetailsIntegrationTests
{
    [Fact]
    public async Task PostExecuteScript_MoDetailRequest_Returns200WithEnvelope()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new
            {
                details = new { name = "Smith Industries LLC", charterNumber = "FL1234567", status = "Active" },
                filings = Array.Empty<object>(),
                addresses = Array.Empty<object>()
            }, false));

        await using var factory = new MoTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-mo-entity-details",
            parameters = new Dictionary<string, object?> { ["uniqueKey"] = "eyJzdGF0ZSI6IlVTLU1PIiwicGFyYW1zIjp7ImVudGl0eUlkIjoiMTIzNDU2NyJ9fQ" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-mo-entity-details", payload.Definition);
    }
}

internal sealed class MoTestApplicationFactory(GenericScriptScraper scraper) : WebApplicationFactory<Program>
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
