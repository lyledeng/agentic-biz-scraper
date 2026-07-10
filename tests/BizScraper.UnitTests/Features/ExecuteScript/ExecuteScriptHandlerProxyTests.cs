using System.Collections.Concurrent;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.ExecuteScript.Commands;
using BizScraper.Api.Features.ExecuteScript.Handlers;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Mappers.Details;
using BizScraper.Api.Features.ExecuteScript.Mappers.Search;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Features.ExecuteScript.Validation;
using BizScraper.Api.Infrastructure.Persistence;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.Actions;
using BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using BizScraper.Api.Infrastructure.Scraping.Proxy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BizScraper.UnitTests.Features.ExecuteScript;

public sealed class ExecuteScriptHandlerProxyTests
{
    private readonly FlowDefinitionLoader _flowLoader = new();
    private readonly GenericScriptScraper _scraper;
    private readonly IBlobStorageClient _blobStorageClient = Substitute.For<IBlobStorageClient>();
    private readonly IWindowsProxyService _proxyService = Substitute.For<IWindowsProxyService>();

    public ExecuteScriptHandlerProxyTests()
    {
        var engine = new ScrapingFlowEngine(
            Array.Empty<IActionHandler>(),
            new ConditionEvaluator(new TargetResolver(NullLogger<TargetResolver>.Instance)),
            new OutputSchemaValidator(NullLogger<OutputSchemaValidator>.Instance),
            NullLogger<ScrapingFlowEngine>.Instance);

        _scraper = Substitute.ForPartsOf<GenericScriptScraper>(
            engine,
            Options.Create(new PlaywrightOptions()),
            Options.Create(new WindowsProxyOptions()),
            Substitute.For<IWindowsProxyService>(),
            Substitute.For<IWebHostEnvironment>(),
            Substitute.For<IBlobStorageClient>(),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IConfiguration>(),
            _flowLoader,
            new PostFlowDocumentProcessorRegistry([]),
            NullLogger<GenericScriptScraper>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WindowsMode_ProxyConfigured_ForwardsToProxyService()
    {
        LoadDefinitions();
        var expectedResponse = new ExecuteScriptResponse
        {
            Definition = "us-ia-business-search",
            CorrelationId = "corr-proxy-1",
            Truncated = false,
            Data = new { name = "Test Corp" }
        };
        _proxyService.ForwardAsync(
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object?>?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var handler = CreateHandler(windowsProxyEndpoint: "https://vm.example.com/api");
        var command = new ExecuteScriptCommand("us-ia-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Test" }, "corr-proxy-1");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("us-ia-business-search", response.Definition);
        Assert.Equal("corr-proxy-1", response.CorrelationId);
        // Verify scraper was NOT called
        await _scraper.DidNotReceive().ExecuteAsync(
            Arg.Any<FlowDefinitionV2>(),
            Arg.Any<Dictionary<string, object?>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WindowsMode_ProxyNotConfigured_FallsBackToScraper()
    {
        LoadDefinitions();
        var flowResult = new FlowExecutionResult(new { name = "Fallback Corp" }, false);
        _scraper.ExecuteAsync(
            Arg.Any<FlowDefinitionV2>(),
            Arg.Any<Dictionary<string, object?>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns(flowResult);
        _blobStorageClient.GetFolderUrl(Arg.Any<string>()).Returns(new Uri("https://blob.test/diag/corr-fallback"));

        var handler = CreateHandler(windowsProxyEndpoint: ""); // not configured
        var command = new ExecuteScriptCommand("us-ia-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Test" }, "corr-fallback");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        // Verify proxy was NOT called
        await _proxyService.DidNotReceive().ForwardAsync(
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object?>?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        // Scraper WAS called (fallback to local)
        await _scraper.Received(1).ExecuteAsync(
            Arg.Any<FlowDefinitionV2>(),
            Arg.Any<Dictionary<string, object?>>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WindowsMode_ProxyConfigured_SkipsPostProcessing()
    {
        LoadDefinitions();
        // Return raw data from proxy — no mapper should be applied
        var rawData = new { businessName = "Raw Corp", id = "999" };
        var expectedResponse = new ExecuteScriptResponse
        {
            Definition = "us-ia-business-search",
            CorrelationId = "corr-no-postprocess",
            Truncated = false,
            Data = rawData
        };
        _proxyService.ForwardAsync(
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, object?>?>(),
            Arg.Any<string>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>())
            .Returns(expectedResponse);

        var handler = CreateHandler(windowsProxyEndpoint: "https://vm.example.com/api");
        var command = new ExecuteScriptCommand("us-ia-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Test" }, "corr-no-postprocess");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        // Response is returned as-is from proxy (no mapper applied)
        Assert.Equal(rawData, response.Data);
    }

    private void LoadDefinitions()
    {
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        _flowLoader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
    }

    private ExecuteScriptHandler CreateHandler(string windowsProxyEndpoint = "")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExecuteScript:ConcurrencyLimits:Default"] = "1",
                ["Playwright:RequestTimeoutSeconds"] = "120",
                ["WindowsProxy:EndpointUrl"] = windowsProxyEndpoint,
                ["WindowsProxy:TimeoutSeconds"] = "180"
            })
            .Build();
        var auditRepository = new ScriptExecutionAuditRepository(configuration, NullLogger<ScriptExecutionAuditRepository>.Instance);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Authorization"] = "Bearer test-token";
        httpContextAccessor.HttpContext.Returns(httpContext);

        var registry = new MapperRegistry(
            [new CoSearchResultMapper(), new WySearchResultMapper(), new IaSearchResultMapper(), new MoSearchResultMapper(), new WaSearchResultMapper(), new DeSearchResultMapper()],
            [new CoEntityDetailMapper(), new WyEntityDetailMapper(), new IaEntityDetailMapper(), new MoEntityDetailMapper(), new WaEntityDetailMapper(), new DeEntityDetailMapper()]);

        return new ExecuteScriptHandler(
            _flowLoader,
            _scraper,
            new ExecuteScriptValidator(),
            auditRepository,
            _blobStorageClient,
            new SearchResultMapper(registry),
            new EntityDetailMapper(registry),
            new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase),
            httpContextAccessor,
            configuration,
            NullLogger<ExecuteScriptHandler>.Instance,
            _proxyService,
            Options.Create(new WindowsProxyOptions { EndpointUrl = windowsProxyEndpoint, TimeoutSeconds = 180 }));
    }
}
