namespace BizScraper.Api.Common.Configuration;

/// <summary>
/// Configuration for proxying headed-browser requests to a Windows VM running BizScraper under IIS.
/// </summary>
public sealed class WindowsProxyOptions
{
    /// <summary>
    /// Base URL of the Windows VM API (e.g. "https://aegis.ilienonline.com/mvpoc/bizscrapper-api").
    /// Empty string means the proxy is not configured.
    /// </summary>
    public string EndpointUrl { get; set; } = string.Empty;

    /// <summary>
    /// Timeout in seconds for proxied requests. Headed Chrome scraping can be slow.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// Whether the proxy endpoint is configured and ready to use.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(EndpointUrl);
}
