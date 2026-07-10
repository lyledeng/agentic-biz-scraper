namespace BizScraper.Api.Features.EntityDetails.Exceptions;

/// <summary>
/// Thrown when the provided entity details URL is malformed or not from an expected domain.
/// </summary>
public sealed class InvalidDetailsUrlException(string message) : Exception(message);
