using System.Net;
using System.Net.Http.Json;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using NSubstitute;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

public sealed class MultiStateEndpointTests
{
    [Fact]
    public async Task CoSearch_ReturnsSuccessViaV2()
    {
        var scraper = CreateMockScraper();
        await using var factory = new V2BusinessSearchTestFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-co-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-co-business-search", payload.Definition);
    }

    [Fact]
    public async Task WySearch_ReturnsSuccessViaV2()
    {
        var scraper = CreateMockScraper();
        await using var factory = new V2BusinessSearchTestFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-wy-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();
        Assert.NotNull(payload);
        Assert.Equal("us-wy-business-search", payload.Definition);
    }

    [Fact]
    public async Task UnknownDefinition_Returns400()
    {
        var scraper = CreateMockScraper();
        await using var factory = new V2BusinessSearchTestFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-tx-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EmptyDefinition_Returns400()
    {
        var scraper = CreateMockScraper();
        await using var factory = new V2BusinessSearchTestFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "",
            parameters = new Dictionary<string, object?>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static GenericScriptScraper CreateMockScraper()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(
                Arg.Any<FlowDefinitionV2>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(
                new[] { new { name = "Acme", identifier = "DOC001", status = "Active" } },
                false));
        return scraper;
    }
}
