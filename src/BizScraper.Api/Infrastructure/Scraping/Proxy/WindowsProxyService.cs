using BizScraper.Api.Common.Configuration;
using BizScraper.Api.Features.ExecuteScript.Models;
using BizScraper.Api.Features.HealthCheck;
using Flurl.Http;
using Microsoft.Extensions.Options;

namespace BizScraper.Api.Infrastructure.Scraping.Proxy;

/// <summary>
/// Forwards execute-script requests to the Windows VM via Flurl.Http.
/// </summary>
internal sealed partial class WindowsProxyService(
    IOptions<WindowsProxyOptions> options,
    ILogger<WindowsProxyService> logger) : IWindowsProxyService
{
    private readonly WindowsProxyOptions _options = options.Value;

    public async Task<ExecuteScriptResponse> ForwardAsync(
        string definitionSlug,
        Dictionary<string, object?>? parameters,
        string correlationId,
        string? authorizationHeader,
        CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.EndpointUrl.TrimEnd('/')}/api/v2/execute-script";

        LogProxyForwarding(definitionSlug, correlationId, endpoint);

        try
        {
            var request = endpoint
                .WithTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
                .WithHeader("X-Correlation-Id", correlationId);

            if (!string.IsNullOrWhiteSpace(authorizationHeader))
            {
                request = request.WithHeader("Authorization", authorizationHeader);
            }

            var body = new
            {
                definition = definitionSlug,
                parameters,
                correlationId
            };

            var response = await request.PostJsonAsync(body, cancellationToken: cancellationToken);
            return await response.GetJsonAsync<ExecuteScriptResponse>();
        }
        catch (FlurlHttpTimeoutException ex)
        {
            LogProxyTimeout(definitionSlug, correlationId, _options.TimeoutSeconds, ex);
            throw new WindowsProxyException(504, "Gateway Timeout",
                $"Windows VM did not respond within {_options.TimeoutSeconds}s for '{definitionSlug}'.");
        }
        catch (FlurlHttpException ex) when (ex.StatusCode is not null)
        {
            // VM returned an HTTP error — propagate status and body
            var errorBody = await ex.GetResponseStringAsync();
            LogProxyVmError(definitionSlug, correlationId, ex.StatusCode.Value, ex);
            throw new WindowsProxyException(ex.StatusCode.Value, "Windows VM Error", errorBody);
        }
        catch (FlurlHttpException ex)
        {
            // Connection refused, DNS failure, etc.
            LogProxyConnectionFailure(definitionSlug, correlationId, ex);
            throw new WindowsProxyException(503, "Service Unavailable",
                $"Windows VM is unreachable for '{definitionSlug}': {ex.Message}", retryAfterSeconds: 30);
        }
        catch (HttpRequestException ex)
        {
            LogProxyConnectionFailure(definitionSlug, correlationId, ex);
            throw new WindowsProxyException(503, "Service Unavailable",
                $"Windows VM is unreachable for '{definitionSlug}': {ex.Message}", retryAfterSeconds: 30);
        }
    }

    public async Task<WindowsProxyStatus> CheckWindowsProxyHealthAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            return new WindowsProxyStatus("not-configured", null, null);
        }

        try
        {
            var healthUrl = $"{_options.EndpointUrl.TrimEnd('/')}/healthz";
            var response = await healthUrl
                .WithTimeout(TimeSpan.FromSeconds(5))
                .GetAsync(cancellationToken: cancellationToken);

            return new WindowsProxyStatus("healthy", _options.EndpointUrl, null);
        }
        catch (Exception ex)
        {
            return new WindowsProxyStatus("unhealthy", _options.EndpointUrl, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Proxying '{DefinitionSlug}' (correlationId={CorrelationId}) to Windows VM at {Endpoint}.")]
    private partial void LogProxyForwarding(string definitionSlug, string correlationId, string endpoint);

    [LoggerMessage(Level = LogLevel.Error, Message = "Windows VM timeout for '{DefinitionSlug}' (correlationId={CorrelationId}) after {TimeoutSeconds}s.")]
    private partial void LogProxyTimeout(string definitionSlug, string correlationId, int timeoutSeconds, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Windows VM returned HTTP {StatusCode} for '{DefinitionSlug}' (correlationId={CorrelationId}).")]
    private partial void LogProxyVmError(string definitionSlug, string correlationId, int statusCode, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Windows VM connection failure for '{DefinitionSlug}' (correlationId={CorrelationId}).")]
    private partial void LogProxyConnectionFailure(string definitionSlug, string correlationId, Exception exception);
}
