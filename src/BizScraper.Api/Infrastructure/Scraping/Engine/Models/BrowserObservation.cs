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
    IReadOnlyList<string> AttemptedSelectors,
    BrowserActionType ActionType,
    string? TargetDescription,
    DateTimeOffset Timestamp,
    BrowserScreenshot? Screenshot = null,
    string? FailureType = null,
    string? FailureMessage = null,
    string? OriginalSelector = null)
{
    public string Url => CurrentUrl;
}
