namespace BizScraper.Api.Features.HealthCheck;

/// <summary>
/// Response from the /ready endpoint reflecting per-mode browser health status.
/// </summary>
/// <param name="Status">Overall readiness: "Ready" (all healthy), "Degraded" (partial), or "NotReady" (all unhealthy).</param>
/// <param name="BrowserModes">Per-mode health status keyed by mode name ("local", "remote").</param>
/// <param name="DefinitionsRequiringLocal">Definition slugs that have browser.mode = "local".</param>
/// <param name="DefinitionsRequiringRemote">Definition slugs that have browser.mode = "remote" (explicit).</param>
/// <param name="WindowsProxy">Health status of the Windows VM proxy, or null if no definitions require it.</param>
/// <param name="DefinitionsRoutedToWindows">Definition slugs that have browser.mode = "windows".</param>
public sealed record ReadinessHealthResponse(
    string Status,
    Dictionary<string, ModeStatus> BrowserModes,
    IReadOnlyList<string> DefinitionsRequiringLocal,
    IReadOnlyList<string> DefinitionsRequiringRemote,
    WindowsProxyStatus? WindowsProxy = null,
    IReadOnlyList<string>? DefinitionsRoutedToWindows = null);

/// <summary>
/// Health status of a single browser mode.
/// </summary>
/// <param name="Status">"healthy", "unhealthy", or "not-configured".</param>
/// <param name="Channel">Browser channel (local mode only).</param>
/// <param name="Endpoint">Remote endpoint URL (remote mode only).</param>
/// <param name="Error">Error message if unhealthy.</param>
public sealed record ModeStatus(
    string Status,
    string? Channel,
    string? Endpoint,
    string? Error);

/// <summary>
/// Health status of the Windows VM proxy.
/// </summary>
/// <param name="Status">"healthy", "unhealthy", or "not-configured".</param>
/// <param name="Endpoint">The configured Windows VM endpoint URL.</param>
/// <param name="Error">Error message if unhealthy.</param>
public sealed record WindowsProxyStatus(
    string Status,
    string? Endpoint,
    string? Error);
