using System.Text.Json;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Features.ExecuteScript.Mappers;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Post-flow document processor for Wyoming entity details.
/// Downloads filing history documents via browser context API, uploads to blob storage,
/// and replaces raw URLs with proxy URLs.
/// </summary>
internal sealed partial class WyDocumentProcessor(ILogger<WyDocumentProcessor> logger) : IPostFlowDocumentProcessor
{
    public string SlugPrefix => "us-wy";

    public async Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken)
    {
        if (!output.TryGetValue("historyDocuments", out var historyDocsObj) || historyDocsObj is null)
        {
            return;
        }

        JsonElement historyDocsElement;
        if (historyDocsObj is JsonElement je)
        {
            historyDocsElement = je;
        }
        else if (historyDocsObj is string jsonStr && !string.IsNullOrEmpty(jsonStr))
        {
            historyDocsElement = JsonDocument.Parse(jsonStr).RootElement;
        }
        else
        {
            return;
        }

        if (historyDocsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var updatedDocs = new List<Dictionary<string, object?>>();

        foreach (var doc in historyDocsElement.EnumerateArray())
        {
            var eventTitle = doc.GetStringOrDefault("eventTitle");
            var date = doc.GetStringOrDefault("date");
            var storageUrl = doc.GetStringOrDefault("storageUrl");
            var fileName = doc.GetStringOrDefault("fileName");

            if (string.IsNullOrEmpty(storageUrl) || string.IsNullOrEmpty(fileName) ||
                storageUrl.Contains("/api/v1/documents/", StringComparison.OrdinalIgnoreCase))
            {
                updatedDocs.Add(CloneDocEntry(doc));
                continue;
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(context.DocumentTimeoutSeconds));

                LogDownloadingHistoryDocument(fileName, context.BlobPrefix);

                var response = await context.BrowserContext.APIRequest.GetAsync(storageUrl);
                var contentType = response.Headers.GetValueOrDefault("content-type", string.Empty);

                if (!contentType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase)
                    && !contentType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    LogUnexpectedContentType(fileName, contentType);
                    updatedDocs.Add(CreateDocEntry(eventTitle, date, null, fileName, $"Unexpected content type: {contentType}"));
                    continue;
                }

                var body = await response.BodyAsync();
                if (body.Length == 0)
                {
                    LogEmptyDocument(fileName);
                    updatedDocs.Add(CreateDocEntry(eventTitle, date, null, fileName, "Downloaded document is empty"));
                    continue;
                }

                var localPath = Path.Combine(context.DiagnosticsDirectory, fileName);
                await File.WriteAllBytesAsync(localPath, body, timeoutCts.Token);

                var blobPath = $"{context.BlobPrefix}/{fileName}";
                await context.BlobStorage.UploadAsync(blobPath, localPath, timeoutCts.Token);

                var proxyUrl = DocumentProcessorHelper.BuildProxyUrl(context.HttpContextAccessor, context.Configuration, blobPath);

                LogDocumentUploaded(fileName, proxyUrl ?? "(fallback)");
                updatedDocs.Add(CreateDocEntry(eventTitle, date, proxyUrl ?? storageUrl, fileName, null));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogDownloadTimeout(fileName, context.DocumentTimeoutSeconds);
                updatedDocs.Add(CreateDocEntry(eventTitle, date, null, fileName, $"Download timed out after {context.DocumentTimeoutSeconds}s"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDownloadFailed(fileName, ex);
                updatedDocs.Add(CreateDocEntry(eventTitle, date, null, fileName, ex.Message));
            }
        }

        output["historyDocuments"] = JsonSerializer.SerializeToElement(updatedDocs);
    }

    private static Dictionary<string, object?> CreateDocEntry(string? eventTitle, string? date, string? storageUrl, string? fileName, string? error) =>
        new()
        {
            ["eventTitle"] = eventTitle,
            ["date"] = date,
            ["storageUrl"] = storageUrl,
            ["fileName"] = fileName,
            ["error"] = error
        };

    private static Dictionary<string, object?> CloneDocEntry(JsonElement doc) =>
        new()
        {
            ["eventTitle"] = doc.GetStringOrDefault("eventTitle"),
            ["date"] = doc.GetStringOrDefault("date"),
            ["storageUrl"] = doc.GetStringOrDefault("storageUrl") is "" ? null : doc.GetStringOrDefault("storageUrl"),
            ["fileName"] = doc.GetStringOrDefault("fileName"),
            ["error"] = doc.GetStringOrDefault("error") is "" ? null : doc.GetStringOrDefault("error")
        };

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading history document '{FileName}' for blob prefix '{BlobPrefix}'.")]
    private partial void LogDownloadingHistoryDocument(string fileName, string blobPrefix);

    [LoggerMessage(Level = LogLevel.Warning, Message = "History document '{FileName}' returned content type '{ContentType}' — skipping upload.")]
    private partial void LogUnexpectedContentType(string fileName, string contentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "History document '{FileName}' is empty (zero bytes) — skipping upload.")]
    private partial void LogEmptyDocument(string fileName);

    [LoggerMessage(Level = LogLevel.Information, Message = "History document '{FileName}' uploaded successfully. ProxyUrl: '{ProxyUrl}'.")]
    private partial void LogDocumentUploaded(string fileName, string proxyUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "History document '{FileName}' download timed out after {TimeoutSeconds}s.")]
    private partial void LogDownloadTimeout(string fileName, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to download/upload history document '{FileName}'.")]
    private partial void LogDownloadFailed(string fileName, Exception exception);
}
