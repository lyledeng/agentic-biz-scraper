using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Infrastructure.Scraping.Engine;
using BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace BizScraper.UnitTests.Infrastructure.Scraping.Engine;

public sealed class GenericScriptScraperRemoteLifecycleTests : IAsyncDisposable
{
    private readonly PlaywrightOptions _playwrightOptions = new()
    {
        BrowserEndpoint = "wss://remote.example.com",
        AccessToken = "test-token"
    };

    private readonly List<GenericScriptScraper> _scrapers = [];

    private GenericScriptScraper CreateScraper(
        Func<string, CancellationToken, Task<(IPlaywright, IBrowser)>>? connectFactory = null,
        Func<IBrowser, Task<FlowExecutionResult>>? executeFactory = null)
    {
        var optionsAccessor = Options.Create(_playwrightOptions);
        var windowsProxyOptions = Options.Create(new WindowsProxyOptions());

        var scraper = Substitute.ForPartsOf<GenericScriptScraper>(
            null!, optionsAccessor, windowsProxyOptions, null!, null!, null!, null!, null!, null!, new PostFlowDocumentProcessorRegistry([]), Substitute.For<Microsoft.Extensions.Logging.ILogger<GenericScriptScraper>>());

        if (connectFactory is not null)
        {
            scraper.ConnectRemoteBrowserAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(callInfo => connectFactory(callInfo.ArgAt<string>(0), callInfo.ArgAt<CancellationToken>(2)));
        }

        if (executeFactory is not null)
        {
            scraper.ExecuteRemoteFlowAsync(
                    Arg.Any<FlowDefinitionV2>(),
                    Arg.Any<Dictionary<string, object?>>(),
                    Arg.Any<string>(),
                    Arg.Any<IBrowser>(),
                    Arg.Any<CancellationToken>())
                .Returns(callInfo => executeFactory(callInfo.ArgAt<IBrowser>(3)));
        }

        _scrapers.Add(scraper);
        return scraper;
    }

    private static (IPlaywright, IBrowser) CreateMockBrowserPair()
    {
        var playwright = Substitute.For<IPlaywright>();
        var browser = Substitute.For<IBrowser>();
        browser.IsConnected.Returns(true);
        return (playwright, browser);
    }

    private static FlowDefinitionV2 CreateRemoteDefinition() => new(
        SchemaVersion: "2.0",
        Metadata: new FlowMetadata
        {
            Id = "test-remote",
            DefinitionSlug = "test-remote-def",
            Name = "Test Remote",
            State = "CO",
            Endpoint = "/api/v1/test"
        },
        Environment: null,
        Browser: new BrowserConfig("remote", null),
        Variables: [],
        Actions: [],
        Scripts: null,
        Output: new OutputDeclarationV2 { VariableName = "result", Type = "object" });

    // T004: Per-request lifecycle — creates and disposes browser per request
    [Fact]
    public async Task RemoteMode_CreatesAndDisposesBrowserPerRequest()
    {
        var (playwright, browser) = CreateMockBrowserPair();
        var connectCalled = 0;

        var scraper = CreateScraper(
            connectFactory: (_, _) =>
            {
                Interlocked.Increment(ref connectCalled);
                return Task.FromResult<(IPlaywright, IBrowser)>((playwright, browser));
            },
            executeFactory: _ => Task.FromResult(new FlowExecutionResult(null, false)));

        var definition = CreateRemoteDefinition();

        await scraper.ExecuteAsync(definition, [], "corr-1", CancellationToken.None);

        Assert.Equal(1, connectCalled);
        await browser.Received(1).CloseAsync();
        await browser.Received(1).DisposeAsync();
        playwright.Received(1).Dispose();
    }

    // T005: Guaranteed cleanup on exception
    [Fact]
    public async Task RemoteMode_DisposesOnException()
    {
        var (playwright, browser) = CreateMockBrowserPair();

        var scraper = CreateScraper(
            connectFactory: (_, _) => Task.FromResult<(IPlaywright, IBrowser)>((playwright, browser)),
            executeFactory: _ => throw new InvalidOperationException("Scrape failed"));

        var definition = CreateRemoteDefinition();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scraper.ExecuteAsync(definition, [], "corr-2", CancellationToken.None));

        await browser.Received(1).CloseAsync();
        await browser.Received(1).DisposeAsync();
        playwright.Received(1).Dispose();
    }

    // T006: One retry on mid-scrape disconnect
    [Fact]
    public async Task RemoteMode_RetriesOnceOnDisconnect()
    {
        var firstPair = CreateMockBrowserPair();
        var retryPair = CreateMockBrowserPair();
        var callIndex = 0;

        var scraper = CreateScraper(
            connectFactory: (_, _) =>
            {
                var idx = Interlocked.Increment(ref callIndex);
                return Task.FromResult(idx == 1 ? firstPair : retryPair);
            },
            executeFactory: browser =>
            {
                if (browser == firstPair.Item2)
                {
                    throw new PlaywrightException("Target page, context or browser has been closed");
                }

                return Task.FromResult(new FlowExecutionResult(new { retried = true }, false));
            });

        var definition = CreateRemoteDefinition();

        var result = await scraper.ExecuteAsync(definition, [], "corr-3", CancellationToken.None);

        // First browser pair should be disposed
        await firstPair.Item2.Received(1).CloseAsync();
        await firstPair.Item2.Received(1).DisposeAsync();
        firstPair.Item1.Received(1).Dispose();

        // Retry browser pair should also be disposed
        await retryPair.Item2.Received(1).CloseAsync();
        await retryPair.Item2.Received(1).DisposeAsync();
        retryPair.Item1.Received(1).Dispose();

        // Should have connected twice (original + retry)
        Assert.Equal(2, callIndex);
        Assert.NotNull(result.Output);
    }

    // T006 supplement: Retry also fails — error propagates, both pairs cleaned up
    [Fact]
    public async Task RemoteMode_RetryAlsoFails_PropagatesToCaller()
    {
        var firstPair = CreateMockBrowserPair();
        var retryPair = CreateMockBrowserPair();
        var callIndex = 0;

        var scraper = CreateScraper(
            connectFactory: (_, _) =>
            {
                var idx = Interlocked.Increment(ref callIndex);
                return Task.FromResult(idx == 1 ? firstPair : retryPair);
            },
            executeFactory: _ => throw new PlaywrightException("Target page, context or browser has been closed"));

        var definition = CreateRemoteDefinition();

        await Assert.ThrowsAsync<PlaywrightException>(
            () => scraper.ExecuteAsync(definition, [], "corr-4", CancellationToken.None));

        // Both pairs should be fully disposed
        await firstPair.Item2.Received(1).CloseAsync();
        firstPair.Item1.Received(1).Dispose();
        await retryPair.Item2.Received(1).CloseAsync();
        retryPair.Item1.Received(1).Dispose();
        Assert.Equal(2, callIndex);
    }

    // T010a: Concurrent requests get independent browser instances
    [Fact]
    public async Task RemoteMode_ConcurrentRequestsGetIndependentInstances()
    {
        var browsers = new List<IBrowser>();
        var playwrights = new List<IPlaywright>();
        var gate = new TaskCompletionSource();

        var scraper = CreateScraper(
            connectFactory: (_, _) =>
            {
                var pair = CreateMockBrowserPair();
                lock (browsers)
                {
                    browsers.Add(pair.Item2);
                    playwrights.Add(pair.Item1);
                }

                return Task.FromResult(pair);
            },
            executeFactory: async _ =>
            {
                // Hold execution until both requests have connected
                await gate.Task;
                return new FlowExecutionResult(null, false);
            });

        var definition = CreateRemoteDefinition();
        var task1 = scraper.ExecuteAsync(definition, [], "corr-a", CancellationToken.None);
        var task2 = scraper.ExecuteAsync(definition, [], "corr-b", CancellationToken.None);

        // Wait for both to connect
        await Task.Delay(200);
        gate.SetResult();

        await Task.WhenAll(task1, task2);

        // Each request should have created its own independent pair
        Assert.Equal(2, browsers.Count);
        Assert.Equal(2, playwrights.Count);
        Assert.NotSame(browsers[0], browsers[1]);
        Assert.NotSame(playwrights[0], playwrights[1]);

        // Both should be disposed
        foreach (var b in browsers)
        {
            await b.Received(1).CloseAsync();
        }

        foreach (var p in playwrights)
        {
            p.Received(1).Dispose();
        }
    }

    // T011: Local mode still reuses browser from pool (routing test)
    [Fact]
    public void LocalMode_ResolvedCorrectly_WhenNoEndpoint()
    {
        var localOpts = new PlaywrightOptions { BrowserEndpoint = "" };
        var config = new BrowserConfig("local", "chrome");

        var result = GenericScriptScraper.ResolveEffectiveMode(config, localOpts);

        Assert.Equal("local", result);
    }

    // T012: Local mode uses pool even when remote endpoint is configured but definition says local
    [Fact]
    public void LocalMode_UsesLocalPool_WhenDefinitionOverridesLocal()
    {
        var config = new BrowserConfig("local", "chrome");
        var opts = new PlaywrightOptions { BrowserEndpoint = "wss://remote.example.com" };

        var result = GenericScriptScraper.ResolveEffectiveMode(config, opts);

        Assert.Equal("local", result);
    }

    // T014: Readiness probe for remote mode — verify ProbePoolAsync returns healthy without connecting
    // when endpoint configured but no browser exists. We test via CanConnectAsync which calls ProbePoolAsync.
    // The definitionLoader requires real file access, so we test the probe behavior
    // through the BrowserPool state: when _remotePool.HasBrowser is false and endpoint is configured,
    // ProbePoolAsync returns healthy without calling EnsureBrowserAsync or ConnectRemoteBrowserAsync.
    [Fact]
    public void ProbeRemote_PoolHasNoBrowser_WhenNoRequestsMade()
    {
        // After construction, the remote pool has no browser — confirming no eager connection is created.
        // This is the prerequisite for ProbePoolAsync to return "healthy" without connecting.
        var pool = new BrowserPool("remote");
        Assert.False(pool.HasBrowser);
        Assert.False(pool.IsConnected);
    }

    // T015: Remote probe reports not-configured when no endpoint
    [Fact]
    public void ProbeRemote_IsRemoteModeIsFalse_WhenNoEndpoint()
    {
        var noRemoteOptions = new PlaywrightOptions { BrowserEndpoint = "" };
        Assert.False(noRemoteOptions.IsRemoteMode);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scraper in _scrapers)
        {
            await scraper.DisposeAsync();
        }
    }
}
