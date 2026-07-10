namespace BizScraper.Api.Infrastructure.Scraping.Proxy;

/// <summary>
/// Exception thrown when the Windows VM proxy returns a non-success response or is unreachable.
/// </summary>
public sealed class WindowsProxyException(
    int statusCode,
    string title,
    string? detail,
    int? retryAfterSeconds = null) : Exception(detail ?? title)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
}
