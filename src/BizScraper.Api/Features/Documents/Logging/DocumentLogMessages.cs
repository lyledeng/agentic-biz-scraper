namespace BizScraper.Api.Features.Documents.Logging;

public static partial class DocumentLogMessages
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Document stream started for '{blobPath}'.")]
    public static partial void DocumentStreamStarted(this ILogger logger, string blobPath);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Document stream completed for '{blobPath}' — {bytesStreamed} bytes in {durationMs} ms.")]
    public static partial void DocumentStreamCompleted(this ILogger logger, string blobPath, long bytesStreamed, long durationMs);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Warning, Message = "Document not found: '{blobPath}'.")]
    public static partial void DocumentNotFound(this ILogger logger, string blobPath);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Document stream failed for '{blobPath}'.")]
    public static partial void DocumentStreamFailed(this ILogger logger, string blobPath, Exception exception);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning, Message = "Document stream timed out for '{blobPath}'.")]
    public static partial void DocumentStreamTimedOut(this ILogger logger, string blobPath);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Document not modified (304) for '{blobPath}'.")]
    public static partial void DocumentNotModified(this ILogger logger, string blobPath);
}
