using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class DownloadActionHandler(
    IBlobStorageClient blobStorageClient,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<DownloadActionHandler> logger) : IActionHandler
{
    public string ActionType => "download";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action)
    {
        var selector = VariableSubstitution.Resolve(action.LinkSelector, context.Variables);
        return $"selector={selector}, filenamePattern={action.FilenamePattern}";
    }

    public string? GetCompletionDetails(ActionContext context, FlowActionV2 action)
    {
        if (action.StoreAs is not null && context.Variables.TryGetValue(action.StoreAs, out var value))
        {
            return $"destination={value}";
        }
        return null;
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var selector = VariableSubstitution.Resolve(action.LinkSelector, context.Variables);
        var link = context.Page.Locator(selector).First;

        if (await link.CountAsync() == 0)
        {
            logger.LogDebug("Download link not found for selector '{Selector}'. Skipping download.", selector);
            return;
        }

        Microsoft.Playwright.IDownload download;
        try
        {
            var timeout = action.TimeoutMs ?? 30_000;
            var downloadTask = context.Page.WaitForDownloadAsync(new() { Timeout = timeout });
            await link.ClickAsync();
            download = await downloadTask;
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Download timed out for selector '{Selector}'. Skipping download.", selector);
            return;
        }

        if (!string.IsNullOrEmpty(action.ValidateExtension))
        {
            var suggestedFilename = download.SuggestedFilename;
            if (!suggestedFilename.EndsWith(action.ValidateExtension, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Downloaded file '{Filename}' does not match expected extension '{Extension}'. Discarding.",
                    suggestedFilename, action.ValidateExtension);
                await download.DeleteAsync();
                return;
            }
        }

        var filenamePattern = VariableSubstitution.Resolve(action.FilenamePattern, context.Variables);
        var diagnosticsDir = context.Variables.TryGetValue("__diagnosticsDir", out var dirObj) ? dirObj?.ToString() : null;

        if (string.IsNullOrEmpty(diagnosticsDir))
        {
            logger.LogWarning("No diagnostics directory available for download. Discarding downloaded file.");
            await download.DeleteAsync();
            return;
        }

        var localPath = Path.Combine(diagnosticsDir, filenamePattern);
        await download.SaveAsAsync(localPath);

        // Upload to blob storage and build proxy URL
        var blobPrefix = context.Variables.TryGetValue("__blobPrefix", out var prefixObj) && prefixObj is string prefix
            ? prefix
            : context.CorrelationId;
        var storageUrl = await UploadAndGetProxyUrlAsync(localPath, filenamePattern, blobPrefix, cancellationToken);

        if (!string.IsNullOrEmpty(action.StoreAs))
        {
            context.Variables[action.StoreAs] = storageUrl ?? localPath;
        }
    }

    private async Task<string?> UploadAndGetProxyUrlAsync(string localPath, string filename, string blobPrefix, CancellationToken cancellationToken)
    {
        var blobPath = $"{blobPrefix}/{filename}";
        try
        {
            await blobStorageClient.UploadAsync(blobPath, localPath, cancellationToken);

            var request = httpContextAccessor.HttpContext?.Request;
            if (request is not null)
            {
                var pathBase = request.PathBase.HasValue
                    ? request.PathBase.Value
                    : NormalizePathBase(configuration["PathBase"]);
                var baseUrl = $"{request.Scheme}://{request.Host}{pathBase}";
                return $"{baseUrl}/api/v1/documents/{blobPath}";
            }

            return blobStorageClient.GetFolderUrl(blobPrefix)?.AbsoluteUri is { } folderUrl
                ? $"{folderUrl.TrimEnd('/')}/{filename}"
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to upload downloaded file '{Filename}' to blob storage.", filename);
            return null;
        }
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
}
