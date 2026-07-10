using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.Actions;

internal sealed class ScreenshotActionHandler : IActionHandler
{
    public string ActionType => "screenshot";

    public string? GetLogDetails(ActionContext context, FlowActionV2 action)
    {
        var milestone = VariableSubstitution.Resolve(action.Milestone, context.Variables);
        return $"milestone={milestone}, force={action.Force ?? false}";
    }

    public async Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken)
    {
        var milestone = VariableSubstitution.Resolve(action.Milestone, context.Variables);
        var force = action.Force ?? false;

        await context.CaptureScreenshotAsync(milestone, force, cancellationToken);
    }
}
