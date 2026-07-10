using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine;

/// <summary>
/// Execution context shared across action handlers during a scraping flow run.
/// </summary>
public sealed class ActionContext(
    IPage page,
    IBrowserContext browserContext,
    string correlationId,
    string blobPrefix,
    Func<IPage, string, string, bool, CancellationToken, Task> captureScreenshot)
{
    public IPage Page { get; } = page;
    public IBrowserContext BrowserContext { get; } = browserContext;
    public string CorrelationId { get; } = correlationId;
    public string BlobPrefix { get; } = blobPrefix;
    public Dictionary<string, object?> Variables { get; } = [];
    public bool Truncated { get; set; }

    // V2 properties — set by ScrapingFlowEngine before action execution
    public FlowEnvironment? Environment { get; set; }
    public IReadOnlyDictionary<string, ScriptDefinition>? Scripts { get; set; }

    /// <summary>
    /// Variable names marked sensitive in the flow definition. Used for log redaction.
    /// </summary>
    public IReadOnlySet<string> SensitiveVariableNames { get; set; } = new HashSet<string>();

    /// <summary>
    /// Current loop nesting depth (0 = not in a loop). Set by LoopActionHandler.
    /// </summary>
    public int LoopDepth { get; set; }

    /// <summary>
    /// Current iteration index within the innermost loop (0-based). Set by LoopActionHandler.
    /// </summary>
    public int LoopIteration { get; set; }

    public Task CaptureScreenshotAsync(string milestone, bool force, CancellationToken cancellationToken) =>
        captureScreenshot(Page, BlobPrefix, milestone, force, cancellationToken);
}
