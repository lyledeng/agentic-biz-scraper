# Research: Extract Site-Specific Code from Generic Scraping Engine

**Feature**: 038-engine-site-code-extraction  
**Date**: 2026-04-13

## R-001: IPostFlowDocumentProcessor Interface Design

### Decision: Unified interface receiving `PostFlowDocumentContext` record with IPage, IBrowserContext, and supporting services

### Rationale

The four existing post-flow processor methods in `GenericScriptScraper.cs` have different parameter signatures:
- **WY** (`ProcessHistoryDocumentsAsync`): Uses `IBrowserContext` (API request context for SOS API calls)
- **MO** (`ProcessMoFilingDocumentsAsync`): Uses `IPage` (page-level fetch for session cookies on remote browsers)
- **WA** (`ProcessWaFilingDocumentsAsync`): Uses `IPage` (same page-level fetch pattern as MO) + `maxFilingDocuments` from definition variable
- **CO** (`ProcessCoCertifiedDocumentsAsync`): Uses `IBrowserContext` (API request context for certification viewer)
- **DE** (`DeEntityDetailsProcessor`): Uses `IDocumentTranslator`, `IMarkdownToPdfConverter`, `IBlobStorageClient` (FAB translation pipeline)

A single `PostFlowDocumentContext` record bundles the superset of context each processor needs. Each implementation picks what it uses:

```csharp
public interface IPostFlowDocumentProcessor
{
    string SlugPrefix { get; }  // e.g. "us-wy", "us-mo", "de-de"
    Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken);
}

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

The `Definition` parameter gives processors access to definition-level variables (e.g., WA's `maxFilingDocuments`), eliminating hardcoded configuration extraction from the engine.

### Alternatives Considered
- **Separate interfaces per parameter pattern** (IBrowserContextDocumentProcessor, IPageDocumentProcessor): Rejected — creates unnecessary interface proliferation. The context record approach is simpler and follows the existing codebase pattern of passing rich context objects (see `ActionContext`).
- **Method overloads on a single interface**: Rejected — violates ISP by forcing implementors to handle both overloads.

## R-002: Convention-Based DI Auto-Discovery Pattern

### Decision: Assembly scanning via `IServiceCollection` extension method using reflection at startup

### Rationale

.NET DI does not natively support "scan all implementations of interface X in assembly." The simplest approach: a single extension method that scans the assembly for types implementing a target interface and registers them:

```csharp
internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAllImplementations<TInterface>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var interfaceType = typeof(TInterface);
        var implementations = interfaceType.Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && interfaceType.IsAssignableFrom(t));

        foreach (var impl in implementations)
        {
            services.Add(new ServiceDescriptor(interfaceType, impl, lifetime));
        }

        return services;
    }
}
```

Registration in `Program.cs` reduces to:
```csharp
builder.Services.AddAllImplementations<IPostFlowDocumentProcessor>();
builder.Services.AddAllImplementations<ISearchResultMapper>();
builder.Services.AddAllImplementations<IEntityDetailMapper>();
```

This replaces the current manual keyed-singleton pattern (6+ factory lambdas per state).

### Alternatives Considered
- **Scrutor library** (`builder.Services.Scan()`): Rejected — adds external dependency for a trivial operation. YAGNI.
- **Source generators for DI registration**: Over-engineered for 20 classes.
- **Manual registration with keyed services**: Current approach. Rejected because it requires `Program.cs` modification for each new state (the exact problem being fixed).

## R-003: Mapper Registry Resolution Strategy

### Decision: Dictionary-based registry keyed by slug prefix, with passthrough fallback

### Rationale

Both `SearchResultMapper` and `EntityDetailMapper` currently contain a slug-matching `switch` that dispatches to per-state methods. The replacement:

```csharp
internal sealed class MapperRegistry(
    IEnumerable<ISearchResultMapper> searchMappers,
    IEnumerable<IEntityDetailMapper> detailMappers)
{
    private readonly Dictionary<string, ISearchResultMapper> _searchMappers =
        searchMappers.ToDictionary(m => m.SlugPrefix, StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IEntityDetailMapper> _detailMappers =
        detailMappers.ToDictionary(m => m.SlugPrefix, StringComparer.OrdinalIgnoreCase);

    public ISearchResultMapper GetSearchMapper(string definitionSlug)
    {
        var prefix = ExtractSlugPrefix(definitionSlug);
        return _searchMappers.GetValueOrDefault(prefix) ?? PassthroughSearchMapper.Instance;
    }

    public IEntityDetailMapper GetDetailMapper(string definitionSlug)
    {
        var prefix = ExtractSlugPrefix(definitionSlug);
        return _detailMappers.GetValueOrDefault(prefix) ?? PassthroughEntityDetailMapper.Instance;
    }

    internal static string ExtractSlugPrefix(string slug)
    {
        // "us-co-business-search" → "us-co"
        var parts = slug.Split('-');
        return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}" : slug;
    }
}
```

**Slug prefix convention**: Extract first two segments of the definition slug (e.g., `us-co-business-search` → `us-co`). This matches the existing slug pattern (`{country}-{state}-{action}`) across all 12 definitions.

**Passthrough fallback**: When no mapper is registered for a slug prefix, return raw flow output as-is. This handles hypothetical future states that need only JSON definitions with no custom mapping.

### Alternatives Considered
- **Attribute-based routing** (`[MapperFor("us-co")]`): Rejected — adds ceremony without benefit. The `SlugPrefix` property is simpler and testable.
- **Convention from class name** (CoSearchResultMapper → "us-co"): Fragile — naming convention does not capture the country code reliably.

## R-004: Variable Resolution — Moving State URLs to Definitions

### Decision: Add `searchEntryUrl` with `defaultValue` to WY, IA, DE definitions; remove hardcoded injection from engine

### Rationale

CO already uses the correct pattern — `searchEntryUrl` is defined as a `source: "config"` variable in `us-co-business-search.json` and resolved via `PlaywrightOptions.SearchEntryUrl`. The engine injects it along with state-specific URLs:

```csharp
// Current (GenericScriptScraper.cs lines 328-334):
actionContext.Variables["searchEntryUrl"] = options.Value.SearchEntryUrl;
actionContext.Variables["wyomingSearchUrl"] = options.Value.WyomingSearchUrl;
actionContext.Variables["iowaSearchUrl"] = options.Value.IowaSearchUrl;
actionContext.Variables["germanySearchUrl"] = options.Value.GermanySearchUrl;
```

**Fix**: Each definition declares its own `searchEntryUrl` with a `defaultValue` containing the actual URL. The engine's `InitializeVariableDefaults()` (in `ScrapingFlowEngine.cs`) already resolves `defaultValue` into `context.Variables` before actions execute. The injection lines can be removed.

Definition changes:
- `us-wy-business-search.json`: Rename `wyomingSearchUrl` → `searchEntryUrl`, add `"defaultValue": "https://wyobiz.wyo.gov/Business/FilingSearch.aspx"`
- `us-ia-business-search.json`: Rename `iowaSearchUrl` → `searchEntryUrl`, add `"defaultValue": "https://sos.iowa.gov/search/business/Search.aspx"`
- `de-de-business-search.json`: Rename `germanySearchUrl` → `searchEntryUrl`, add `"defaultValue": "https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml"`

**Also update the action scripts**: Any JavaScript in the definition actions that references `{{wyomingSearchUrl}}`, `{{iowaSearchUrl}}`, or `{{germanySearchUrl}}` must be updated to `{{searchEntryUrl}}`.

The `maxPages` variable follows the same `source: "config"` pattern and can remain injected generically since it applies to all definitions.

### Alternatives Considered
- **Environment variable per state** (e.g., `WY_SEARCH_URL`): Rejected — proliferates config without benefit. The URL is intrinsic to the definition.
- **Keep PlaywrightOptions but use a dictionary**: Rejected — still requires code changes for new states. Definition-level config is self-contained.

## R-005: V1 Scraper Deletion — Dependency Analysis

### Decision: Delete `JsonDrivenSearchScraper.cs` and `JsonDrivenEntityDetailScraper.cs` with cascading cleanup

### Rationale

Analysis of all references:

**JsonDrivenSearchScraper** referenced in:
1. `Program.cs` — keyed singleton registrations for CO and WY (lines 180-200)
2. `IStateSearchScraper` interface — implemented by `JsonDrivenSearchScraper`
3. Unit tests — `JsonDrivenSearchScraperTests.cs`

**JsonDrivenEntityDetailScraper** referenced in:
1. `Program.cs` — no direct registration found (was historical)
2. `IEntityDetailScraper` interface — implemented by `JsonDrivenEntityDetailScraper`
3. Unit tests — `JsonDrivenEntityDetailScraperTests.cs`

**Deletion cascade**:
1. Delete `JsonDrivenSearchScraper.cs` and `JsonDrivenEntityDetailScraper.cs`
2. Delete `IStateSearchScraper.cs` and `IEntityDetailScraper.cs` (v1 interfaces — only used by deleted classes)
3. Remove keyed singleton registrations from `Program.cs`
4. Delete corresponding unit test files
5. Verify no v1 API endpoints reference these interfaces (v1 endpoints were deprecated in feature 018)

**Risk**: Low — all execution already flows through V2 `GenericScriptScraper` via `/api/v2/execute-script`. The v1 scrapers are dead code wrapped around the same `ScrapingFlowEngine`.

### Alternatives Considered
- **Keep as deprecated adapters**: Rejected — dead code accumulates confusion and maintenance burden. Per clarification, delete entirely.

## R-006: IPostProcessor Consolidation into WY Mapper

### Decision: Merge `WyomingResultPostProcessor.ParseResultBlock` logic into `WySearchResultMapper`

### Rationale

`WyomingResultPostProcessor` is the only implementation of `IPostProcessor`. It runs as an `extract` action post-processor (triggered by `action.PostProcessor = "wyoming-result-parser"`) to parse WY's unique result block format (status, standing, filed date labels).

**Current flow**: Extract action → raw text → `WyomingResultPostProcessor.ProcessAsync` → parsed `NormalizedSearchResult`

**New flow**: The WY business search definition's extract action result will be mapped by `WySearchResultMapper` during the mapper phase in `ExecuteScriptHandler`. The parse logic moves into the mapper.

**Impact**: The `postProcessor` field in WY definition actions should be removed. The `ExtractActionHandler` no longer needs `IPostProcessor` injection — the extract action returns raw text, and the mapper handles normalization.

**Deletion cascade**:
1. Move `ParseResultBlock` logic → `WySearchResultMapper`
2. Delete `IPostProcessor.cs` and `WyomingResultPostProcessor.cs`
3. Remove `IPostProcessor`-related DI registration from `Program.cs`
4. Simplify `ExtractActionHandler` — remove post-processor dictionary and conditional invocation
5. Update WY definition JSON to remove `postProcessor` fields from extract actions

### Alternatives Considered
- **Keep IPostProcessor as ExtractActionHandler extension point**: Rejected — per clarification, consolidate into mapper. One abstraction for result normalization.

## R-007: DeEntityDetailsProcessor Unification

### Decision: Refactor into `DeDocumentProcessor : IPostFlowDocumentProcessor` with slug prefix `de-de`

### Rationale

Current `DeEntityDetailsProcessor` runs in `ExecuteScriptHandler` after all scraping completes. It receives the final `JsonElement` output, downloads the German PDF, translates it via FAB, converts to PDF, uploads, and returns modified output.

This is functionally identical to the other post-flow document processors in `GenericScriptScraper` — it modifies the output after the scraping flow completes. The only difference is its dependencies (`IDocumentTranslator`, `IMarkdownToPdfConverter`, `IPlaywrightPageFactory`).

**New design**: `DeDocumentProcessor` implements `IPostFlowDocumentProcessor` with slug prefix `de-de`. Its unique dependencies are constructor-injected via DI. The `PostFlowDocumentContext` provides the standard context; DE-specific services come from DI.

**Impact**: `ExecuteScriptHandler` removes `IsDeEntityDetails()` check and `deEntityDetailsProcessor` constructor parameter. The engine's post-flow processing loop handles DE uniformly.

**Execution location change**: Currently runs in handler (after engine returns). After refactoring, runs inside engine (during post-flow processing while browser is still alive). This is actually **better** because DE processing may need the live browser context for future enhancements.

### Alternatives Considered
- **Keep in handler layer**: Rejected per clarification — handler should have zero state-specific branching.

## R-008: GetExpectedStatePrefix Replacement

### Decision: Derive state prefix from definition metadata (`{Country}-{State}` from slug)

### Rationale

`GetExpectedStatePrefix()` is a hardcoded switch mapping slug prefixes to state codes (e.g., `us-co-` → `US-CO`). The mapping is deterministic from the slug structure: uppercase the first two segments.

```csharp
// Current:
private static string? GetExpectedStatePrefix(string slug) => slug switch { ... };

// Replacement:
private static string? GetExpectedStatePrefix(string slug)
{
    var parts = slug.Split('-');
    return parts.Length >= 2 ? $"{parts[0]}-{parts[1]}".ToUpperInvariant() : null;
}
```

This is a single-line derivation that handles all current and future states without modification. The `UniqueKeyEncoder.KnownStates` set can be populated from `FlowDefinitionLoader.SlugIndex` at startup instead of a hardcoded `HashSet`.

### Alternatives Considered
- **Store state prefix in definition metadata**: Adds a new field that duplicates information already in the slug. Rejected per YAGNI.
