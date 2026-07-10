using System.Text.Json;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.ExecuteScript.Mappers;
using BizScraper.Api.Features.HealthCheck;
using BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using BizScraper.Api.Infrastructure.Scraping.Proxy;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal partial class GenericScriptScraper(
    ScrapingFlowEngine engine,
    IOptions<PlaywrightOptions> options,
    IOptions<WindowsProxyOptions> windowsProxyOptions,
    IWindowsProxyService windowsProxyService,
    IWebHostEnvironment environment,
    IBlobStorageClient blobStorageClient,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    FlowDefinitionLoader definitionLoader,
    PostFlowDocumentProcessorRegistry processorRegistry,
    ILogger<GenericScriptScraper> logger) : IAsyncDisposable
{
    private readonly BrowserPool _localPool = new("local");
    private readonly BrowserPool _remotePool = new("remote");
    private readonly SemaphoreSlim _executionSemaphoreLocal = new(3, 3);
    private readonly SemaphoreSlim _executionSemaphoreRemote = new(3, 3);

    /// <summary>
    /// Resolves the effective browser mode for a definition using the three-step resolution algorithm:
    /// 1. If definition has a browser override, use it (with remote→local fallback when no endpoint, windows→local when proxy not configured).
    /// 2. Otherwise, use global default (remote when BrowserEndpoint is configured, local otherwise).
    /// </summary>
    internal static string ResolveEffectiveMode(BrowserConfig? browserConfig, PlaywrightOptions playwrightOptions, WindowsProxyOptions? windowsProxyOptions = null)
    {
        if (browserConfig is not null)
        {
            if (browserConfig.Mode.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                return "local";
            }

            if (browserConfig.Mode.Equals("remote", StringComparison.OrdinalIgnoreCase))
            {
                return playwrightOptions.IsRemoteMode ? "remote" : "local";
            }

            if (browserConfig.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase))
            {
                return windowsProxyOptions is { IsConfigured: true } ? "windows" : "local";
            }
        }

        return playwrightOptions.IsRemoteMode ? "remote" : "local";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Browser mode resolved: {EffectiveMode} (source: {Source}) for definition '{DefinitionSlug}'.")]
    private partial void LogBrowserModeResolved(string effectiveMode, string source, string definitionSlug);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote mode requested but no BrowserEndpoint configured for definition '{DefinitionSlug}'; falling back to local.")]
    private partial void LogRemoteFallbackToLocal(string definitionSlug);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Windows mode requested but no WindowsProxy endpoint configured for definition '{DefinitionSlug}'; falling back to local.")]
    private partial void LogWindowsFallbackToLocal(string definitionSlug);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Browser connectivity check failed for {Mode} pool.")]
    private partial void LogConnectivityCheckFailed(string mode, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recycling {Mode} browser after {UsageCount} uses (connected={IsConnected}).")]
    private partial void LogRecyclingBrowser(string mode, int usageCount, bool isConnected);

    [LoggerMessage(Level = LogLevel.Information, Message = "Force-recycling disconnected {Mode} browser (usageCount={UsageCount}).")]
    private partial void LogForceRecycling(string mode, int usageCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Browser channel '{Channel}' not found for definition '{DefinitionSlug}'. Ensure the channel is installed in the container.")]
    private partial void LogChannelNotFound(string channel, string definitionSlug, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Remote browser connecting (correlationId={CorrelationId}).")]
    private partial void LogRemoteBrowserConnecting(string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Remote browser connected (correlationId={CorrelationId}, elapsed={ElapsedMs}ms).")]
    private partial void LogRemoteBrowserConnected(string correlationId, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Information, Message = "Remote browser disposed (correlationId={CorrelationId}).")]
    private partial void LogRemoteBrowserDisposed(string correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Remote browser disconnected mid-scrape, retrying (correlationId={CorrelationId}).")]
    private partial void LogRemoteBrowserRetrying(string correlationId, Exception exception);

    /// <summary>
    /// Health-check probe: checks both pools and returns per-mode health status.
    /// </summary>
    public virtual async Task<ReadinessHealthResponse> CanConnectAsync(CancellationToken cancellationToken)
    {
        var localStatus = await ProbePoolAsync(_localPool, cancellationToken);
        var remoteStatus = await ProbePoolAsync(_remotePool, cancellationToken);

        var localConfig = definitionLoader.Definitions.Values
            .Where(d => d.Browser is not null && d.Browser.Mode.Equals("local", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
            .ToArray();

        var remoteConfig = definitionLoader.Definitions.Values
            .Where(d => d.Browser is not null && d.Browser.Mode.Equals("remote", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
            .ToArray();

        var windowsConfig = definitionLoader.Definitions.Values
            .Where(d => d.Browser is not null && d.Browser.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.Metadata.DefinitionSlug ?? d.Metadata.Id)
            .ToArray();

        var browserModes = new Dictionary<string, ModeStatus>();

        if (localConfig.Length > 0 || !options.Value.IsRemoteMode)
        {
            browserModes["local"] = localStatus;
        }

        if (options.Value.IsRemoteMode)
        {
            browserModes["remote"] = remoteStatus;
        }

        var anyHealthy = browserModes.Values.Any(m => m.Status == "healthy");
        var allHealthy = browserModes.Values.All(m => m.Status == "healthy");

        var overallStatus = allHealthy ? "Ready" : anyHealthy ? "Degraded" : "NotReady";

        // Windows proxy health (does NOT affect overall readiness — it's an auxiliary proxy)
        WindowsProxyStatus? windowsProxyStatus = null;
        if (windowsConfig.Length > 0)
        {
            windowsProxyStatus = await windowsProxyService.CheckWindowsProxyHealthAsync(cancellationToken);
        }

        return new ReadinessHealthResponse(overallStatus, browserModes, localConfig, remoteConfig, windowsProxyStatus, windowsConfig);
    }

    private async Task<ModeStatus> ProbePoolAsync(BrowserPool pool, CancellationToken cancellationToken)
    {
        try
        {
            if (pool.Mode == "remote" && !options.Value.IsRemoteMode)
            {
                return new ModeStatus("not-configured", null, null, null);
            }

            if (pool.Mode == "local")
            {
                await EnsureBrowserAsync(pool, null, cancellationToken);
                var healthy = pool.IsConnected;
                return new ModeStatus(
                    healthy ? "healthy" : "unhealthy",
                    options.Value.BrowserChannel is { Length: > 0 } ch ? ch : null,
                    null, healthy ? null : "Browser not connected");
            }
            else
            {
                // Don't call EnsureBrowserAsync for remote — it creates billable Azure Playwright sessions.
                // Just report current connection state; the browser will connect on first real request.
                if (!pool.HasBrowser)
                {
                    // Not yet connected — treat as healthy (will connect on-demand)
                    return new ModeStatus("healthy", null, options.Value.BrowserEndpoint, null);
                }

                var healthy = pool.IsConnected;
                return new ModeStatus(
                    healthy ? "healthy" : "unhealthy",
                    null,
                    options.Value.BrowserEndpoint,
                    healthy ? null : "Remote browser disconnected");
            }
        }
        catch (Exception ex) when (ex is PlaywrightException or InvalidOperationException)
        {
            LogConnectivityCheckFailed(pool.Mode, ex);
            return new ModeStatus("unhealthy", null, pool.Mode == "remote" ? options.Value.BrowserEndpoint : null, ex.Message);
        }
    }

    /// <summary>
    /// Executes a scraping flow definition. Routes to per-request remote lifecycle (connect → execute → dispose)
    /// for remote mode, or pooled local browser for local/windows modes.
    /// </summary>
    public virtual async Task<FlowExecutionResult> ExecuteAsync(
        FlowDefinitionV2 definition,
        Dictionary<string, object?> parameters,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var slug = definition.Metadata.DefinitionSlug ?? definition.Metadata.Id;

        // Validate browser config if present
        if (definition.Browser is not null &&
            !definition.Browser.Mode.Equals("local", StringComparison.OrdinalIgnoreCase) &&
            !definition.Browser.Mode.Equals("remote", StringComparison.OrdinalIgnoreCase) &&
            !definition.Browser.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Invalid browser mode '{definition.Browser.Mode}' in definition '{slug}'. Must be 'local', 'remote', or 'windows'.");
        }

        var effectiveMode = ResolveEffectiveMode(definition.Browser, options.Value, windowsProxyOptions.Value);

        // Log fallback
        if (definition.Browser is not null &&
            definition.Browser.Mode.Equals("remote", StringComparison.OrdinalIgnoreCase) &&
            effectiveMode == "local")
        {
            LogRemoteFallbackToLocal(slug);
        }

        if (definition.Browser is not null &&
            definition.Browser.Mode.Equals("windows", StringComparison.OrdinalIgnoreCase) &&
            effectiveMode == "local")
        {
            LogWindowsFallbackToLocal(slug);
        }

        var source = definition.Browser is not null ? "definition override" : "global default";
        LogBrowserModeResolved(effectiveMode, source, slug);

        if (effectiveMode == "remote")
        {
            // Per-request lifecycle: connect → execute → dispose (no pooling)
            await _executionSemaphoreRemote.WaitAsync(cancellationToken);
            try
            {
                return await ExecuteRemoteCoreAsync(definition, parameters, correlationId, cancellationToken);
            }
            finally
            {
                _executionSemaphoreRemote.Release();
            }
        }

        var pool = _localPool;
        var semaphore = _executionSemaphoreLocal;

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            return await ExecuteCoreAsync(definition, parameters, correlationId, pool, cancellationToken);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<FlowExecutionResult> ExecuteCoreAsync(
        FlowDefinitionV2 definition,
        Dictionary<string, object?> parameters,
        string correlationId,
        BrowserPool pool,
        CancellationToken cancellationToken)
    {
        await EnsureBrowserAsync(pool, definition.Browser, cancellationToken);

        // When using a real browser channel (e.g. "chrome"), skip user-agent override
        // so the browser uses its native UA string. Cloudflare detects mismatches between
        // the Chrome version in the UA and the actual browser engine version.
        var hasChannel = !string.IsNullOrWhiteSpace(definition.Browser?.Channel)
            || !string.IsNullOrWhiteSpace(options.Value.BrowserChannel);
        var userAgent = hasChannel ? null
            : definition.Environment?.UserAgent
              ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        IBrowserContext context;
        try
        {
            context = await pool.Browser!.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                UserAgent = userAgent,
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "en-US",
                TimezoneId = "America/Chicago"
            });
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(ex, "Browser disconnected between readiness check and context creation. Recycling and retrying once.");
            await ForceRecycleBrowserAsync(pool, cancellationToken);
            await EnsureBrowserAsync(pool, definition.Browser, cancellationToken);
            context = await pool.Browser!.NewContextAsync(new BrowserNewContextOptions
            {
                IgnoreHTTPSErrors = true,
                UserAgent = userAgent,
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                Locale = "en-US",
                TimezoneId = "America/Chicago"
            });
        }

        await using var _ = context;

        var page = await context.NewPageAsync();

        // Remove automation signals that trigger CDN bot detection (Akamai, Cloudflare, etc.)
        await page.AddInitScriptAsync("""
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            // Cloudflare checks window.chrome — ensure it exists (channel=chrome provides it natively,
            // but bundled Chromium may not). Also hide cdc_ (ChromeDriver) prefixed properties.
            if (!window.chrome) { window.chrome = { runtime: {}, csi: function(){}, loadTimes: function(){} }; }
            // Remove Playwright-injected properties
            delete window.__playwright;
            delete window.__pw_manual;
            """);

        // Build enriched blob prefix: {date}/{slug}/{searchLabel}/{correlationId}
        var blobPrefix = BuildBlobPrefix(definition.Metadata.DefinitionSlug ?? "unknown", parameters, correlationId);

        var actionContext = new ActionContext(
            page, context, correlationId, blobPrefix,
            (p, prefix, milestone, force, ct) => CaptureScreenshotAsync(p, prefix, milestone, force, ct));

        // Populate runtime variables from request parameters
        foreach (var (key, value) in parameters)
        {
            actionContext.Variables[key] = value;
        }

        // Populate system-generated variables
        actionContext.Variables["correlationId"] = correlationId;
        actionContext.Variables["__blobPrefix"] = blobPrefix;

        // Supply diagnostics directory for download handler
        var diagDir = Path.Combine(
            environment.ContentRootPath,
            options.Value.Diagnostics.OutputPath,
            blobPrefix);
        Directory.CreateDirectory(diagDir);
        actionContext.Variables["__diagnosticsDir"] = diagDir;

        // Populate config-source variables from PlaywrightOptions
        actionContext.Variables["maxPages"] = options.Value.MaxPages;

        var result = await engine.ExecuteAsync(definition, actionContext, cancellationToken);

        // Post-flow: delegate to registry-based document processor
        var slug = definition.Metadata.DefinitionSlug ?? definition.Metadata.Id;
        var processor = processorRegistry.GetProcessor(slug);
        if (processor is not null && result.Output is Dictionary<string, object?> composedOutput)
        {
            try
            {
                var processorContext = new PostFlowDocumentContext(
                    page, context, blobStorageClient, httpContextAccessor, configuration,
                    blobPrefix, diagDir, options.Value.DocumentDownloadTimeoutSeconds, definition);
                await processor.ProcessAsync(composedOutput, processorContext, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Post-flow document processing failed for {Slug}", slug);
            }
        }

        return result;
    }

    /// <summary>
    /// Creates ephemeral IPlaywright + IBrowser for a single remote request.
    /// Uses a 30-second ConnectAsync timeout and x-mpt-access-key header.
    /// </summary>
    internal virtual async Task<(IPlaywright Playwright, IBrowser Browser)> ConnectRemoteBrowserAsync(string correlationId, string? definitionSlug, CancellationToken cancellationToken)
    {
        LogRemoteBrowserConnecting(correlationId);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var playwright = await Playwright.CreateAsync();
        try
        {
            var endpoint = AppendRunId(options.Value.BrowserEndpoint, correlationId, definitionSlug);
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(options.Value.AccessToken))
            {
                headers["x-mpt-access-key"] = options.Value.AccessToken;
            }

            var browser = await playwright.Chromium.ConnectAsync(endpoint, new BrowserTypeConnectOptions
            {
                Headers = headers,
                Timeout = 30_000f
            });

            sw.Stop();
            LogRemoteBrowserConnected(correlationId, sw.ElapsedMilliseconds);
            return (playwright, browser);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Per-request remote browser lifecycle: connect → execute flow → dispose.
    /// Retries once on mid-scrape disconnect (PlaywrightException with "browser has been closed").
    /// </summary>
    private async Task<FlowExecutionResult> ExecuteRemoteCoreAsync(
        FlowDefinitionV2 definition,
        Dictionary<string, object?> parameters,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var slug = definition.Metadata.DefinitionSlug;
        var (playwright, browser) = await ConnectRemoteBrowserAsync(correlationId, slug, cancellationToken);
        var disposed = false;
        try
        {
            return await ExecuteRemoteFlowAsync(definition, parameters, correlationId, browser, cancellationToken);
        }
        catch (PlaywrightException ex) when (ex.Message.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase))
        {
            // Mid-scrape disconnect — dispose failed instances and retry once
            LogRemoteBrowserRetrying(correlationId, ex);
            await DisposeRemoteBrowserAsync(playwright, browser, correlationId);
            disposed = true;

            var (retryPlaywright, retryBrowser) = await ConnectRemoteBrowserAsync(correlationId, slug, cancellationToken);
            try
            {
                var result = await ExecuteRemoteFlowAsync(definition, parameters, correlationId, retryBrowser, cancellationToken);
                return result;
            }
            finally
            {
                await DisposeRemoteBrowserAsync(retryPlaywright, retryBrowser, correlationId);
            }
        }
        finally
        {
            if (!disposed)
            {
                await DisposeRemoteBrowserAsync(playwright, browser, correlationId);
            }
        }
    }

    internal virtual async Task<FlowExecutionResult> ExecuteRemoteFlowAsync(
        FlowDefinitionV2 definition,
        Dictionary<string, object?> parameters,
        string correlationId,
        IBrowser browser,
        CancellationToken cancellationToken)
    {
        var hasChannel = !string.IsNullOrWhiteSpace(definition.Browser?.Channel)
            || !string.IsNullOrWhiteSpace(options.Value.BrowserChannel);
        var userAgent = hasChannel ? null
            : definition.Environment?.UserAgent
              ?? "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            UserAgent = userAgent,
            ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
            Locale = "en-US",
            TimezoneId = "America/Chicago"
        });

        var page = await context.NewPageAsync();

        await page.AddInitScriptAsync("""
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            if (!window.chrome) { window.chrome = { runtime: {}, csi: function(){}, loadTimes: function(){} }; }
            delete window.__playwright;
            delete window.__pw_manual;
            """);

        var blobPrefix = BuildBlobPrefix(definition.Metadata.DefinitionSlug ?? "unknown", parameters, correlationId);

        var diagDir = Path.Combine(
            environment.ContentRootPath,
            options.Value.Diagnostics.OutputPath,
            blobPrefix);
        Directory.CreateDirectory(diagDir);

        var actionContext = new ActionContext(
            page, context, correlationId, blobPrefix,
            (p, prefix, milestone, force, ct) => CaptureScreenshotAsync(p, prefix, milestone, force, ct));

        foreach (var (key, value) in parameters)
        {
            actionContext.Variables[key] = value;
        }

        actionContext.Variables["correlationId"] = correlationId;
        actionContext.Variables["__blobPrefix"] = blobPrefix;
        actionContext.Variables["__diagnosticsDir"] = diagDir;
        actionContext.Variables["maxPages"] = options.Value.MaxPages;

        var result = await engine.ExecuteAsync(definition, actionContext, cancellationToken);

        // Post-flow: delegate to registry-based document processor
        var remoteSlug = definition.Metadata.DefinitionSlug ?? definition.Metadata.Id;
        var remoteProcessor = processorRegistry.GetProcessor(remoteSlug);
        if (remoteProcessor is not null && result.Output is Dictionary<string, object?> composedOutput)
        {
            try
            {
                var processorContext = new PostFlowDocumentContext(
                    page, context, blobStorageClient, httpContextAccessor, configuration,
                    blobPrefix, diagDir, options.Value.DocumentDownloadTimeoutSeconds, definition);
                await remoteProcessor.ProcessAsync(composedOutput, processorContext, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Post-flow document processing failed for {Slug}", remoteSlug);
            }
        }

        return result;
    }

    private async Task DisposeRemoteBrowserAsync(IPlaywright playwright, IBrowser browser, string correlationId)
    {
        try { await browser.CloseAsync(); } catch { /* already disconnected */ }
        try { await browser.DisposeAsync(); } catch { /* ignore disposal errors on disconnected browser */ }
        try { playwright.Dispose(); } catch { /* ignore */ }
        LogRemoteBrowserDisposed(correlationId);
    }

    /// <summary>
    /// Appends <c>?runId={slug}_{correlationId}</c> to the Azure Playwright Testing WSS endpoint
    /// so each browser session appears in the portal with the definition slug and correlation ID as its Source ID.
    /// </summary>
    internal static string AppendRunId(string endpoint, string correlationId, string? definitionSlug = null)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return endpoint;
        }

        var runId = string.IsNullOrWhiteSpace(definitionSlug)
            ? correlationId
            : $"{definitionSlug}_{correlationId}";

        var separator = endpoint.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{endpoint}{separator}runId={Uri.EscapeDataString(runId)}";
    }

    private async Task EnsureBrowserAsync(BrowserPool pool, BrowserConfig? browserConfig, CancellationToken cancellationToken)
    {
        await pool.AcquireLockAsync(cancellationToken);
        try
        {
            var reuse = options.Value.ReuseBrowserForRequests;
            if (pool.HasBrowser && pool.IsConnected && !pool.NeedsRecycle(reuse))
            {
                pool.IncrementUsage();
                return;
            }

            if (pool.HasBrowser)
            {
                LogRecyclingBrowser(pool.Mode, pool.UsageCount, pool.IsConnected);
                await pool.ClearBrowserAsync();
            }

            var playwright = pool.Playwright ?? await Playwright.CreateAsync();

            if (pool.Mode == "remote" && options.Value.IsRemoteMode)
            {
                var endpoint = options.Value.BrowserEndpoint;
                var headers = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(options.Value.AccessToken))
                {
                    headers["x-mpt-access-key"] = options.Value.AccessToken;
                }

                var browser = await playwright.Chromium.ConnectAsync(endpoint, new BrowserTypeConnectOptions
                {
                    Headers = headers,
                    Timeout = options.Value.RequestTimeoutSeconds * 1000f
                });

                pool.SetBrowser(playwright, browser);
            }
            else
            {
                // Local mode — use definition-level channel/headless or fall back to global
                var channel = browserConfig?.Channel
                    ?? (string.IsNullOrWhiteSpace(options.Value.BrowserChannel) ? null : options.Value.BrowserChannel);
                var headless = browserConfig?.Headless ?? true;
                var slug = "unknown";

                try
                {
                    var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = headless,
                        Channel = channel,
                        Args =
                        [
                            "--disable-blink-features=AutomationControlled",
                            "--disable-features=AutomationControlled",
                            "--no-first-run",
                            "--no-default-browser-check",
                            "--disable-component-update"
                        ]
                    });

                    pool.SetBrowser(playwright, browser);
                }
                catch (PlaywrightException ex) when (channel is not null && ex.Message.Contains("channel", StringComparison.OrdinalIgnoreCase))
                {
                    LogChannelNotFound(channel, slug, ex);
                    throw new InvalidOperationException(
                        $"Browser channel '{channel}' is not available. Ensure it is installed in the container. Definition: '{slug}'.", ex);
                }
            }
        }
        finally
        {
            pool.ReleaseLock();
        }
    }

    private async Task ForceRecycleBrowserAsync(BrowserPool pool, CancellationToken cancellationToken)
    {
        await pool.AcquireLockAsync(cancellationToken);
        try
        {
            if (pool.HasBrowser)
            {
                LogForceRecycling(pool.Mode, pool.UsageCount);
                await pool.ClearBrowserAsync();
            }
        }
        finally
        {
            pool.ReleaseLock();
        }
    }

    private async Task CaptureScreenshotAsync(IPage page, string blobPrefix, string milestone, bool force, CancellationToken cancellationToken)
    {
        if (!options.Value.Diagnostics.ScreenshotsEnabled && !force)
        {
            return;
        }

        try
        {
            var outputPath = Path.Combine(
                environment.ContentRootPath,
                options.Value.Diagnostics.OutputPath,
                blobPrefix);

            Directory.CreateDirectory(outputPath);
            var filePath = Path.Combine(outputPath, $"{milestone}.png");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = filePath, FullPage = true });

            if (options.Value.Diagnostics.CloudStorage.Enabled)
            {
                var blobPath = $"{blobPrefix}/{milestone}.png";
                await blobStorageClient.UploadAsync(blobPath, filePath, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Screenshot capture failed for milestone {Milestone}.", milestone);
        }
    }

    /// <summary>
    /// Builds a blob storage folder prefix: {date}/{slug}/{label}/{correlationId}
    /// where label is derived from searchTerm or detailsUrl parameter.
    /// </summary>
    internal static string BuildBlobPrefix(string definitionSlug, Dictionary<string, object?> parameters, string correlationId)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Extract a human-readable label from parameters
        var label = "unknown";
        if (parameters.TryGetValue("searchTerm", out var searchTermObj) && searchTermObj is string searchTerm && !string.IsNullOrWhiteSpace(searchTerm))
        {
            label = searchTerm;
        }
        else if (parameters.TryGetValue("entityName", out var entityNameObj) && entityNameObj is string entityName && !string.IsNullOrWhiteSpace(entityName))
        {
            label = entityName;
        }
        else if (parameters.TryGetValue("detailsUrl", out var detailsUrlObj) && detailsUrlObj is string detailsUrl && !string.IsNullOrWhiteSpace(detailsUrl))
        {
            // For entity-details, extract a recognizable segment from the URL
            label = ExtractLabelFromUrl(detailsUrl);
        }

        label = SanitizeBlobSegment(label);
        if (label.Length > 60)
        {
            label = label[..60];
        }

        return $"{date}/{definitionSlug}/{label}/{correlationId}";
    }

    private static string ExtractLabelFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            // Use query string params or last path segment as label
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            var entityId = query["masterFileId"] ?? query["id"] ?? query["entityid"];
            if (!string.IsNullOrWhiteSpace(entityId))
            {
                return entityId;
            }

            var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
            return !string.IsNullOrWhiteSpace(lastSegment) ? lastSegment : "entity";
        }
        catch
        {
            return "entity";
        }
    }

    internal static string SanitizeBlobSegment(string input)
    {
        // Replace any character that isn't alphanumeric, hyphen, underscore, or dot with a hyphen
        var sanitized = System.Text.RegularExpressions.Regex.Replace(input.Trim(), @"[^a-zA-Z0-9\-_.]", "-");
        // Collapse multiple hyphens
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"-{2,}", "-");
        return sanitized.Trim('-');
    }

    public async ValueTask DisposeAsync()
    {
        await _localPool.DisposeAsync();
        await _remotePool.DisposeAsync();
        _executionSemaphoreLocal.Dispose();
        _executionSemaphoreRemote.Dispose();
    }
}
