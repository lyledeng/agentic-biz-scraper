using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class FillActionHandler(
    TargetResolver targetResolver,
    IAgentFallbackActionExecutor? agentFallbackActionExecutor = null) : IActionHandler
{
    private const int DefaultSelectorTimeoutMs = 5000;

    public string ActionType => "fill";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action)
    {
        var description = action.Target?.Description ?? action.Selector;
        var safeValue = VariableSubstitution.ResolveSafe(action.Value, context.Variables, context.SensitiveVariableNames);
        if (safeValue.Contains("***", StringComparison.Ordinal))
        {
            var actualValue = VariableSubstitution.Resolve(action.Value, context.Variables);
            return $"target={description}, value=[{actualValue.Length} chars]";
        }
        return $"target={description}, value={safeValue}";
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var value = VariableSubstitution.Resolve(action.Value, context.Variables);
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
                    BrowserActionType.Fill);
                await locator.FillAsync(value);
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

                var originalSelector = target?.Selectors?.FirstOrDefault()?.Value ?? action.Selector;
                var attemptedSelectors = ex.AttemptedSelectors?.ToList() ?? [];
                var observation = new BrowserObservation(
                    context.Page.Url,
                    await context.Page.TitleAsync(),
                    attemptedSelectors,
                    BrowserActionType.Fill,
                    target?.Description,
                    DateTimeOffset.UtcNow,
                    screenshot,
                    ex.GetType().Name,
                    ex.Message,
                    attemptedSelectors.FirstOrDefault() ?? originalSelector);

                var request = new FillActionRequest(observation, action.Target.Description, value, null);
                var fallbackResult = await agentFallbackActionExecutor.ExecuteFillAsync(request, cancellationToken);
                if (fallbackResult.Success)
                {
                    return;
                }

                throw;
            }
        }

        var selector = VariableSubstitution.Resolve(action.Selector, context.Variables);
        await context.Page.Locator(selector).FillAsync(value);
    }
}
