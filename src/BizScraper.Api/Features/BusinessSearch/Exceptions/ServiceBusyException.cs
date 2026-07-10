namespace BizScraper.Api.Features.BusinessSearch.Exceptions;

/// <summary>
/// Thrown when the browser pool is exhausted. Includes a Retry-After hint.
/// </summary>
public sealed class ServiceBusyException(string message, int retryAfterSeconds) : Exception(message)
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
