using System.Text.Json;
using BizScraper.Api.Features.ExecuteScript.Mappers;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Post-flow document processor for Colorado entity details.
/// Downloads the certified documents PDF using browser context API,
/// uploads to blob storage, and replaces the viewer URL with a proxy URL.
/// </summary>
internal sealed partial class CoDocumentProcessor(ILogger<CoDocumentProcessor> logger) : IPostFlowDocumentProcessor
{
    public string SlugPrefix => "us-co";

    public async Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken)
    {
        if (!output.TryGetValue("certifiedDocumentsViewerUrl", out var viewerUrlObj) ||
            viewerUrlObj is not string viewerUrl ||
            string.IsNullOrEmpty(viewerUrl))
        {
            return;
        }

        var idNumber = "unknown";
        if (output.TryGetValue("details", out var detailsObj) && detailsObj is JsonElement detailsEl &&
            detailsEl.ValueKind == JsonValueKind.Object)
        {
            idNumber = detailsEl.GetStringOrDefault("idNumber") ?? "unknown";
        }

        var fileName = $"{idNumber}-alldocuments.pdf";

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(context.DocumentTimeoutSeconds));

            LogDownloading(idNumber, viewerUrl);

            var response = await context.BrowserContext.APIRequest.GetAsync(viewerUrl);
            var contentType = response.Headers.GetValueOrDefault("content-type", string.Empty);

            if (!contentType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase) &&
                !contentType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                LogUnexpectedContentType(idNumber, contentType);
                return;
            }

            var body = await response.BodyAsync();
            if (body.Length == 0)
            {
                LogEmptyDocument(idNumber);
                return;
            }

            var localPath = Path.Combine(context.DiagnosticsDirectory, fileName);
            await File.WriteAllBytesAsync(localPath, body, timeoutCts.Token);

            var blobPath = $"{context.BlobPrefix}/{fileName}";
            await context.BlobStorage.UploadAsync(blobPath, localPath, timeoutCts.Token);

            var proxyUrl = DocumentProcessorHelper.BuildProxyUrl(context.HttpContextAccessor, context.Configuration, blobPath);
            output["certifiedDocumentsUrl"] = proxyUrl ?? viewerUrl;

            LogUploaded(idNumber, proxyUrl ?? "(fallback)");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimeout(idNumber, context.DocumentTimeoutSeconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogDownloadFailed(idNumber, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading CO certified documents for entity '{IdNumber}' from '{ViewerUrl}'.")]
    private partial void LogDownloading(string idNumber, string viewerUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CO certified documents for '{IdNumber}' returned content type '{ContentType}' — skipping upload.")]
    private partial void LogUnexpectedContentType(string idNumber, string contentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CO certified documents for '{IdNumber}' is empty (zero bytes) — skipping upload.")]
    private partial void LogEmptyDocument(string idNumber);

    [LoggerMessage(Level = LogLevel.Information, Message = "CO certified documents for '{IdNumber}' uploaded successfully. ProxyUrl: '{ProxyUrl}'.")]
    private partial void LogUploaded(string idNumber, string proxyUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CO certified documents for '{IdNumber}' download timed out after {TimeoutSeconds}s.")]
    private partial void LogTimeout(string idNumber, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to download/upload CO certified documents for '{IdNumber}'.")]
    private partial void LogDownloadFailed(string idNumber, Exception exception);
}
