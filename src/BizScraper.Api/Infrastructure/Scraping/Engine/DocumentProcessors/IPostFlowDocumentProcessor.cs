using BizScraper.Api.Common.Interfaces;
using BizScraper.Api.Infrastructure.Scraping.Engine.Models;
using Microsoft.Playwright;

namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Abstraction for state-specific document processing after a scraping flow completes.
/// Implementations handle document download, upload, and URL rewriting for a specific state.
/// </summary>
public interface IPostFlowDocumentProcessor
{
    /// <summary>
    /// The definition slug prefix this processor handles (e.g., "us-wy", "us-mo", "de-de").
    /// Used for convention-based resolution by <see cref="PostFlowDocumentProcessorRegistry"/>.
    /// </summary>
    string SlugPrefix { get; }

    /// <summary>
    /// Process documents after flow execution. Mutates the output dictionary in-place
    /// (e.g., replaces raw URLs with proxy URLs, adds uploaded document references).
    /// </summary>
    Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Immutable context passed to post-flow document processors.
/// Contains the live browser session and supporting services.
/// </summary>
public sealed record PostFlowDocumentContext(
    IPage Page,
    IBrowserContext BrowserContext,
    IBlobStorageClient BlobStorage,
    IHttpContextAccessor HttpContextAccessor,
    IConfiguration Configuration,
    string BlobPrefix,
    string DiagnosticsDirectory,
    int DocumentTimeoutSeconds,
    FlowDefinitionV2 Definition);
