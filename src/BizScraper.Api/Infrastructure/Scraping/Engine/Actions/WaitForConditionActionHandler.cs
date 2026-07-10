using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class WaitForConditionActionHandler : IActionHandler
{
    public string ActionType => "wait-for-condition";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action) =>
        $"condition={action.Javascript}, timeoutMs={action.TimeoutMs ?? 5000}";

    public string? GetCompletionDetails(ActionContext context, FlowActionV2 action) => "outcome=met";

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var javascript = VariableSubstitution.ResolveJavaScript(action.Javascript, context.Variables);
        var timeout = action.TimeoutMs ?? 5000;

        try
        {
            await context.Page.WaitForFunctionAsync(javascript, new PageWaitForFunctionOptions
            {
                Timeout = timeout
            });
        }
        catch (TimeoutException)
        {
            // Timeout is expected when condition isn't met — treat as no-op
        }
    }
}
