# Quickstart: Extract Site-Specific Code from Generic Scraping Engine

**Feature**: 038-engine-site-code-extraction

## Prerequisites

- .NET 10 SDK
- Existing BizScraper.Api project building and tests passing
- All 12 JSON definitions in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/`

## No New Configuration

This is a pure internal refactoring. No new configuration keys, environment variables, or infrastructure changes are required. All existing `PlaywrightOptions` configuration continues to work (minus the removed state-specific URL properties which move to definition variables).

### Configuration Removed

The following `appsettings.json` / environment variable keys become unused after refactoring:
- `Playwright:WyomingSearchUrl` → now in `us-wy-business-search.json` `searchEntryUrl` defaultValue
- `Playwright:IowaSearchUrl` → now in `us-ia-business-search.json` `searchEntryUrl` defaultValue
- `Playwright:GermanySearchUrl` → now in `de-de-business-search.json` `searchEntryUrl` defaultValue

These can be removed from appsettings/environment but will be silently ignored if present.

## Run the API

```bash
cd src/BizScraper.Api
dotnet run
```

The API starts on `https://localhost:8443`. All existing endpoints behave identically.

## Verify Refactoring

### Run Full Test Suite

```bash
# Unit tests (326+)
dotnet test tests/BizScraper.UnitTests/

# Integration tests (45)
dotnet test tests/BizScraper.IntegrationTests/
```

All tests must pass with zero modifications to test assertions. This validates SC-005.

### Verify No State-Specific Code in Engine

After refactoring, these files should contain zero state identifiers:

```bash
# Should return NO matches:
grep -r "us-co\|us-wy\|us-ia\|us-mo\|us-wa\|de-de\|Wyoming\|Colorado\|Iowa\|Missouri\|Washington\|Germany" \
  src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
```

### Test Each State End-to-End

```bash
# CO business search
curl -k -X POST "https://localhost:8443/api/v2/execute-script" \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-co-business-search", "parameters": {"searchTerm": "test"}}'

# WY entity details
curl -k -X POST "https://localhost:8443/api/v2/execute-script" \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-wy-entity-details", "parameters": {"detailsUrl": "..."}}'

# Repeat for IA, MO, WA, DE definitions
```

All responses must be identical to pre-refactoring output.

## New Extension Point

To add support for a new state (e.g., Texas):

1. Create JSON definition files: `us-tx-business-search.json`, `us-tx-entity-details.json`
2. Optionally add `TxSearchResultMapper : ISearchResultMapper` (if response needs custom mapping)
3. Optionally add `TxEntityDetailMapper : IEntityDetailMapper` (if details need custom mapping)
4. Optionally add `TxDocumentProcessor : IPostFlowDocumentProcessor` (if post-flow document handling needed)
5. **No changes to any existing file** — DI auto-discovery registers new implementations at startup.

## Files Deleted (verify absence)

```bash
# These files should no longer exist after refactoring:
ls src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenSearchScraper.cs      # DELETED
ls src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs # DELETED
ls src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/IPostProcessor.cs # DELETED
ls src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/WyomingResultPostProcessor.cs # DELETED
ls src/BizScraper.Api/Features/ExecuteScript/Handlers/DeEntityDetailsProcessor.cs     # DELETED
```
