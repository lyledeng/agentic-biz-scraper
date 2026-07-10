namespace BizScraper.Api.Features.EntityDetails.Logging;

public static partial class EntityDetailsLogMessages
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Information, Message = "Starting entity detail retrieval for '{detailsUrl}'.")]
    public static partial void EntityDetailStarted(this ILogger logger, string detailsUrl);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Completed entity detail retrieval for '{detailsUrl}' — entity '{entityName}' ({entityStatus}).")]
    public static partial void EntityDetailCompleted(this ILogger logger, string detailsUrl, string entityName, string entityStatus);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Entity detail retrieval for '{detailsUrl}' failed.")]
    public static partial void EntityDetailFailed(this ILogger logger, string detailsUrl, Exception exception);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Warning, Message = "Rejected concurrent entity detail request for '{detailsUrl}'.")]
    public static partial void EntityDetailRejectedAsBusy(this ILogger logger, string detailsUrl);

    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "Entity detail URL validation failed for '{detailsUrl}'.")]
    public static partial void EntityDetailValidationError(this ILogger logger, string detailsUrl);

    [LoggerMessage(EventId = 2005, Level = LogLevel.Information, Message = "Certificate downloaded for '{detailsUrl}' — saved to '{localPath}'.")]
    public static partial void CertificateDownloaded(this ILogger logger, string detailsUrl, string localPath);

    [LoggerMessage(EventId = 2006, Level = LogLevel.Warning, Message = "Certificate download or upload failed for '{detailsUrl}'.")]
    public static partial void CertificateUploadFailed(this ILogger logger, string detailsUrl, Exception exception);

    [LoggerMessage(EventId = 2007, Level = LogLevel.Warning, Message = "Audit trail persistence failed for entity detail request '{correlationId}'.")]
    public static partial void AuditWriteFailed(this ILogger logger, string correlationId, Exception exception);

    [LoggerMessage(EventId = 2008, Level = LogLevel.Information, Message = "Downloading history document '{fileName}' for '{correlationId}'.")]
    public static partial void HistoryDocumentDownloadStarted(this ILogger logger, string fileName, string correlationId);

    [LoggerMessage(EventId = 2009, Level = LogLevel.Information, Message = "History document '{fileName}' uploaded to cloud storage for '{correlationId}'.")]
    public static partial void HistoryDocumentUploaded(this ILogger logger, string fileName, string correlationId);

    [LoggerMessage(EventId = 2010, Level = LogLevel.Warning, Message = "History document '{fileName}' download or upload failed for '{correlationId}'.")]
    public static partial void HistoryDocumentFailed(this ILogger logger, string fileName, string correlationId, Exception exception);

    [LoggerMessage(EventId = 2011, Level = LogLevel.Warning, Message = "History document '{fileName}' has unexpected content type '{contentType}' for '{correlationId}'.")]
    public static partial void HistoryDocumentInvalidContentType(this ILogger logger, string fileName, string contentType, string correlationId);

    [LoggerMessage(EventId = 2012, Level = LogLevel.Information, Message = "Certificate flow started for '{correlationId}' with filing ID '{filingId}'.")]
    public static partial void CertificateFlowStarted(this ILogger logger, string correlationId, string filingId);

    [LoggerMessage(EventId = 2013, Level = LogLevel.Information, Message = "Certificate flow completed for '{correlationId}' with filing ID '{filingId}'.")]
    public static partial void CertificateFlowCompleted(this ILogger logger, string correlationId, string filingId);

    [LoggerMessage(EventId = 2014, Level = LogLevel.Warning, Message = "Certificate flow failed for '{correlationId}' with filing ID '{filingId}': {errorMessage}")]
    public static partial void CertificateFlowFailed(this ILogger logger, string correlationId, string filingId, string errorMessage, Exception exception);

    [LoggerMessage(EventId = 2015, Level = LogLevel.Warning, Message = "Skipping certificate flow for '{correlationId}' because filing ID '{filingId}' is invalid.")]
    public static partial void CertificateFlowSkippedInvalidFilingId(this ILogger logger, string correlationId, string filingId);

    [LoggerMessage(EventId = 2016, Level = LogLevel.Warning, Message = "Certificate flow timed out for '{correlationId}' with filing ID '{filingId}'.")]
    public static partial void CertificateFlowTimedOut(this ILogger logger, string correlationId, string filingId, Exception exception);
}
