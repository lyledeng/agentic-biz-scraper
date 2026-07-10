# Implementation Plan: Extract Site-Specific Code from Generic Scraping Engine

**Branch**: `038-engine-site-code-extraction` | **Date**: 2026-04-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/038-engine-site-code-extraction/spec.md`

## Summary

Audit and refactor the BizScraper generic scraping engine to remove all site-specific code from generic components. 20 violations were identified across 8 files, concentrated in `GenericScriptScraper.cs` (4 post-flow document processors), `SearchResultMapper.cs` / `EntityDetailMapper.cs` (12 state-specific branches), `PlaywrightOptions.cs` (3 hardcoded state URLs), and `Program.cs` (manual factory registrations). The approach: extract into per-state classes behind `IPostFlowDocumentProcessor`, `ISearchResultMapper`, and `IEntityDetailMapper` interfaces, resolved by convention from definition slug prefix. Delete deprecated v1 scrapers (`JsonDrivenSearchScraper`, `JsonDrivenEntityDetailScraper`). Consolidate `IPostProcessor` into mapper. Unify `DeEntityDetailsProcessor` as `IPostFlowDocumentProcessor`.

## Technical Context

**Language/Version**: .NET 10, C# 14  
**Primary Dependencies**: Playwright (browser automation), Azure Blob Storage SDK, System.Text.Json source generation  
**Storage**: Azure Blob Storage (document PDFs), Azure SQL MI (audit trail)  
**Testing**: xUnit + NSubstitute (unit), Testcontainers (integration)  
**Target Platform**: AKS (Linux containers) + Windows VM (headed proxy)  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Constraints**: Zero behavioural regression — byte-for-byte identical API responses for all 12 definitions  
**Scale/Scope**: 8 files with violations, 6 states (CO, WY, IA, MO, WA, DE), ~20 new per-state classes, 2 v1 scrapers deleted

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|------|--------|-------|
| I. Test-First | PASS | Unit tests for every new interface, per-state implementation, registry, and DI wiring. Existing 326+ unit + 45 integration tests must continue to pass unchanged. |
| II. SOLID Design | PASS | This feature **is** the SOLID fix. Extracts SRP violations, adds OCP-compliant abstractions, removes DIP violations. |
| III. Quality Attributes | PASS | Error isolation per document processor (log + skip on failure). Existing observability (correlation ID, action logging) preserved. |
| IV. Versioning | N/A | Internal refactor — no schema version bump. API response shape unchanged. |
| V. Hands-Free Deployment | PASS | No infrastructure changes. Existing CI/CD pipeline handles code changes. |
| VI. Documentation | PASS | Each new interface and per-state class carries XML doc-comments. Quickstart updated. |
| Global Spec Snapshot | WILL UPDATE | current-specs/ updated post-implementation with refactored data model. |

**Post-Design Re-evaluation** (after Phase 1): All gates continue to PASS. The design introduces ~20 new per-state classes and 3 new interfaces, which adds file count but each class has a single responsibility (SOLID II). The assembly scanning DI pattern is the simplest discovery mechanism (KISS). No speculative abstractions (YAGNI) — every interface has concrete implementations. Error isolation per document processor satisfies resilience (III). No gate violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/038-engine-site-code-extraction/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (interface contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (files to create and modify)

```text
src/BizScraper.Api/
├── Infrastructure/Scraping/Engine/
│   ├── GenericScriptScraper.cs                    # MODIFY: Remove 4 state-specific methods + URL injection
│   ├── JsonDrivenSearchScraper.cs                 # DELETE
│   ├── JsonDrivenEntityDetailScraper.cs           # DELETE
│   ├── PostProcessors/
│   │   ├── IPostProcessor.cs                      # DELETE
│   │   └── WyomingResultPostProcessor.cs          # DELETE
│   └── DocumentProcessors/                        # NEW folder
│       ├── IPostFlowDocumentProcessor.cs          # NEW: Abstraction
│       ├── PostFlowDocumentProcessorRegistry.cs   # NEW: Convention-based resolver
│       ├── WyDocumentProcessor.cs                 # NEW: Extracted from GenericScriptScraper
│       ├── MoDocumentProcessor.cs                 # NEW: Extracted from GenericScriptScraper
│       ├── WaDocumentProcessor.cs                 # NEW: Extracted from GenericScriptScraper
│       ├── CoDocumentProcessor.cs                 # NEW: Extracted from GenericScriptScraper
│       └── DeDocumentProcessor.cs                 # NEW: Refactored from DeEntityDetailsProcessor
├── Features/ExecuteScript/
│   ├── Handlers/
│   │   ├── ExecuteScriptHandler.cs                # MODIFY: Remove GetExpectedStatePrefix(), DE branching
│   │   └── DeEntityDetailsProcessor.cs            # DELETE (moved to DocumentProcessors/)
│   └── Mappers/
│       ├── SearchResultMapper.cs                  # MODIFY: Delegate to per-state mappers
│       ├── EntityDetailMapper.cs                  # MODIFY: Delegate to per-state mappers
│       ├── ISearchResultMapper.cs                 # NEW: Abstraction
│       ├── IEntityDetailMapper.cs                 # NEW: Abstraction
│       ├── MapperRegistry.cs                      # NEW: Convention-based resolver
│       ├── Search/                                # NEW folder
│       │   ├── CoSearchResultMapper.cs            # NEW
│       │   ├── WySearchResultMapper.cs            # NEW (includes WyomingResultPostProcessor logic)
│       │   ├── IaSearchResultMapper.cs            # NEW
│       │   ├── MoSearchResultMapper.cs            # NEW
│       │   ├── WaSearchResultMapper.cs            # NEW
│       │   └── DeSearchResultMapper.cs            # NEW
│       └── Details/                               # NEW folder
│           ├── CoEntityDetailMapper.cs            # NEW
│           ├── WyEntityDetailMapper.cs            # NEW
│           ├── IaEntityDetailMapper.cs            # NEW
│           ├── MoEntityDetailMapper.cs            # NEW
│           ├── WaEntityDetailMapper.cs            # NEW
│           └── DeEntityDetailMapper.cs            # NEW
├── Common/Configuration/
│   └── PlaywrightOptions.cs                       # MODIFY: Remove WyomingSearchUrl, IowaSearchUrl, GermanySearchUrl
└── Program.cs                                     # MODIFY: Replace manual registrations with DI scan

src/BizScraper.Api/Infrastructure/Scraping/Definitions/
├── us-wy-business-search.json                     # MODIFY: wyomingSearchUrl → searchEntryUrl
├── us-ia-business-search.json                     # MODIFY: iowaSearchUrl → searchEntryUrl
└── de-de-business-search.json                     # MODIFY: germanySearchUrl → searchEntryUrl

tests/BizScraper.UnitTests/
├── Infrastructure/Scraping/Engine/DocumentProcessors/  # NEW folder
│   ├── WyDocumentProcessorTests.cs                # NEW
│   ├── MoDocumentProcessorTests.cs                # NEW
│   ├── WaDocumentProcessorTests.cs                # NEW
│   ├── CoDocumentProcessorTests.cs                # NEW
│   ├── DeDocumentProcessorTests.cs                # NEW
│   └── PostFlowDocumentProcessorRegistryTests.cs  # NEW
└── Features/ExecuteScript/Mappers/
    ├── Search/                                    # NEW folder (per-state mapper tests)
    ├── Details/                                   # NEW folder (per-state mapper tests)
    └── MapperRegistryTests.cs                     # NEW
```

**Structure Decision**: Per-state classes organized under `DocumentProcessors/`, `Mappers/Search/`, and `Mappers/Details/` folders following the existing project convention of grouping by concern. No new projects — all changes within `BizScraper.Api` and `BizScraper.UnitTests`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| ~20 new per-state classes | Each state has unique field mapping, document processing, URL patterns | A single generic mapper cannot handle per-state field differences without conditional branching (which is the current problem). Per-state classes are the OCP-compliant solution. |
| Registry + DI scan pattern | Convention-based resolution avoids manual registration per state | Direct DI keyed registration (current approach) requires `Program.cs` modification for each new state — the exact problem being fixed. |
