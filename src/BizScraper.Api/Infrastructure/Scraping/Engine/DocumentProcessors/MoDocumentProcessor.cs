using System.Text.Json;
using System.Text.RegularExpressions;
using BizScraper.Api.Features.ExecuteScript.Mappers;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Post-flow document processor for Missouri entity details.
/// Downloads filing documents using page-level fetch (preserves session cookies on remote browsers),
/// uploads to blob storage, and replaces raw URLs with proxy URLs.
/// </summary>
internal sealed partial class MoDocumentProcessor(ILogger<MoDocumentProcessor> logger) : IPostFlowDocumentProcessor
{
    public string SlugPrefix => "us-mo";

    public async Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken)
    {
        if (!output.TryGetValue("filings", out var filingsObj) || filingsObj is null)
        {
            return;
        }

        JsonElement filingsElement;
        if (filingsObj is JsonElement je)
        {
            filingsElement = je;
        }
        else if (filingsObj is string jsonStr && !string.IsNullOrEmpty(jsonStr))
        {
            filingsElement = JsonDocument.Parse(jsonStr).RootElement;
        }
        else
        {
            return;
        }

        if (filingsElement.ValueKind != JsonValueKind.Array || filingsElement.GetArrayLength() == 0)
        {
            return;
        }

        var updatedFilings = new List<Dictionary<string, object?>>();

        foreach (var filing in filingsElement.EnumerateArray())
        {
            var action = filing.GetStringOrDefault("action");
            var documentType = filing.GetStringOrDefault("documentType");
            var rowIndex = filing.TryGetProperty("rowIndex", out var ri) && ri.ValueKind == JsonValueKind.Number
                ? ri.GetInt32()
                : -1;
            var storageUrl = filing.GetStringOrDefault("storageUrl");

            if (string.IsNullOrEmpty(storageUrl) ||
                storageUrl.Contains("/api/v1/documents/", StringComparison.OrdinalIgnoreCase))
            {
                updatedFilings.Add(CloneFilingEntry(filing));
                continue;
            }

            var slug = $"{action}-{documentType}"
                .Replace(" ", "-", StringComparison.Ordinal)
                .Replace("/", "-", StringComparison.Ordinal);
            slug = SanitizeRegex().Replace(slug, "");
            if (slug.Length > 80)
            {
                slug = slug[..80];
            }
            var fileName = $"{slug}-{rowIndex}.pdf";

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(context.DocumentTimeoutSeconds));

                LogDownloadingFiling(rowIndex, documentType, storageUrl);

                var fetchResult = await context.Page.EvaluateAsync<JsonElement>(
                    @"async (url) => {
                        try {
                            const resp = await fetch(url);
                            const ct = resp.headers.get('content-type') || '';
                            if (!resp.ok) return { error: resp.status + ' ' + resp.statusText, contentType: ct };
                            const buf = await resp.arrayBuffer();
                            const bytes = new Uint8Array(buf);
                            let binary = '';
                            for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
                            return { contentType: ct, base64: btoa(binary), size: bytes.length };
                        } catch(e) { return { error: e.message, contentType: '' }; }
                    }", storageUrl);

                if (fetchResult.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                {
                    var errorMsg = errorProp.GetString()!;
                    LogFetchFailed(rowIndex, errorMsg);
                    updatedFilings.Add(CloneFilingEntry(filing, error: errorMsg));
                    continue;
                }

                var contentType = fetchResult.GetProperty("contentType").GetString() ?? "";

                if (!contentType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase)
                    && !contentType.Contains("image/tiff", StringComparison.OrdinalIgnoreCase)
                    && !contentType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                {
                    LogUnexpectedContentType(rowIndex, contentType);
                    updatedFilings.Add(CloneFilingEntry(filing, error: $"Unexpected content type: {contentType}"));
                    continue;
                }

                var base64 = fetchResult.GetProperty("base64").GetString();
                var body = Convert.FromBase64String(base64 ?? "");

                if (body.Length == 0)
                {
                    LogEmptyDocument(rowIndex, documentType);
                    updatedFilings.Add(CloneFilingEntry(filing, fileName: fileName, error: "Downloaded document is empty"));
                    continue;
                }

                if (contentType.Contains("image/tiff", StringComparison.OrdinalIgnoreCase) &&
                    !fileName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase))
                {
                    fileName = Path.ChangeExtension(fileName, ".tiff");
                }

                var localPath = Path.Combine(context.DiagnosticsDirectory, fileName);
                await File.WriteAllBytesAsync(localPath, body, timeoutCts.Token);

                var blobPath = $"{context.BlobPrefix}/{fileName}";
                await context.BlobStorage.UploadAsync(blobPath, localPath, timeoutCts.Token);

                var proxyUrl = DocumentProcessorHelper.BuildProxyUrl(context.HttpContextAccessor, context.Configuration, blobPath);

                LogDocumentUploaded(rowIndex, documentType, body.Length, proxyUrl ?? "(fallback)");
                updatedFilings.Add(CloneFilingEntry(filing, storageUrl: proxyUrl, fileName: fileName));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogDownloadTimeout(rowIndex, documentType, context.DocumentTimeoutSeconds);
                updatedFilings.Add(CloneFilingEntry(filing, error: $"Download timed out after {context.DocumentTimeoutSeconds}s"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDownloadFailed(rowIndex, documentType, ex);
                updatedFilings.Add(CloneFilingEntry(filing, error: ex.Message));
            }
        }

        output["filings"] = JsonSerializer.SerializeToElement(updatedFilings);
    }

    private static Dictionary<string, object?> CloneFilingEntry(
        JsonElement filing,
        string? storageUrl = null,
        string? fileName = null,
        string? error = null) =>
        new()
        {
            ["action"] = filing.GetStringOrDefault("action"),
            ["documentType"] = filing.GetStringOrDefault("documentType"),
            ["dateFiled"] = filing.GetStringOrDefault("dateFiled"),
            ["effectiveDate"] = filing.GetStringOrDefault("effectiveDate"),
            ["rowIndex"] = filing.TryGetProperty("rowIndex", out var ri) ? ri.GetInt32() : 0,
            ["hasViewDocument"] = filing.TryGetProperty("hasViewDocument", out var hvd) && hvd.ValueKind == JsonValueKind.True,
            ["storageUrl"] = storageUrl ?? (filing.GetStringOrDefault("storageUrl") is "" ? null : filing.GetStringOrDefault("storageUrl")),
            ["fileName"] = fileName ?? filing.GetStringOrDefault("fileName"),
            ["error"] = error ?? (filing.GetStringOrDefault("error") is "" ? null : filing.GetStringOrDefault("error"))
        };

    [GeneratedRegex(@"[^a-zA-Z0-9\-]")]
    private static partial Regex SanitizeRegex();

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading MO filing document row {RowIndex} '{DocumentType}' from '{Url}'.")]
    private partial void LogDownloadingFiling(int rowIndex, string documentType, string url);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MO filing document row {RowIndex} fetch failed: {Error}.")]
    private partial void LogFetchFailed(int rowIndex, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MO filing document row {RowIndex} returned content type '{ContentType}' — skipping.")]
    private partial void LogUnexpectedContentType(int rowIndex, string contentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MO filing document row {RowIndex} '{DocumentType}' is empty — skipping.")]
    private partial void LogEmptyDocument(int rowIndex, string documentType);

    [LoggerMessage(Level = LogLevel.Information, Message = "MO filing document row {RowIndex} '{DocumentType}' uploaded ({Size} bytes). ProxyUrl: '{ProxyUrl}'.")]
    private partial void LogDocumentUploaded(int rowIndex, string documentType, int size, string proxyUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "MO filing document row {RowIndex} '{DocumentType}' download timed out after {TimeoutSeconds}s.")]
    private partial void LogDownloadTimeout(int rowIndex, string documentType, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to download MO filing document row {RowIndex} '{DocumentType}'.")]
    private partial void LogDownloadFailed(int rowIndex, string documentType, Exception exception);
}
