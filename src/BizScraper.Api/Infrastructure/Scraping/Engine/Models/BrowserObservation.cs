namespace BizScraper.Api.Infrastructure.Scraping.Engine.Models;

public enum BrowserActionType
{
    Click,
    Fill
}

public abstract record BrowserScreenshot;

public sealed record BrowserScreenshotPath(string Path) : BrowserScreenshot;

public sealed record BrowserScreenshotBytes(ReadOnlyMemory<byte> Bytes) : BrowserScreenshot;

public sealed record BrowserObservation(
    string CurrentUrl,
    string PageTitle,
    BrowserScreenshot? Screenshot,
    IReadOnlyList<string> AttemptedSelectors,
    BrowserActionType ActionType,
    string? TargetDescription,
    DateTimeOffset Timestamp);
