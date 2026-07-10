using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

public interface IAgentTargetResolver
{
    Task<AgentTargetResolutionResult> ResolveTargetAsync(
        BrowserObservation observation,
        CancellationToken cancellationToken);
}
