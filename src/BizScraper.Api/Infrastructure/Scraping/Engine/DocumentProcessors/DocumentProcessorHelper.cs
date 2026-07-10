namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Shared utility methods for post-flow document processors.
/// </summary>
internal static class DocumentProcessorHelper
{
    /// <summary>
    /// Builds a proxy URL for a blob path, using the current HTTP request context.
    /// </summary>
    public static string? BuildProxyUrl(IHttpContextAccessor httpContextAccessor, IConfiguration config, string blobPath)
    {
        var request = httpContextAccessor.HttpContext?.Request;
        if (request is null)
        {
            return null;
        }

        var pathBase = request.PathBase.HasValue
            ? request.PathBase.Value
            : NormalizePathBase(config["PathBase"]);
        var baseUrl = $"{request.Scheme}://{request.Host}{pathBase}";
        return $"{baseUrl}/api/v1/documents/{blobPath}";
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
