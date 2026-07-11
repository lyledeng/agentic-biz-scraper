using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class ClickActionHandler(
    TargetResolver targetResolver,
    IAgentFallbackActionExecutor? agentFallbackActionExecutor = null) : IActionHandler
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
        var target = action.Target;
        if (target is not null)
        {
            try
            {
                var timeoutMs = context.Environment?.SelectorTimeoutMs ?? DefaultSelectorTimeoutMs;
                var locator = await targetResolver.ResolveAsync(
                    target,
                    context.Page,
                    context.Variables,
                    timeoutMs,
                    cancellationToken,
                    BrowserActionType.Click);

                await locator.ClickAsync();
                return;
            }
            catch (TargetResolutionException ex) when (agentFallbackActionExecutor is not null)
            {
                BrowserScreenshot? screenshot = null;
                try
                {
                    var bytes = await context.Page.ScreenshotAsync(new PageScreenshotOptions { FullPage = false });
                    screenshot = new BrowserScreenshotBytes(bytes);
                }
                catch
                {
                    screenshot = null;
                }

                var originalSelector = target.Selectors.Count > 0 ? target.Selectors[0].Value : action.Selector;
                var attemptedSelectors = ex.AttemptedSelectors?.ToList() ?? [];
                var observation = new BrowserObservation(
                    context.Page.Url,
                    await context.Page.TitleAsync(),
                    attemptedSelectors,
                    BrowserActionType.Click,
                    target.Description,
                    DateTimeOffset.UtcNow,
                    screenshot,
                    ex.GetType().Name,
                    ex.Message,
                    attemptedSelectors.FirstOrDefault() ?? originalSelector);

                var request = new ClickActionRequest(observation, target.Description, null);
                var fallbackResult = await agentFallbackActionExecutor.ExecuteClickAsync(request, cancellationToken);
                if (fallbackResult.Success)
                {
                    return;
                }

                throw;
            }
        }

        var selector = VariableSubstitution.Resolve(action.Selector, context.Variables);
        await context.Page.Locator(selector).First.ClickAsync();

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
