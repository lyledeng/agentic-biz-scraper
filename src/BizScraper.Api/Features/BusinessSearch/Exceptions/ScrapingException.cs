namespace BizScraper.Api.Features.BusinessSearch.Exceptions;

/// <summary>
/// Thrown when a scraping operation fails due to unexpected page structure or extraction errors.
/// </summary>
public sealed class ScrapingException(string message, Exception? innerException = null) : Exception(message, innerException);
