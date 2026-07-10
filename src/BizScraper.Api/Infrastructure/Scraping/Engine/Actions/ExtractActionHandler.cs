using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class ExtractActionHandler : IActionHandler
{
    public string ActionType => "extract";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action)
    {
        var scriptRef = action.ScriptRef ?? "(inline)";
        return $"script={scriptRef}, storeAs={action.StoreAs}";
    }

    public string? GetCompletionDetails(ActionContext context, FlowActionV2 action)
    {
        if (action.StoreAs is not null && context.Variables.TryGetValue(action.StoreAs, out var value))
        {
            var length = value?.ToString()?.Length ?? 0;
            return $"resultLength={length}";
        }
        return null;
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var javascript = ScriptResolver.Resolve(action, context.Scripts);
        javascript = VariableSubstitution.ResolveJavaScript(javascript, context.Variables);
        var rawResult = await context.Page.EvaluateAsync<string>(javascript);

        if (!string.IsNullOrEmpty(action.StoreAs))
        {
            context.Variables[action.StoreAs] = rawResult;
        }
    }
}
