namespace BizScraper.Api.Common.Configuration;

/// <summary>
/// Configuration for the Playwright browser automation runtime.
/// </summary>
public sealed class PlaywrightOptions
{
    public string BrowserEndpoint { get; set; } = string.Empty;

    public string AccessToken { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 120;

    public int MaxPages { get; set; } = 10;

    public int RetryCount { get; set; } = 2;

    public int RetryDelayMilliseconds { get; set; } = 500;

    public int ReuseBrowserForRequests { get; set; } = 25;

    public bool IsRemoteMode => !string.IsNullOrWhiteSpace(BrowserEndpoint);

    public DiagnosticsOptions Diagnostics { get; set; } = new();

    public int DocumentDownloadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Browser channel for local mode (e.g. "chrome", "msedge"). Empty uses bundled Chromium.
    /// </summary>
    public string BrowserChannel { get; set; } = string.Empty;
}
