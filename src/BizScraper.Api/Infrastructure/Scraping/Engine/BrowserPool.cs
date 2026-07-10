using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

/// <summary>
/// Encapsulates browser lifecycle for a single mode (local or remote).
/// Each pool independently manages its Playwright instance, browser, lock, and usage counter.
/// Pools are created eagerly but browsers are launched lazily on first use.
/// </summary>
internal sealed class BrowserPool : IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private int _usageCount;

    public BrowserPool(string mode)
    {
        Mode = mode;
    }

    /// <summary>"local" or "remote".</summary>
    public string Mode { get; }

    /// <summary>Number of requests served by the current browser instance.</summary>
    public int UsageCount => _usageCount;

    /// <summary>True when a browser instance is currently held.</summary>
    public bool HasBrowser => Browser is not null;

    /// <summary>True when the browser is connected.</summary>
    public bool IsConnected => Browser?.IsConnected ?? false;

    /// <summary>The current browser instance (may be null if not yet created).</summary>
    public IBrowser? Browser { get; private set; }

    /// <summary>The Playwright instance for this pool.</summary>
    public IPlaywright? Playwright { get; private set; }

    public void IncrementUsage() => Interlocked.Increment(ref _usageCount);

    public bool NeedsRecycle(int threshold) => _usageCount >= threshold;

    public void ResetUsage() => Interlocked.Exchange(ref _usageCount, 0);

    public async Task AcquireLockAsync(CancellationToken cancellationToken) =>
        await _browserLock.WaitAsync(cancellationToken);

    public async Task<bool> TryAcquireLockAsync(TimeSpan timeout) =>
        await _browserLock.WaitAsync(timeout);

    public void ReleaseLock() => _browserLock.Release();

    /// <summary>
    /// Sets the browser and playwright instances after creation. Resets usage to 1.
    /// </summary>
    public void SetBrowser(IPlaywright playwright, IBrowser browser)
    {
        Playwright = playwright;
        Browser = browser;
        _usageCount = 1;
    }

    /// <summary>
    /// Clears the current browser, closing and disposing it. Does NOT release playwright.
    /// </summary>
    public async Task ClearBrowserAsync()
    {
        if (Browser is not null)
        {
            try { await Browser.CloseAsync(); } catch { /* already disconnected */ }
            await Browser.DisposeAsync();
            Browser = null;
            _usageCount = 0;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            try { await Browser.CloseAsync(); } catch { /* best effort */ }
            await Browser.DisposeAsync();
        }

        Playwright?.Dispose();
        _browserLock.Dispose();
    }

    public void Dispose()
    {
        Browser?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Playwright?.Dispose();
        _browserLock.Dispose();
    }
}
