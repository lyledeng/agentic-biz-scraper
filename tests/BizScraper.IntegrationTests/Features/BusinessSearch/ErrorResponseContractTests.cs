using System.Net;
using System.Net.Http.Json;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using NSubstitute;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

public sealed class ErrorResponseContractTests
{
    [Fact]
    public async Task ExecuteScript_ReturnsProblemDetailsFor422And502()
    {
        // 422: ExceededRecordCountException
        var scraper422 = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper422.ExecuteAsync(
                Arg.Any<FlowDefinitionV2>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<FlowExecutionResult>(_ => throw new ExceededRecordCountException("Exceeded Record Count"));

        await using var factory422 = new V2BusinessSearchTestFactory(scraper422);
        var client422 = factory422.CreateClient();

        var response422 = await client422.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-co-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "a" }
        });

        // 502: UpstreamException
        var scraper502 = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper502.ExecuteAsync(
                Arg.Any<FlowDefinitionV2>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns<FlowExecutionResult>(_ => throw new UpstreamException("Upstream failure"));

        await using var factory502 = new V2BusinessSearchTestFactory(scraper502);
        var client502 = factory502.CreateClient();

        var response502 = await client502.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-co-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal((HttpStatusCode)422, response422.StatusCode);
        Assert.Equal("application/problem+json", response422.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadGateway, response502.StatusCode);
        Assert.Equal("application/problem+json", response502.Content.Headers.ContentType?.MediaType);
    }
}
