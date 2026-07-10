using System.Diagnostics;
using Azure;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.Documents.Logging;
using BizScraper.Api.Features.Documents.Metrics;
using BizScraper.Api.Features.Documents.Models;
using BizScraper.Api.Features.Documents.Validation;
using Polly.Timeout;

namespace BizScraper.Api.Features.Documents.Handlers;

/// <summary>
/// Handles document streaming requests by retrieving PDFs from Azure Blob Storage with caching.
/// </summary>
public sealed class StreamDocumentHandler(
    IBlobStorageClient blobStorageClient,
    ILogger<StreamDocumentHandler> logger)
{
    public async Task<IResult> HandleAsync(StreamDocumentQuery query, CancellationToken cancellationToken)
    {
        var blobPath = query.BlobPath;

        // Redundant safety check — endpoint validates before dispatch
        if (!BlobPathValidator.IsValid(blobPath))
        {
            return Results.Problem(
                title: "Invalid blob path",
                detail: $"The blob path '{blobPath}' does not match the allowed pattern.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        logger.DocumentStreamStarted(blobPath);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Conditional request: check ETag before downloading
            if (query.IfNoneMatch is not null)
            {
                var currentETag = await blobStorageClient.GetBlobETagAsync(blobPath, cancellationToken);
                if (currentETag is null)
                {
                    stopwatch.Stop();
                    logger.DocumentNotFound(blobPath);
                    DocumentMetrics.RecordRequest(404, stopwatch.Elapsed.TotalMilliseconds);
                    return Results.Problem(
                        title: "Document not found",
                        detail: $"No document exists at path '{blobPath}'.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                if (string.Equals(currentETag, query.IfNoneMatch, StringComparison.Ordinal))
                {
                    stopwatch.Stop();
                    logger.DocumentNotModified(blobPath);
                    DocumentMetrics.RecordRequest(304, stopwatch.Elapsed.TotalMilliseconds);
                    return Results.StatusCode(StatusCodes.Status304NotModified);
                }
            }

            var downloadResult = await blobStorageClient.DownloadBlobAsync(blobPath, cancellationToken);
            if (downloadResult is null)
            {
                stopwatch.Stop();
                logger.DocumentNotFound(blobPath);
                DocumentMetrics.RecordRequest(404, stopwatch.Elapsed.TotalMilliseconds);
                return Results.Problem(
                    title: "Document not found",
                    detail: $"No document exists at path '{blobPath}'.",
                    statusCode: StatusCodes.Status404NotFound);
            }

            stopwatch.Stop();
            logger.DocumentStreamCompleted(blobPath, downloadResult.ContentLength, stopwatch.ElapsedMilliseconds);
            DocumentMetrics.RecordRequest(200, stopwatch.Elapsed.TotalMilliseconds);

            var fileDownloadName = query.ForceDownload ? downloadResult.FileName : null;

            return Results.Stream(
                downloadResult.Content,
                downloadResult.ContentType,
                fileDownloadName,
                enableRangeProcessing: false);
        }
        catch (RequestFailedException ex)
        {
            stopwatch.Stop();
            logger.DocumentStreamFailed(blobPath, ex);
            DocumentMetrics.RecordRequest(502, stopwatch.Elapsed.TotalMilliseconds);
            return Results.Problem(
                title: "Storage error",
                detail: "The upstream storage service returned an error. Please retry later.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (TimeoutRejectedException)
        {
            stopwatch.Stop();
            logger.DocumentStreamTimedOut(blobPath);
            DocumentMetrics.RecordRequest(504, stopwatch.Elapsed.TotalMilliseconds);
            return Results.Problem(
                title: "Gateway timeout",
                detail: "The document download timed out after 30 seconds.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }
}
