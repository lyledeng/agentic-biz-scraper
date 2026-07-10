using Microsoft.Extensions.Logging;

namespace BizScraper.Api.Features.BusinessSearch.Logging;

public static partial class BusinessSearchLogMessages
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Starting business search for '{searchTerm}'.")]
    public static partial void SearchStarted(this ILogger logger, string searchTerm);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Completed business search for '{searchTerm}' with {resultCount} results across {pagesScraped} page(s).")]
    public static partial void SearchCompleted(this ILogger logger, string searchTerm, int resultCount, int pagesScraped);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Rejected concurrent business search for '{searchTerm}'.")]
    public static partial void SearchRejectedAsBusy(this ILogger logger, string searchTerm);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Business search for '{searchTerm}' failed.")]
    public static partial void SearchFailed(this ILogger logger, string searchTerm, Exception exception);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Saved tracing artifact at '{artifactPath}'.")]
    public static partial void TraceSaved(this ILogger logger, string artifactPath);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Discarded trace for correlation ID '{correlationId}'.")]
    public static partial void TraceDiscarded(this ILogger logger, string correlationId);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Saved screenshot artifact at '{artifactPath}'.")]
    public static partial void ScreenshotSaved(this ILogger logger, string artifactPath);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "Failed to persist diagnostic {artifactType} artifact at '{artifactPath}'.")]
    public static partial void DiagnosticArtifactFailed(this ILogger logger, string artifactType, string artifactPath, Exception exception);

    [LoggerMessage(EventId = 1008, Level = LogLevel.Information, Message = "Uploaded diagnostic {artifactType} artifact to '{blobPath}'.")]
    public static partial void DiagnosticArtifactUploaded(this ILogger logger, string artifactType, string blobPath);

    [LoggerMessage(EventId = 1009, Level = LogLevel.Warning, Message = "Failed to upload diagnostic {artifactType} artifact from '{artifactPath}' to '{blobPath}'.")]
    public static partial void DiagnosticArtifactUploadFailed(this ILogger logger, string artifactType, string artifactPath, string blobPath, Exception exception);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Information, Message = "Ensured diagnostics blob container '{containerName}' exists.")]
    public static partial void DiagnosticsContainerReady(this ILogger logger, string containerName);

    [LoggerMessage(EventId = 1100, Level = LogLevel.Information, Message = "Starting Wyoming business search for '{searchTerm}'.")]
    public static partial void WySearchStarted(this ILogger logger, string searchTerm);

    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "CAPTCHA challenge detected on Wyoming SOS site.")]
    public static partial void CaptchaDetected(this ILogger logger);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Information, Message = "CAPTCHA solved successfully ({captchaLength} characters).")]
    public static partial void CaptchaSolved(this ILogger logger, int captchaLength);

    [LoggerMessage(EventId = 1103, Level = LogLevel.Error, Message = "CAPTCHA resolution failed on Wyoming SOS site.")]
    public static partial void CaptchaFailed(this ILogger logger);

    [LoggerMessage(EventId = 1104, Level = LogLevel.Information, Message = "Scraped Wyoming page {pageNumber} with {resultCount} results.")]
    public static partial void WyPageScraped(this ILogger logger, int pageNumber, int resultCount);

    [LoggerMessage(EventId = 1105, Level = LogLevel.Information, Message = "Completed Wyoming search for '{searchTerm}' with {totalResults} results across {pagesScraped} page(s).")]
    public static partial void WySearchCompleted(this ILogger logger, string searchTerm, int totalResults, int pagesScraped);
}
