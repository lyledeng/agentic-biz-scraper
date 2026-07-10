namespace BizScraper.Api.Common.Configuration;

/// <summary>
/// Configuration for the FAB document translation service.
/// </summary>
public sealed class FabDocumentTranslatorOptions
{
    public string EndpointUrl { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 60;

    public long MaxDocumentSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB
}
