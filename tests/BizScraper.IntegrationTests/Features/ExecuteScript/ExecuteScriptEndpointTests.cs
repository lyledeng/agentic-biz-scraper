using System.Collections.Concurrent;
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

public sealed class ExecuteScriptEndpointTests
{
    [Fact]
    public async Task PostExecuteScript_ValidRequest_Returns200WithEnvelope()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new[] { new { name = "Acme" } }, false));

        await using var factory = new ExecuteScriptTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-co-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-Correlation-Id"));

        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-co-business-search", payload.Definition);
        Assert.False(payload.Truncated);
    }

    [Fact]
    public async Task PostExecuteScript_EmptyDefinition_Returns400()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        await using var factory = new ExecuteScriptTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "",
            parameters = new Dictionary<string, object?>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostExecuteScript_UnknownDefinition_Returns400()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        await using var factory = new ExecuteScriptTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "does-not-exist",
            parameters = new Dictionary<string, object?>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDefinitions_ReturnsArrayOfDefinitions()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        await using var factory = new ExecuteScriptTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v2/definitions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var definitions = await response.Content.ReadFromJsonAsync<DefinitionInfo[]>();
        Assert.NotNull(definitions);
        Assert.True(definitions.Length >= 8);
        Assert.Contains(definitions, d => d.DefinitionSlug == "us-ia-business-search");
        Assert.Contains(definitions, d => d.DefinitionSlug == "us-ia-entity-details");
    }

    [Fact]
    public async Task PostExecuteScript_IaSearchRequest_Returns200WithEnvelope()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new[]
            {
                new
                {
                    name = "Acme Iowa LLC",
                    identifier = "123456",
                    status = "Active",
                    entityType = "Legal",
                    formationDate = "2020-01-01",
                    detailsUrl = "https://sos.iowa.gov/search/business/Entity.aspx?BusinessNumber=123456"
                }
            }, false));

        await using var factory = new ExecuteScriptTestApplicationFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-ia-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-ia-business-search", payload.Definition);
        Assert.False(payload.Truncated);
    }
}

internal sealed class ExecuteScriptTestApplicationFactory(GenericScriptScraper scraper) : WebApplicationFactory<Program>
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
