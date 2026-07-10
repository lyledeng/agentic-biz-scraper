using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Pdf;

/// <summary>
/// Abstraction for creating a Playwright page, enabling unit tests to substitute a mock page
/// without requiring Playwright browser binaries.
/// </summary>
public interface IPlaywrightPageFactory
{
    /// <summary>
    /// Creates a Playwright page and returns it along with a disposable scope that cleans up
    /// the page, browser, and playwright instance when disposed.
    /// </summary>
    Task<PlaywrightPageLease> CreatePageAsync();
}

/// <summary>
/// Holds a leased Playwright page and a scope that disposes all underlying resources.
/// </summary>
public sealed class PlaywrightPageLease(IPage page, IAsyncDisposable scope) : IAsyncDisposable
{
    public IPage Page => page;

    public async ValueTask DisposeAsync() => await scope.DisposeAsync().ConfigureAwait(false);
}
