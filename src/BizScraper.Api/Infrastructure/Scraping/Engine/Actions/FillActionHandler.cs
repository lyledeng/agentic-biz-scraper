using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

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

        if (action.Target is not null)
        {
            try
            {
                var timeoutMs = context.Environment?.SelectorTimeoutMs ?? DefaultSelectorTimeoutMs;
                var locator = await targetResolver.ResolveAsync(
                    action.Target,
                    context.Page,
                    context.Variables,
                    timeoutMs,
                    cancellationToken,
                    BrowserActionType.Fill);
                await locator.FillAsync(value);
                return;
            }
            catch (TargetResolutionException) when (agentFallbackActionExecutor is not null)
            {
                var observation = new BrowserObservation(
                    context.Page.Url,
                    await context.Page.TitleAsync(),
                    [],
                    BrowserActionType.Fill,
                    action.Target.Description,
                    DateTimeOffset.UtcNow);

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
