using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class WaitForLoadActionHandler : IActionHandler
{
    public string ActionType => "wait-for-load";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action) =>
        $"state={action.State ?? "networkidle"}";

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var loadState = LoadState.NetworkIdle;
        if (string.Equals(action.State, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
        {
            loadState = LoadState.DOMContentLoaded;
        }
        else if (string.Equals(action.State, "load", StringComparison.OrdinalIgnoreCase))
        {
            loadState = LoadState.Load;
        }

        await context.Page.WaitForLoadStateAsync(loadState);
    }
}
