namespace BizScraper.Api.Features.BusinessSearch.Exceptions;

/// <summary>
/// Thrown when the upstream Secretary of State website returns an error or is unreachable.
/// </summary>
public sealed class UpstreamException(string message, Exception? innerException = null) : Exception(message, innerException);
