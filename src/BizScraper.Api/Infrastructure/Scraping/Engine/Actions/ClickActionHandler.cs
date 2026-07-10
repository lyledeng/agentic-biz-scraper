using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class ClickActionHandler(TargetResolver targetResolver) : IActionHandler
{
    private const int DefaultSelectorTimeoutMs = 5000;

    public string ActionType => "click";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action)
    {
        var description = action.Target?.Description ?? action.Selector;
        return $"target={description}, waitAfter={action.WaitAfter}";
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        ILocator locator;
        if (action.Target is not null)
        {
            var timeoutMs = context.Environment?.SelectorTimeoutMs ?? DefaultSelectorTimeoutMs;
            locator = await targetResolver.ResolveAsync(action.Target, context.Page, context.Variables, timeoutMs, cancellationToken);
        }
        else
        {
            var selector = VariableSubstitution.Resolve(action.Selector, context.Variables);
            locator = context.Page.Locator(selector).First;
        }

        await locator.ClickAsync();

        if (action.WaitAfter is not null)
        {
            var loadState = LoadState.NetworkIdle;
            if (string.Equals(action.WaitAfter, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
            {
                loadState = LoadState.DOMContentLoaded;
            }
            else if (string.Equals(action.WaitAfter, "load", StringComparison.OrdinalIgnoreCase))
            {
                loadState = LoadState.Load;
            }

            await context.Page.WaitForLoadStateAsync(loadState);
        }
    }
}
