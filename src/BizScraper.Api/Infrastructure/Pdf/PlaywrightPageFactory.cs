using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Pdf;

/// <summary>
/// Creates a real Playwright Chromium page for PDF conversion.
/// </summary>
internal sealed class PlaywrightPageFactory : IPlaywrightPageFactory
{
    public async Task<PlaywrightPageLease> CreatePageAsync()
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        return new PlaywrightPageLease(page, new PlaywrightScope(page, browser, playwright));
    }

    private sealed class PlaywrightScope(IPage page, IBrowser browser, IPlaywright playwright) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await page.CloseAsync();
            await browser.CloseAsync();
            await browser.DisposeAsync();
            playwright.Dispose();
        }
    }
}
