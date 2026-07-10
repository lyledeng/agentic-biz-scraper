using System.Text.Json;
using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Infrastructure.Pdf;
using Microsoft.Extensions.Options;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Post-flow document processor for Germany (DE) entity details.
/// Reads the downloaded AD PDF from blob storage, translates via FAB,
/// converts translated markdown to PDF, uploads to blob, and assembles
/// the final output with original + translated document references.
/// </summary>
internal sealed partial class DeDocumentProcessor(
    IDocumentTranslator documentTranslator,
    IMarkdownToPdfConverter pdfConverter,
    IPlaywrightPageFactory playwrightPageFactory,
    IBlobStorageClient blobStorageClient,
    IOptions<PlaywrightOptions> playwrightOptions,
    IWebHostEnvironment environment,
    ILogger<DeDocumentProcessor> logger) : IPostFlowDocumentProcessor
{
    private const string DocumentsPrefix = "/api/v1/documents/";

    public string SlugPrefix => "de-de";

    public async Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken)
    {
        // Extract the original document URL from the output
        string? originalDocumentUrl = null;
        if (output.TryGetValue("originalDocumentUrl", out var urlObj))
        {
            originalDocumentUrl = urlObj switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
                _ => null
            };
        }

        if (string.IsNullOrEmpty(originalDocumentUrl))
        {
            LogNoDocument(context.BlobPrefix);
            return;
        }

        var correlationId = context.BlobPrefix.Split('/').LastOrDefault() ?? "unknown";

        var originalBlobPath = ExtractBlobPath(originalDocumentUrl);
        var originalFileName = Path.GetFileName(originalBlobPath ?? $"de-hardcopy-{correlationId}-original.pdf");

        var pdfBytes = await ReadPdfFromBlobAsync(originalBlobPath, cancellationToken);

        var blobPrefixFromUrl = originalBlobPath is not null
            ? originalBlobPath[..originalBlobPath.LastIndexOf('/')]
            : context.BlobPrefix;

        if (pdfBytes is null)
        {
            LogPdfNotAvailable(originalBlobPath, correlationId);
            output["documents"] = JsonSerializer.SerializeToElement(new[]
            {
                new
                {
                    title = "Current Hardcopy Printout",
                    date = (string?)null,
                    downloads = new[]
                    {
                        new { label = "Original", proxyUrl = (string?)originalDocumentUrl, fileName = originalFileName, error = (string?)null },
                        new { label = "Translated", proxyUrl = (string?)null, fileName = $"de-hardcopy-{correlationId}-translated.pdf", error = (string?)"Original PDF not available for translation" }
                    }
                }
            });
            return;
        }

        // Translate and upload
        string? translatedProxyUrl = null;
        string translatedFileName = $"de-hardcopy-{correlationId}-translated.pdf";
        string? translatedError = null;

        try
        {
            var translationResult = await documentTranslator.TranslateAsync(pdfBytes, cancellationToken);
            LogTranslationReceived(correlationId, translationResult.SourceLanguage);

            await using var pageLease = await playwrightPageFactory.CreatePageAsync();
            var translatedPdfBytes = await pdfConverter.ConvertAsync(translationResult.TranslatedMarkdown, pageLease.Page, cancellationToken);

            var translatedBlobPath = $"{blobPrefixFromUrl}/{translatedFileName}";
            var diagnosticsDir = Path.Combine(
                environment.ContentRootPath,
                playwrightOptions.Value.Diagnostics.OutputPath,
                blobPrefixFromUrl);
            Directory.CreateDirectory(diagnosticsDir);
            var diagnosticsPath = Path.Combine(diagnosticsDir, translatedFileName);
            await File.WriteAllBytesAsync(diagnosticsPath, translatedPdfBytes, cancellationToken);
            await blobStorageClient.UploadAsync(translatedBlobPath, diagnosticsPath, cancellationToken);

            translatedProxyUrl = BuildProxyUrl(context.HttpContextAccessor, translatedBlobPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogTranslationFailed(correlationId, ex);
            translatedError = ex.Message;
        }

        output["documents"] = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                title = "Current Hardcopy Printout",
                date = (string?)null,
                downloads = new[]
                {
                    new { label = "Original", proxyUrl = (string?)originalDocumentUrl, fileName = originalFileName, error = (string?)null },
                    new { label = "Translated", proxyUrl = (string?)translatedProxyUrl, fileName = translatedFileName, error = (string?)translatedError }
                }
            }
        });
    }

    private async Task<byte[]?> ReadPdfFromBlobAsync(string? blobPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(blobPath))
        {
            return null;
        }

        await using var downloadResult = await blobStorageClient.DownloadBlobAsync(blobPath, cancellationToken);
        if (downloadResult is null)
        {
            return null;
        }

        using var ms = new MemoryStream();
        await downloadResult.Content.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static string? ExtractBlobPath(string? proxyUrl)
    {
        if (string.IsNullOrEmpty(proxyUrl))
        {
            return null;
        }

        var idx = proxyUrl.IndexOf(DocumentsPrefix, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? proxyUrl[(idx + DocumentsPrefix.Length)..] : null;
    }

    private static string BuildProxyUrl(IHttpContextAccessor httpContextAccessor, string blobPath)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is not null)
        {
            var pathBase = request.PathBase.HasValue
                ? request.PathBase.Value
                : NormalizePathBase(request.HttpContext.RequestServices.GetService<IConfiguration>()?["PathBase"]);
            var baseUrl = $"{request.Scheme}://{request.Host}{pathBase}";
            return $"{baseUrl}{DocumentsPrefix}{blobPath}";
        }

        return $"{DocumentsPrefix}{blobPath}";
    }

    private static string NormalizePathBase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "No AD document available for blob prefix '{BlobPrefix}'. Skipping DE document processing.")]
    private partial void LogNoDocument(string blobPrefix);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not read original PDF from blob at '{BlobPath}' for correlation {CorrelationId}.")]
    private partial void LogPdfNotAvailable(string? blobPath, string correlationId);

    [LoggerMessage(Level = LogLevel.Information, Message = "FAB translation received for correlation {CorrelationId}, source language: {Language}.")]
    private partial void LogTranslationReceived(string correlationId, string language);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Translation failed for correlation {CorrelationId}. Recording partial success.")]
    private partial void LogTranslationFailed(string correlationId, Exception exception);
}
