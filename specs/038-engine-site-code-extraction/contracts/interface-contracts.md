# Interface Contracts: Extract Site-Specific Code from Generic Scraping Engine

**Feature**: 038-engine-site-code-extraction  
**Date**: 2026-04-13

## IPostFlowDocumentProcessor

```csharp
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
    /// <param name="output">The composed flow output dictionary to modify.</param>
    /// <param name="context">Execution context with browser, storage, and configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken);
}
```

## PostFlowDocumentContext

```csharp
namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

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
```

## ISearchResultMapper

```csharp
namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Abstraction for normalizing raw search results from a specific state's format
/// into the unified API response shape.
/// </summary>
public interface ISearchResultMapper
{
    /// <summary>
    /// The definition slug prefix this mapper handles (e.g., "us-co", "de-de").
    /// </summary>
    string SlugPrefix { get; }

    /// <summary>
    /// Map raw flow output to an array of unified search results.
    /// Returns null if the output cannot be mapped.
    /// </summary>
    UnifiedSearchResult[]? Map(JsonElement output);
}
```

## IEntityDetailMapper

```csharp
namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Abstraction for normalizing raw entity details from a specific state's format
/// into the unified API response shape.
/// </summary>
public interface IEntityDetailMapper
{
    /// <summary>
    /// The definition slug prefix this mapper handles (e.g., "us-co", "de-de").
    /// </summary>
    string SlugPrefix { get; }

    /// <summary>
    /// Map raw flow output to a unified entity detail response.
    /// Returns null if the output cannot be mapped.
    /// </summary>
    UnifiedEntityDetailResponse? Map(JsonElement output);
}
```

## Registry Contracts

### PostFlowDocumentProcessorRegistry

```csharp
namespace BizScraper.Api.Infrastructure.Scraping.Engine.DocumentProcessors;

/// <summary>
/// Resolves post-flow document processors by definition slug prefix.
/// </summary>
internal sealed class PostFlowDocumentProcessorRegistry(IEnumerable<IPostFlowDocumentProcessor> processors)
{
    /// <summary>
    /// Get the processor for the given definition slug, or null if none registered.
    /// </summary>
    public IPostFlowDocumentProcessor? GetProcessor(string definitionSlug);

    /// <summary>
    /// Extract slug prefix from a full definition slug.
    /// "us-co-entity-details" → "us-co"
    /// </summary>
    internal static string ExtractSlugPrefix(string slug);
}
```

### MapperRegistry

```csharp
namespace BizScraper.Api.Features.ExecuteScript.Mappers;

/// <summary>
/// Resolves search result and entity detail mappers by definition slug prefix.
/// Returns passthrough mappers when no match is found.
/// </summary>
internal sealed class MapperRegistry(
    IEnumerable<ISearchResultMapper> searchMappers,
    IEnumerable<IEntityDetailMapper> detailMappers)
{
    /// <summary>
    /// Get the search result mapper for the given definition slug.
    /// Returns PassthroughSearchMapper if no mapper is registered.
    /// </summary>
    public ISearchResultMapper GetSearchMapper(string definitionSlug);

    /// <summary>
    /// Get the entity detail mapper for the given definition slug.
    /// Returns PassthroughEntityDetailMapper if no mapper is registered.
    /// </summary>
    public IEntityDetailMapper GetDetailMapper(string definitionSlug);

    /// <summary>
    /// Extract slug prefix from a full definition slug.
    /// </summary>
    internal static string ExtractSlugPrefix(string slug);
}
```

## DI Registration Contract

```csharp
// Program.cs — replaces all manual keyed registrations
builder.Services.AddAllImplementations<IPostFlowDocumentProcessor>();
builder.Services.AddAllImplementations<ISearchResultMapper>();
builder.Services.AddAllImplementations<IEntityDetailMapper>();
builder.Services.AddSingleton<PostFlowDocumentProcessorRegistry>();
builder.Services.AddSingleton<MapperRegistry>();

// Removes:
// - All AddKeyedSingleton<IStateSearchScraper> registrations
// - AddSingleton<IPostProcessor, WyomingResultPostProcessor>
// - AddScoped<DeEntityDetailsProcessor>
// - AddSingleton<SearchResultMapper> (replaced by MapperRegistry + per-state mappers)
// - AddSingleton<EntityDetailMapper> (replaced by MapperRegistry + per-state mappers)
```

## Engine Post-Flow Processing Contract

The `GenericScriptScraper` post-flow processing loop replaces 4 conditional blocks:

```csharp
// After flow execution, while browser context is still alive:
var processor = processorRegistry.GetProcessor(definition.Metadata.DefinitionSlug);
if (processor is not null && result.Output is Dictionary<string, object?> output)
{
    try
    {
        await processor.ProcessAsync(output, context, cancellationToken);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Post-flow document processing failed for {Slug}", definition.Metadata.DefinitionSlug);
        // Flow result is returned with processing errors noted — not a fatal failure
    }
}
```
