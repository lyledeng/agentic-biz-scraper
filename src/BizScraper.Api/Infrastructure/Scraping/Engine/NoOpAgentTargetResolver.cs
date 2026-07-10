using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Extensions.Logging;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

internal sealed class NoOpAgentTargetResolver(ILogger<NoOpAgentTargetResolver> logger) : IAgentTargetResolver
{
    public Task<AgentTargetResolutionResult> ResolveTargetAsync(
        BrowserObservation observation,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "No-op agent target resolver invoked for {ActionType} on {Url}. No fallback target produced.",
            observation.ActionType,
            observation.CurrentUrl);

        return Task.FromResult(new AgentTargetResolutionResult(
            false,
            null,
            null,
            null,
            null,
            ToAgentAction(observation.ActionType),
            $"No fallback target produced for {observation.ActionType}."));
    }

    private static AgentTargetResolutionAction ToAgentAction(BrowserActionType actionType) =>
        actionType switch
        {
            BrowserActionType.Click => AgentTargetResolutionAction.Click,
            BrowserActionType.Fill => AgentTargetResolutionAction.Fill,
            _ => AgentTargetResolutionAction.Click
        };
}
