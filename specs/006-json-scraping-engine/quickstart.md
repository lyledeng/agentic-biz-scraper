# Quickstart: JSON-Driven Scraping Engine

**Feature**: 006-json-scraping-engine  
**Date**: 2026-03-25

## Prerequisites

- .NET 10 SDK
- Playwright browsers installed (`pwsh bin/Debug/net10.0/playwright.ps1 install`)
- Running instance of the Playwright browser endpoint (configured in appsettings)

## Build & Run

```bash
dotnet restore BizScraper.slnx
dotnet build BizScraper.slnx
dotnet run --project src/BizScraper.Api
```

## Run Tests

```bash
# All tests
dotnet test BizScraper.slnx

# Unit tests only
dotnet test tests/BizScraper.UnitTests

# Integration tests only
dotnet test tests/BizScraper.IntegrationTests
```

## Key Files

| Purpose | Path |
|---------|------|
| Execution engine | `src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs` |
| Action handlers | `src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/` |
| Post-processors | `src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/` |
| CO search flow | `src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-business-search.json` |
| WY search flow | `src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-business-search.json` |
| CO details flow | `src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-entity-details.json` |
| Flow schema | `specs/006-json-scraping-engine/contracts/flow-definition-schema.json` |
| Engine unit tests | `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/` |
| Engine integration tests | `tests/BizScraper.IntegrationTests/Infrastructure/Scraping/Engine/` |

## Adding a New Scraping Flow

1. Create a new JSON file in `Infrastructure/Scraping/Definitions/` following the schema in `contracts/flow-definition-schema.json`.
2. Set `schemaVersion` to the current engine version (currently `1`).
3. Define variables with `source: "runtime"` for values injected by the handler (search term, correlation ID, etc.).
4. Add actions in execution order. Use `screenshot` on key milestones for diagnostics.
5. If extraction needs complex C# parsing, create a post-processor class implementing `IPostProcessor<TInput, TOutput>`, register it in DI, and reference its name in the `postProcessor` field of the extract action.
6. Register the flow definition in `Program.cs` DI configuration.
7. Create a handler that loads the definition, passes runtime variables, and calls `ScrapingFlowEngine.ExecuteAsync`.

## Editing an Existing Flow

1. Modify the JSON file (e.g., update a selector, change a URL).
2. Restart the service. The engine validates definitions at startup and reports errors.
3. No recompilation needed — JSON files are content files copied to the output directory.

## Example: Updating a Selector

Before (CO search submit button):
```json
{
  "type": "click",
  "selector": "input[name='cmd'][type='submit'][value='Search']",
  "waitAfter": "networkidle"
}
```

After (if the site changes the button):
```json
{
  "type": "click",
  "selector": "button#searchSubmit",
  "waitAfter": "networkidle"
}
```

Restart the service and the engine uses the new selector immediately.
