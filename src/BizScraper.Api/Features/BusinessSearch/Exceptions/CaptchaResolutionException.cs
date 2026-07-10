namespace BizScraper.Api.Features.BusinessSearch.Exceptions;

/// <summary>
/// Thrown when a CAPTCHA challenge cannot be solved by the AI agent service.
/// </summary>
public sealed class CaptchaResolutionException(string message, Exception? innerException = null) : Exception(message, innerException);
