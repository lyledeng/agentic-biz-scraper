using System.ComponentModel.DataAnnotations;

namespace BizScraper.Api.Common.Configuration;

/// <summary>
/// Configuration for Azure Blob Storage used by diagnostics screenshots.
/// </summary>
public sealed class CloudStorageOptions
{
    public bool Enabled { get; set; }

    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "diagnostics";
}

/// <summary>
/// Controls when Playwright tracing is captured during scraping operations.
/// </summary>
public enum TracingMode
{
    Off = 0,
    OnFailure = 1,
    Always = 2
}

/// <summary>
/// Configuration for scraping diagnostics including tracing, screenshots, and cloud storage.
/// </summary>
public sealed class DiagnosticsOptions
{
    public TracingMode TracingMode { get; set; } = TracingMode.Always;

    public bool ScreenshotsEnabled { get; set; } = true;

    [Required(AllowEmptyStrings = false)]
    public string OutputPath { get; set; } = "diagnostics";

    public CloudStorageOptions CloudStorage { get; set; } = new();
}