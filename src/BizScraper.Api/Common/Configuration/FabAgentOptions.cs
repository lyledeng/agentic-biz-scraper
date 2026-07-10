namespace BizScraper.Api.Common.Configuration;

/// <summary>
/// Configuration for the FAB AI agent service integration.
/// </summary>
public sealed class FabAgentOptions
{
    public CaptchaSolverOptions CaptchaSolver { get; set; } = new();
}

/// <summary>
/// Configuration for the FAB CAPTCHA solving service.
/// </summary>
public sealed class CaptchaSolverOptions
{
    public string EndpointUrl { get; set; } = string.Empty;

    public string AuthToken { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;
}
