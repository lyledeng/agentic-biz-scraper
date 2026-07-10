using System.Text.Json;
using System.Text.RegularExpressions;
using BizScraper.Api.Features.ExecuteScript.Mappers;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Post-flow document processor for Washington entity details.
/// Downloads filing documents using page-level fetch to WA CCFS API endpoints,
/// preserving session cookies. Discovers docs via GetTransactionDocumentsList,
/// downloads each via DownloadOnlineFilesByNumber.
/// </summary>
internal sealed partial class WaDocumentProcessor(ILogger<WaDocumentProcessor> logger) : IPostFlowDocumentProcessor
{
    public string SlugPrefix => "us-wa";

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

        var maxFilingDocs = 10;
        if (context.Definition.Variables.FirstOrDefault(v => v.Name == "maxFilingDocuments") is { DefaultValue: JsonElement { ValueKind: JsonValueKind.Number } cfgMax })
        {
            maxFilingDocs = cfgMax.GetInt32();
        }

        var updatedFilings = new List<Dictionary<string, object?>>();
        var filingIndex = 0;

        foreach (var filing in filingsElement.EnumerateArray())
        {
            var filingNumber = filing.GetStringOrDefault("filingNumber");
            var filingType = filing.GetStringOrDefault("filingType");

            var baseFiling = new Dictionary<string, object?>
            {
                ["filingNumber"] = filingNumber,
                ["filingDateTime"] = filing.GetStringOrDefault("filingDateTime"),
                ["effectiveDate"] = filing.GetStringOrDefault("effectiveDate"),
                ["filingType"] = filingType,
                ["documents"] = new List<Dictionary<string, object?>>()
            };

            if (filingIndex >= maxFilingDocs || string.IsNullOrEmpty(filingNumber))
            {
                updatedFilings.Add(baseFiling);
                filingIndex++;
                continue;
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(context.DocumentTimeoutSeconds));

                LogFetchingDocList(filingIndex, filingNumber);

                var docListResult = await context.Page.EvaluateAsync<JsonElement>(
                    @"async (filingNumber) => {
                        try {
                            const resp = await fetch('https://ccfs-api.prod.sos.wa.gov/api/Common/GetTransactionDocumentsList', {
                                method: 'POST',
                                headers: { 'Content-Type': 'application/json' },
                                body: JSON.stringify({ TransactionNumber: filingNumber })
                            });
                            if (!resp.ok) return { error: resp.status + ' ' + resp.statusText, documents: [] };
                            const data = await resp.json();
                            return { documents: Array.isArray(data) ? data : (data.DocumentList || data.documents || []) };
                        } catch(e) { return { error: e.message, documents: [] }; }
                    }", filingNumber);

                var documents = new List<Dictionary<string, object?>>();

                if (docListResult.TryGetProperty("error", out var docListError) && docListError.ValueKind == JsonValueKind.String)
                {
                    LogDocListFetchFailed(filingIndex, filingNumber, docListError.GetString()!);
                    baseFiling["documents"] = documents;
                    updatedFilings.Add(baseFiling);
                    filingIndex++;
                    continue;
                }

                if (!docListResult.TryGetProperty("documents", out var docsArray) ||
                    docsArray.ValueKind != JsonValueKind.Array)
                {
                    baseFiling["documents"] = documents;
                    updatedFilings.Add(baseFiling);
                    filingIndex++;
                    continue;
                }

                var docIndex = 0;
                foreach (var doc in docsArray.EnumerateArray())
                {
                    var documentType = doc.GetStringOrDefault("DocumentType") ?? doc.GetStringOrDefault("documentType") ?? "unknown";
                    var createdDate = doc.GetStringOrDefault("CreatedDate") ?? doc.GetStringOrDefault("createdDate");
                    var fileNumber = doc.GetStringOrDefault("FileNumber") ?? doc.GetStringOrDefault("fileNumber") ?? filingNumber;

                    var docEntry = new Dictionary<string, object?>
                    {
                        ["documentType"] = documentType,
                        ["createdDate"] = createdDate,
                        ["storageUrl"] = (string?)null,
                        ["fileName"] = (string?)null,
                        ["error"] = (string?)null
                    };

                    try
                    {
                        var slug = $"{filingType}-{documentType}"
                            .Replace(" ", "-", StringComparison.Ordinal)
                            .Replace("/", "-", StringComparison.Ordinal);
                        slug = SanitizeRegex().Replace(slug, "");
                        if (slug.Length > 80)
                        {
                            slug = slug[..80];
                        }
                        var fileName = $"{slug}-{filingIndex}-{docIndex}.pdf";

                        LogDownloadingDoc(filingIndex, docIndex, documentType);

                        var fetchResult = await context.Page.EvaluateAsync<JsonElement>(
                            @"async (fileNumber) => {
                                try {
                                    const resp = await fetch('https://ccfs-api.prod.sos.wa.gov/api/Common/DownloadOnlineFilesByNumber?fileNumber=' + encodeURIComponent(fileNumber));
                                    const ct = resp.headers.get('content-type') || '';
                                    if (!resp.ok) return { error: resp.status + ' ' + resp.statusText, contentType: ct };
                                    const buf = await resp.arrayBuffer();
                                    const bytes = new Uint8Array(buf);
                                    let binary = '';
                                    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);
                                    return { contentType: ct, base64: btoa(binary), size: bytes.length };
                                } catch(e) { return { error: e.message, contentType: '' }; }
                            }", fileNumber);

                        if (fetchResult.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                        {
                            var errorMsg = errorProp.GetString()!;
                            LogDocFetchFailed(filingIndex, docIndex, errorMsg);
                            docEntry["error"] = errorMsg;
                            documents.Add(docEntry);
                            docIndex++;
                            continue;
                        }

                        var contentType = fetchResult.GetProperty("contentType").GetString() ?? "";
                        if (!contentType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase)
                            && !contentType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                        {
                            LogUnexpectedContentType(filingIndex, docIndex, contentType);
                            docEntry["error"] = $"Unexpected content type: {contentType}";
                            documents.Add(docEntry);
                            docIndex++;
                            continue;
                        }

                        var base64 = fetchResult.GetProperty("base64").GetString();
                        var body = Convert.FromBase64String(base64 ?? "");

                        if (body.Length == 0)
                        {
                            LogEmptyDocument(filingIndex, docIndex);
                            docEntry["fileName"] = fileName;
                            docEntry["error"] = "Downloaded document is empty";
                            documents.Add(docEntry);
                            docIndex++;
                            continue;
                        }

                        var localPath = Path.Combine(context.DiagnosticsDirectory, fileName);
                        await File.WriteAllBytesAsync(localPath, body, timeoutCts.Token);

                        var blobPath = $"{context.BlobPrefix}/{fileName}";
                        await context.BlobStorage.UploadAsync(blobPath, localPath, timeoutCts.Token);

                        var proxyUrl = DocumentProcessorHelper.BuildProxyUrl(context.HttpContextAccessor, context.Configuration, blobPath);

                        LogDocUploaded(filingIndex, docIndex, documentType, body.Length, proxyUrl ?? "(fallback)");
                        docEntry["storageUrl"] = proxyUrl;
                        docEntry["fileName"] = fileName;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        LogDocTimeout(filingIndex, docIndex, context.DocumentTimeoutSeconds);
                        docEntry["error"] = $"Download timed out after {context.DocumentTimeoutSeconds}s";
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        LogDocDownloadFailed(filingIndex, docIndex, ex);
                        docEntry["error"] = ex.Message;
                    }

                    documents.Add(docEntry);
                    docIndex++;
                }

                baseFiling["documents"] = documents;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogFilingTimeout(filingIndex, filingNumber);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogFilingFailed(filingIndex, filingNumber, ex);
            }

            updatedFilings.Add(baseFiling);
            filingIndex++;
        }

        output["filings"] = JsonSerializer.SerializeToElement(updatedFilings);
    }

    [GeneratedRegex(@"[^a-zA-Z0-9\-]")]
    private static partial Regex SanitizeRegex();

    [LoggerMessage(Level = LogLevel.Information, Message = "WA filing {FilingIndex} '{FilingNumber}' — fetching document list.")]
    private partial void LogFetchingDocList(int filingIndex, string filingNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} '{FilingNumber}' document list fetch failed: {Error}.")]
    private partial void LogDocListFetchFailed(int filingIndex, string filingNumber, string error);

    [LoggerMessage(Level = LogLevel.Information, Message = "WA filing {FilingIndex} doc {DocIndex} '{DocumentType}' — downloading.")]
    private partial void LogDownloadingDoc(int filingIndex, int docIndex, string documentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} doc {DocIndex} fetch failed: {Error}.")]
    private partial void LogDocFetchFailed(int filingIndex, int docIndex, string error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} doc {DocIndex} returned content type '{ContentType}' — skipping.")]
    private partial void LogUnexpectedContentType(int filingIndex, int docIndex, string contentType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} doc {DocIndex} is empty — skipping.")]
    private partial void LogEmptyDocument(int filingIndex, int docIndex);

    [LoggerMessage(Level = LogLevel.Information, Message = "WA filing {FilingIndex} doc {DocIndex} '{DocumentType}' uploaded ({Size} bytes). ProxyUrl: '{ProxyUrl}'.")]
    private partial void LogDocUploaded(int filingIndex, int docIndex, string documentType, int size, string proxyUrl);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} doc {DocIndex} download timed out after {TimeoutSeconds}s.")]
    private partial void LogDocTimeout(int filingIndex, int docIndex, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to download WA filing {FilingIndex} doc {DocIndex}.")]
    private partial void LogDocDownloadFailed(int filingIndex, int docIndex, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} '{FilingNumber}' timed out.")]
    private partial void LogFilingTimeout(int filingIndex, string filingNumber);

    [LoggerMessage(Level = LogLevel.Warning, Message = "WA filing {FilingIndex} '{FilingNumber}' processing failed.")]
    private partial void LogFilingFailed(int filingIndex, string filingNumber, Exception exception);
}
