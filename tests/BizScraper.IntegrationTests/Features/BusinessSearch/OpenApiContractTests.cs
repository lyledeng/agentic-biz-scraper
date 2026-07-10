using System.Net;
using System.Net.Http.Json;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using NSubstitute;

namespace BizScraper.IntegrationTests.Features.BusinessSearch;

public sealed class OpenApiContractTests
{
    [Fact]
    public async Task OpenApiFile_ContainsDocumentedResponseCodes()
    {
        var openApi = await File.ReadAllTextAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "specs", "001-cosos-business-search-api", "contracts", "openapi.yaml")));

        Assert.Contains("\"200\":", openApi, StringComparison.Ordinal);
        Assert.Contains("\"400\":", openApi, StringComparison.Ordinal);
        Assert.Contains("\"422\":", openApi, StringComparison.Ordinal);
        Assert.Contains("\"502\":", openApi, StringComparison.Ordinal);
        Assert.Contains("\"503\":", openApi, StringComparison.Ordinal);
    }

    [Fact]
    public async Task V2ExecuteScript_ReturnsSuccessForValidRequest()
    {
        var scraper = Substitute.For<GenericScriptScraper>(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        scraper.ExecuteAsync(
                Arg.Any<FlowDefinitionV2>(),
                Arg.Any<Dictionary<string, object?>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(new FlowExecutionResult(new[] { new { name = "Test" } }, false));

        await using var factory = new V2BusinessSearchTestFactory(scraper);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v2/execute-script", new
        {
            definition = "us-co-business-search",
            parameters = new Dictionary<string, object?> { ["searchTerm"] = "Acme" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
