# Data Model: Extract Site-Specific Code from Generic Scraping Engine

**Feature**: 038-engine-site-code-extraction  
**Date**: 2026-04-13

## New Abstractions

### IPostFlowDocumentProcessor

Abstraction for state-specific document download, upload, and URL rewriting after a scraping flow completes.

```csharp
public interface IPostFlowDocumentProcessor
{
    /// <summary>Slug prefix this processor handles (e.g., "us-wy", "us-mo", "de-de").</summary>
    string SlugPrefix { get; }

    /// <summary>Process documents after flow execution, mutating the output dictionary in-place.</summary>
    Task ProcessAsync(Dictionary<string, object?> output, PostFlowDocumentContext context, CancellationToken cancellationToken);
}
```

| Implementation | SlugPrefix | Extracted From | Key Dependencies |
|---------------|-----------|----------------|------------------|
| WyDocumentProcessor | `us-wy` | `GenericScriptScraper.ProcessHistoryDocumentsAsync` | IBrowserContext (API request) |
| MoDocumentProcessor | `us-mo` | `GenericScriptScraper.ProcessMoFilingDocumentsAsync` | IPage (page-level fetch) |
| WaDocumentProcessor | `us-wa` | `GenericScriptScraper.ProcessWaFilingDocumentsAsync` | IPage (page-level fetch), maxFilingDocuments from definition |
| CoDocumentProcessor | `us-co` | `GenericScriptScraper.ProcessCoCertifiedDocumentsAsync` | IBrowserContext (API request) |
| DeDocumentProcessor | `de-de` | `DeEntityDetailsProcessor` | IDocumentTranslator, IMarkdownToPdfConverter, IPlaywrightPageFactory |

### PostFlowDocumentContext

Immutable context record passed to every document processor.

```csharp
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

| Property | Source | Used By |
|----------|--------|---------|
| Page | Live Playwright page from flow execution | MO, WA (page-level fetch) |
| BrowserContext | Live Playwright browser context | WY, CO (API request context) |
| BlobStorage | DI-injected service | All (upload documents) |
| HttpContextAccessor | DI-injected service | All (build proxy URLs) |
| Configuration | DI-injected service | All (blob container name) |
| BlobPrefix | Computed from correlation ID | All (storage path) |
| DiagnosticsDirectory | Computed from request | All (local save path) |
| DocumentTimeoutSeconds | From PlaywrightOptions | All (download timeout) |
| Definition | The active flow definition | WA (maxFilingDocuments from variables) |

### PostFlowDocumentProcessorRegistry

Resolves processor(s) for a definition slug by extracting the slug prefix.

```csharp
internal sealed class PostFlowDocumentProcessorRegistry
{
    // Constructor: IEnumerable<IPostFlowDocumentProcessor> → Dictionary<string, IPostFlowDocumentProcessor>
    IPostFlowDocumentProcessor? GetProcessor(string definitionSlug);
    static string ExtractSlugPrefix(string slug);  // "us-co-entity-details" → "us-co"
}
```

---

### ISearchResultMapper

Abstraction for normalizing raw search results into the unified API response shape.

```csharp
public interface ISearchResultMapper
{
    /// <summary>Slug prefix this mapper handles (e.g., "us-co", "de-de").</summary>
    string SlugPrefix { get; }

    /// <summary>Map raw flow output to unified search results.</summary>
    UnifiedSearchResult[]? Map(JsonElement output);
}
```

| Implementation | SlugPrefix | Extracted From | Notes |
|---------------|-----------|----------------|-------|
| CoSearchResultMapper | `us-co` | `SearchResultMapper.MapCoResults` | |
| WySearchResultMapper | `us-wy` | `SearchResultMapper.MapWyResults` + `WyomingResultPostProcessor` | Consolidates post-processor parsing |
| IaSearchResultMapper | `us-ia` | `SearchResultMapper.MapIaResults` | |
| MoSearchResultMapper | `us-mo` | `SearchResultMapper.MapMoResults` | |
| WaSearchResultMapper | `us-wa` | `SearchResultMapper.MapWaResults` | |
| DeSearchResultMapper | `de-de` | `SearchResultMapper.MapDeResults` | |

### IEntityDetailMapper

Abstraction for normalizing raw entity details into the unified API response shape.

```csharp
public interface IEntityDetailMapper
{
    /// <summary>Slug prefix this mapper handles (e.g., "us-co", "de-de").</summary>
    string SlugPrefix { get; }

    /// <summary>Map raw flow output to unified entity detail response.</summary>
    UnifiedEntityDetailResponse? Map(JsonElement output);
}
```

| Implementation | SlugPrefix | Extracted From |
|---------------|-----------|----------------|
| CoEntityDetailMapper | `us-co` | `EntityDetailMapper.MapCoDetails` |
| WyEntityDetailMapper | `us-wy` | `EntityDetailMapper.MapWyDetails` |
| IaEntityDetailMapper | `us-ia` | `EntityDetailMapper.MapIaDetails` |
| MoEntityDetailMapper | `us-mo` | `EntityDetailMapper.MapMoDetails` |
| WaEntityDetailMapper | `us-wa` | `EntityDetailMapper.MapWaDetails` |
| DeEntityDetailMapper | `de-de` | `EntityDetailMapper.MapDeDetails` |

### MapperRegistry

Resolves the correct mapper by definition slug prefix with passthrough fallback.

```csharp
internal sealed class MapperRegistry
{
    // Constructor: IEnumerable<ISearchResultMapper>, IEnumerable<IEntityDetailMapper>
    ISearchResultMapper GetSearchMapper(string definitionSlug);      // Returns PassthroughSearchMapper if no match
    IEntityDetailMapper GetDetailMapper(string definitionSlug);      // Returns PassthroughEntityDetailMapper if no match
    static string ExtractSlugPrefix(string slug);
}
```

---

## Changes to Existing Models

### PlaywrightOptions (properties removed)

| Property | Current Value | Replacement |
|----------|-------------|-------------|
| `WyomingSearchUrl` | `https://wyobiz.wyo.gov/Business/FilingSearch.aspx` | `us-wy-business-search.json` variable `searchEntryUrl` with `defaultValue` |
| `IowaSearchUrl` | `https://sos.iowa.gov/search/business/Search.aspx` | `us-ia-business-search.json` variable `searchEntryUrl` with `defaultValue` |
| `GermanySearchUrl` | `https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml` | `de-de-business-search.json` variable `searchEntryUrl` with `defaultValue` |

### GenericScriptScraper (methods removed)

| Method | Lines | Destination |
|--------|-------|-------------|
| `ProcessHistoryDocumentsAsync` | static, ~150 lines | `WyDocumentProcessor.ProcessAsync` |
| `ProcessMoFilingDocumentsAsync` | static, ~190 lines | `MoDocumentProcessor.ProcessAsync` |
| `ProcessWaFilingDocumentsAsync` | static, ~240 lines | `WaDocumentProcessor.ProcessAsync` |
| `ProcessCoCertifiedDocumentsAsync` | static, ~60 lines | `CoDocumentProcessor.ProcessAsync` |
| URL injection (lines 330-334) | 3 lines | Removed — resolved from definition variables |
| 4 conditional post-flow blocks (lines 348-397) | ~50 lines | Replaced by registry-based loop |

### SearchResultMapper / EntityDetailMapper (refactored)

Current state-branching `MapToUnified` methods become thin delegators:

```csharp
// Before: switch on slug, call private per-state methods
// After: delegate to MapperRegistry
internal sealed class SearchResultMapper(MapperRegistry registry)
{
    public UnifiedSearchResult[]? MapToUnified(JsonElement output, string definitionSlug)
        => registry.GetSearchMapper(definitionSlug).Map(output);
}
```

The 6 per-state private methods are extracted to their own classes and deleted from the mapper.

### ExtractActionHandler (simplified)

After `IPostProcessor` removal:
- Remove `Dictionary<string, IPostProcessor> _postProcessors` field
- Remove constructor parameter `IEnumerable<IPostProcessor>`
- Remove `if (!string.IsNullOrEmpty(action.PostProcessor))` block
- The `postProcessor` field on WY definition actions is removed

### ExecuteScriptHandler (simplified)

- Remove `DeEntityDetailsProcessor` constructor parameter and DI registration
- Remove `IsDeEntityDetails()` method
- Remove DE-specific post-processing block (lines 93-126)
- Replace `GetExpectedStatePrefix()` hardcoded switch with slug-derived computation

---

## Definition File Changes

### Variable Rename (3 definitions)

| Definition | Old Variable | New Variable | Default Value |
|-----------|-------------|-------------|---------------|
| `us-wy-business-search.json` | `wyomingSearchUrl` | `searchEntryUrl` | `https://wyobiz.wyo.gov/Business/FilingSearch.aspx` |
| `us-ia-business-search.json` | `iowaSearchUrl` | `searchEntryUrl` | `https://sos.iowa.gov/search/business/Search.aspx` |
| `de-de-business-search.json` | `germanySearchUrl` | `searchEntryUrl` | `https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml` |

Action scripts within these definitions that reference `{{wyomingSearchUrl}}`, `{{iowaSearchUrl}}`, or `{{germanySearchUrl}}` are updated to `{{searchEntryUrl}}`.

### PostProcessor Removal (WY definition)

The `postProcessor` field is removed from extract actions in `us-wy-business-search.json`. Result parsing moves to `WySearchResultMapper`.

---

## Files Deleted

| File | Reason |
|------|--------|
| `JsonDrivenSearchScraper.cs` | Deprecated v1 wrapper — all execution via V2 GenericScriptScraper |
| `JsonDrivenEntityDetailScraper.cs` | Deprecated v1 wrapper — all execution via V2 GenericScriptScraper |
| `IStateSearchScraper.cs` | V1 interface — only implemented by deleted class |
| `IEntityDetailScraper.cs` | V1 interface — only implemented by deleted class |
| `IPostProcessor.cs` | Consolidated into ISearchResultMapper |
| `WyomingResultPostProcessor.cs` | Logic moved to WySearchResultMapper |
| `DeEntityDetailsProcessor.cs` | Logic moved to DeDocumentProcessor |

## No Database Changes

This is a pure code refactoring. No schema changes, no new tables, no migration scripts.

## No API Response Changes

All 12 definitions produce byte-for-byte identical API responses after refactoring. This is validated by SC-005 (existing test suite passes unchanged).
