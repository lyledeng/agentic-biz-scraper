using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

public interface IAgentFallbackActionExecutor
{
    Task<AgentFallbackActionExecutionResult> ExecuteClickAsync(
        ClickActionRequest request,
        CancellationToken cancellationToken);

    Task<AgentFallbackActionExecutionResult> ExecuteFillAsync(
        FillActionRequest request,
        CancellationToken cancellationToken);
}
