using System.Collections.Concurrent;
using System.Text.Json;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Common.Models;
using BizScraper.Api.Features.BusinessSearch.Exceptions;
using BizScraper.Api.Features.ExecuteScript.Commands;
using BizScraper.Api.Features.ExecuteScript.Exceptions;
using BizScraper.Api.Features.ExecuteScript.Handlers;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.ExecuteScript.Mappers.Details;
using BizScraper.Api.Features.ExecuteScript.Mappers.Search;
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

public sealed class ExecuteScriptHandlerTests
{
    private static readonly string[] TruncatedData = ["a", "b"];
    private readonly FlowDefinitionLoader _flowLoader = new();
    private readonly GenericScriptScraper _scraper;
    private readonly IBlobStorageClient _blobStorageClient = Substitute.For<IBlobStorageClient>();

    public ExecuteScriptHandlerTests()
    {
        // Build real ScrapingFlowEngine (sealed) with empty action handlers
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
    public async Task HandleAsync_UnknownDefinition_ThrowsUnknownDefinitionException()
    {
        var handler = CreateHandler();
        var command = new ExecuteScriptCommand("nonexistent-slug", null, "corr-1");

        await Assert.ThrowsAsync<UnknownDefinitionException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_SemaphoreBusy_ThrowsServiceBusyException()
    {
        LoadTestDefinition();
        var semaphores = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);
        var semaphore = new SemaphoreSlim(1, 1);
        await semaphore.WaitAsync();
        semaphores["us-co-business-search"] = semaphore;

        var handler = CreateHandler(semaphores: semaphores);
        var command = new ExecuteScriptCommand("us-co-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Acme" }, "corr-2");

        await Assert.ThrowsAsync<ServiceBusyException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_SuccessfulExecution_ReturnsResponseEnvelope()
    {
        LoadTestDefinition();
        var flowResult = new FlowExecutionResult(new { name = "Acme" }, false);
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(flowResult);
        _blobStorageClient.GetFolderUrl(Arg.Any<string>()).Returns(new Uri("https://blob.test/diag/corr-3"));

        var handler = CreateHandler();
        var command = new ExecuteScriptCommand("us-co-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Acme" }, "corr-3");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("us-co-business-search", response.Definition);
        Assert.Equal("corr-3", response.CorrelationId);
        Assert.False(response.Truncated);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task HandleAsync_MissingParams_ThrowsMissingParametersException()
    {
        LoadTestDefinition();

        var handler = CreateHandler();
        var command = new ExecuteScriptCommand("us-co-business-search", [], "corr-4");

        await Assert.ThrowsAsync<MissingParametersException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_TruncatedResult_SetsFlag()
    {
        LoadTestDefinition();
        var flowResult = new FlowExecutionResult(TruncatedData, true);
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(flowResult);

        var handler = CreateHandler();
        var command = new ExecuteScriptCommand("us-co-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Acme" }, "corr-5");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(response.Truncated);
    }

    [Fact]
    public async Task HandleAsync_Timeout_ThrowsTimeoutException()
    {
        LoadTestDefinition();
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                var ct = callInfo.ArgAt<CancellationToken>(3);
                await Task.Delay(Timeout.Infinite, ct);
                return new FlowExecutionResult(null, false);
            });

        var handler = CreateHandler(timeoutSeconds: 1);
        var command = new ExecuteScriptCommand("us-co-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Acme" }, "corr-timeout");

        await Assert.ThrowsAsync<TimeoutException>(
            () => handler.HandleAsync(command, CancellationToken.None));
    }

    [Theory]
    [InlineData("us-co-business-search")]
    [InlineData("us-ia-business-search")]
    [InlineData("de-de-business-search")]
    public async Task HandleAsync_BusinessSearch_SetsResultCount(string slug)
    {
        LoadTestDefinition();
        var searchResults = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "Acme Corp", identifier = "123", status = "Active", entityType = "LLC", formationDate = "01/01/2020", state = "CO", @event = (string?)null, detailsUrl = "https://example.com", companyName = "Acme Corp", registeredOffice = "Berlin", registrationId = "HRB 123" },
            new { name = "Beta Inc", identifier = "456", status = "Active", entityType = "Corp", formationDate = "02/02/2021", state = "CO", @event = (string?)null, detailsUrl = "https://example.com", companyName = "Beta Inc", registeredOffice = "Munich", registrationId = "HRB 456" }
        });
        var flowResult = new FlowExecutionResult(searchResults, false);
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(flowResult);
        _blobStorageClient.GetFolderUrl(Arg.Any<string>()).Returns(new Uri("https://blob.test/diag/corr-rc"));

        var handler = CreateHandler();
        var command = new ExecuteScriptCommand(slug, new Dictionary<string, object?> { ["searchTerm"] = "Acme" }, "corr-rc");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(2, response.ResultCount);
        Assert.IsType<UnifiedSearchResult[]>(response.Data);
    }

    [Fact]
    public async Task HandleAsync_WyBusinessSearch_SetsResultCount()
    {
        LoadTestDefinition();
        // WY mapper expects raw text blocks from the extract JavaScript
        var searchResults = JsonSerializer.SerializeToElement(new[]
        {
            new { text = "Acme Corp - 123 (LLC) Status: Active Filed On: 01/01/2020", detailsUrl = "https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=123" },
            new { text = "Beta Inc - 456 (Corp) Status: Active Filed On: 02/02/2021", detailsUrl = "https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=456" }
        });
        var flowResult = new FlowExecutionResult(searchResults, false);
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(flowResult);
        _blobStorageClient.GetFolderUrl(Arg.Any<string>()).Returns(new Uri("https://blob.test/diag/corr-rc-wy"));

        var handler = CreateHandler();
        var command = new ExecuteScriptCommand("us-wy-business-search", new Dictionary<string, object?> { ["searchTerm"] = "Acme" }, "corr-rc-wy");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(2, response.ResultCount);
        Assert.IsType<UnifiedSearchResult[]>(response.Data);
    }

    [Theory]
    [InlineData("us-co-business-search")]
    [InlineData("us-wy-business-search")]
    [InlineData("us-ia-business-search")]
    [InlineData("de-de-business-search")]
    public async Task HandleAsync_BusinessSearch_ZeroResults_SetsResultCountZero(string slug)
    {
        LoadTestDefinition();
        var emptyResults = JsonSerializer.SerializeToElement(Array.Empty<object>());
        var flowResult = new FlowExecutionResult(emptyResults, false);
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(flowResult);
        _blobStorageClient.GetFolderUrl(Arg.Any<string>()).Returns(new Uri("https://blob.test/diag/corr-zero"));

        var handler = CreateHandler();
        var command = new ExecuteScriptCommand(slug, new Dictionary<string, object?> { ["searchTerm"] = "ZZZZNONEXISTENT" }, "corr-zero");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(0, response.ResultCount);
    }

    [Fact]
    public async Task HandleAsync_EntityDetails_ResultCountIsNull()
    {
        LoadTestDefinition();
        var detailData = JsonSerializer.SerializeToElement(new { name = "Acme Corp", status = "Active" });
        var flowResult = new FlowExecutionResult(detailData, false);
        _scraper.ExecuteAsync(Arg.Any<FlowDefinitionV2>(), Arg.Any<Dictionary<string, object?>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(flowResult);
        _blobStorageClient.GetFolderUrl(Arg.Any<string>()).Returns(new Uri("https://blob.test/diag/corr-detail"));

        var uniqueKey = BizScraper.Api.Common.UniqueKeyEncoder.Encode("US-CO", new Dictionary<string, string> { ["masterFileId"] = "123" });
        var handler = CreateHandler();
        var command = new ExecuteScriptCommand("us-co-entity-details", new Dictionary<string, object?> { ["uniqueKey"] = uniqueKey, ["detailsUrl"] = "https://example.com/details" }, "corr-detail");

        var response = await handler.HandleAsync(command, CancellationToken.None);

        Assert.Null(response.ResultCount);
    }

    private void LoadTestDefinition()
    {
        // Load test definitions from the actual definitions folder
        var definitionsPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Scraping", "Definitions");
        if (Directory.Exists(definitionsPath))
        {
            _flowLoader.LoadAndValidateAll(definitionsPath, NullLogger.Instance);
            return;
        }

        // Fallback: manually add a test definition via reflection (the slug index is built at load time)
        // Since FlowDefinitionLoader is internal and we can't easily add definitions manually,
        // we need the actual definition files. If they're not available in the test output, skip.
        throw new InvalidOperationException("Definition files not found in test output directory. Ensure CopyToOutputDirectory is set.");
    }

    private ExecuteScriptHandler CreateHandler(ConcurrentDictionary<string, SemaphoreSlim>? semaphores = null, int timeoutSeconds = 120)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ExecuteScript:ConcurrencyLimits:Default"] = "1",
                ["Playwright:RequestTimeoutSeconds"] = timeoutSeconds.ToString()
            })
            .Build();
        var auditRepository = new ScriptExecutionAuditRepository(configuration, NullLogger<ScriptExecutionAuditRepository>.Instance);

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
            semaphores ?? new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase),
            Substitute.For<IHttpContextAccessor>(),
            configuration,
            NullLogger<ExecuteScriptHandler>.Instance,
            Substitute.For<IWindowsProxyService>(),
            Options.Create(new WindowsProxyOptions()));
    }
}
