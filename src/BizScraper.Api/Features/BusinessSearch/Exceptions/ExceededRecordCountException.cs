namespace BizScraper.Api.Features.BusinessSearch.Exceptions;

/// <summary>
/// Thrown when the upstream source returns more records than can be meaningfully processed.
/// </summary>
public sealed class ExceededRecordCountException(string message) : Exception(message);
