using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Extensions.Logging;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class NoOpAgentFallbackActionExecutor(ILogger<NoOpAgentFallbackActionExecutor> logger)
    : IAgentFallbackActionExecutor
{
    public Task<AgentFallbackActionExecutionResult> ExecuteClickAsync(
        ClickActionRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "No-op agent fallback action executor invoked for click on {Url}.",
            request.Observation.CurrentUrl);

        return Task.FromResult(new AgentFallbackActionExecutionResult(false, "No fallback action executed."));
    }

    public Task<AgentFallbackActionExecutionResult> ExecuteFillAsync(
        FillActionRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "No-op agent fallback action executor invoked for fill on {Url}.",
            request.Observation.CurrentUrl);

        return Task.FromResult(new AgentFallbackActionExecutionResult(false, "No fallback action executed."));
    }
}
