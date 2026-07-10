using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class NavigateActionHandler : IActionHandler
{
    public string ActionType => "navigate";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action)
    {
        var url = VariableSubstitution.Resolve(action.Url, context.Variables);
        return $"url={url}, waitUntil={action.WaitUntil ?? "networkidle"}";
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var url = VariableSubstitution.Resolve(action.Url, context.Variables);

        var waitUntil = WaitUntilState.NetworkIdle;
        if (string.Equals(action.WaitUntil, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
        {
            waitUntil = WaitUntilState.DOMContentLoaded;
        }
        else if (string.Equals(action.WaitUntil, "load", StringComparison.OrdinalIgnoreCase))
        {
            waitUntil = WaitUntilState.Load;
        }

        await context.Page.GotoAsync(url, new PageGotoOptions { WaitUntil = waitUntil });
    }
}
