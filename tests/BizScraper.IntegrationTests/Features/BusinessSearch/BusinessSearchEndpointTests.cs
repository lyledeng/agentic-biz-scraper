using System.Net;
using System.Net.Http.Json;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using NSubstitute;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

public sealed class BusinessSearchEndpointTests
{
    [Fact]
    public async Task PostExecuteScript_CoBusinessSearch_ReturnsJsonResponse()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(
                Arg.Any<FlowDefinitionV2>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(
                new[] { new { name = "Acme Test", identifier = "DOC001", status = "Good Standing", entityType = "LLC", state = "CO" } },
                false));

        await using var factory = new V2BusinessSearchTestFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-co-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        var payload = await response.Content.ReadFromJsonAsync<ExecuteScriptResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.NotNull(payload);
        Assert.Equal("us-co-business-search", payload.Definition);
    }
}
