using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Features.HealthCheck;

namespace BizScraper.Api.Infrastructure.Scraping.Proxy;

/// <summary>
/// Forwards execute-script requests to the Windows VM running BizScraper under IIS.
/// </summary>
public interface IWindowsProxyService
{
    /// <summary>
    /// POSTs the execute-script request to the Windows VM and returns the response.
    /// </summary>
    Task<ExecuteScriptResponse> ForwardAsync(
        string definitionSlug,
        Dictionary<string, object?>? parameters,
        string correlationId,
        string? authorizationHeader,
        CancellationToken cancellationToken);

    /// <summary>
    /// Probes the Windows VM health endpoint.
    /// </summary>
    Task<WindowsProxyStatus> CheckWindowsProxyHealthAsync(CancellationToken cancellationToken);
}
