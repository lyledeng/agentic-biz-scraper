using BizScraper.Api.Infrastructure.Scraping.Engine.Models;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

/// <summary>
/// Defines a handler that executes a specific V2 flow action type.
/// </summary>
public interface IActionHandler
{
    string ActionType { get; }

    Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken);

    /// <summary>
    /// Returns action-specific detail string for inclusion in the ActionStarted log entry.
    /// Called before <see cref="ExecuteAsync"/>. Returns null if no details are relevant.
    /// </summary>
    string? GetLogDetails(ActionContext context, FlowActionV2 action);

    /// <summary>
    /// Returns post-execution detail string for inclusion in the ActionCompleted log entry.
    /// Called after <see cref="ExecuteAsync"/>. Default returns null.
    /// </summary>
    string? GetCompletionDetails(ActionContext context, FlowActionV2 action) => null;
}
