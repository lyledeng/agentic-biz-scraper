# Implementation Plan: JSON-Driven Scraping Engine

**Branch**: `006-json-scraping-engine` | **Date**: 2026-03-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-json-scraping-engine/spec.md`

## Summary

Refactor the three existing Playwright scraper classes (CO search, WY search, CO entity details) from hard-coded C# action sequences into declarative JSON flow definitions executed by a generic scraping engine. The engine interprets action types (navigate, fill, click, extract, loop, call-service, etc.), supports variables, conditionals, loops, external service calls, and optional C# post-processors. All existing behavior—diagnostics, browser pool, concurrency, error handling—is preserved. As part of this feature, the project is renamed from `CososWebScraping` to `BizScraper` to reflect its multi-state, generic nature.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0  
**Storage**: SQL Server (audit trail via ADO.NET), Azure Blob Storage (diagnostics artifacts), local filesystem (JSON flow definitions bundled as content files)  
**Testing**: xUnit 2.9.3, NSubstitute 5.3.0, Testcontainers.MsSql 4.11.0, Microsoft.AspNetCore.Mvc.Testing 10.0.5  
**Target Platform**: Linux container (Playwright .NET runtime image), AKS deployment via Helm  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Performance Goals**: <10% overhead vs. current hard-coded scrapers for equivalent flows  
**Constraints**: JSON definitions loaded at startup (no hot-reload); single concurrent request per state scraper  
**Scale/Scope**: 3 flow definitions (CO search, WY search, CO entity details); ~11 action types; 1 execution engine class

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Engine, action handlers, post-processors, and flow definitions will be tested at unit/integration/E2E layers following Red-Green-Refactor |
| II. SOLID Design | PASS | Engine follows Open/Closed (new action types without modifying engine), Dependency Inversion (actions depend on abstractions), Interface Segregation (small per-action-type interfaces), Single Responsibility (engine orchestrates, actions execute) |
| III. Quality Attributes | PASS | Existing diagnostics/observability/resilience infrastructure reused; error propagation preserved; JSON validated at startup |
| IV. Versioning | PASS | JSON schema version field (FR-016) ensures forward compatibility |
| V. Deployment | PASS | JSON files bundled as content files; no manual deployment steps added |
| Design Standards | PASS | Options pattern for config; typed errors; public APIs documented via OpenAPI |
| Quality Gates | PASS | All gates (test, static analysis, security, review, build) apply normally |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/006-json-scraping-engine/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
BizScraper.slnx                              # Renamed from CososWebScraping.slnx
Directory.Build.props
Directory.Packages.props
src/
├── bizscraper-dockerfile                     # Renamed from cososwebscraping-dockerfile
└── BizScraper.Api/                           # Renamed from CososWebScraping.Api
    ├── BizScraper.Api.csproj
    ├── Program.cs
    ├── appsettings.json
    ├── Common/
    │   ├── Configuration/
    │   ├── Interfaces/
    │   └── Serialization/
    ├── Domain/
    │   └── Entities/
    ├── Features/
    │   ├── BusinessSearch/
    │   ├── EntityDetails/
    │   └── HealthCheck/
    ├── Infrastructure/
    │   ├── CaptchaSolving/
    │   ├── Persistence/
    │   ├── Scraping/
    │   │   ├── Engine/                       # NEW: Generic scraping engine
    │   │   │   ├── Actions/                  # Action type implementations
    │   │   │   ├── PostProcessors/           # Named C# post-processors
    │   │   │   └── ScrapingFlowEngine.cs     # Core engine
    │   │   ├── Definitions/                  # NEW: JSON flow definition files
    │   │   │   ├── co-business-search.json
    │   │   │   ├── wy-business-search.json
    │   │   │   └── co-entity-details.json
    │   │   ├── PlaywrightBusinessEntityScraper.cs   # Removed after migration
    │   │   ├── PlaywrightWyomingSearchScraper.cs    # Removed after migration
    │   │   └── PlaywrightEntityDetailScraper.cs     # Removed after migration
    │   └── Storage/
    └── Middleware/
tests/
├── BizScraper.UnitTests/                     # Renamed from CososWebScraping.UnitTests
├── BizScraper.IntegrationTests/              # Renamed from CososWebScraping.IntegrationTests
├── BizScraper.EndToEndTests/                 # Renamed from CososWebScraping.EndToEndTests
└── k6/
deploy/
├── helm/
│   └── bizscraper/                           # Renamed from cososwebscraping
├── liquibase/
└── pipelines/
```

**Structure Decision**: Single API project (existing pattern). New code goes into `Infrastructure/Scraping/Engine/` for the execution engine, `Infrastructure/Scraping/Definitions/` for JSON files, and `Infrastructure/Scraping/Engine/PostProcessors/` for C# post-processors. Old scraper classes remain in `Infrastructure/Scraping/` during migration and are removed after verification.

## Complexity Tracking

No constitution violations to justify.

## Post-Design Constitution Re-check

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/, quickstart.md).*

| Principle | Status | Post-Design Notes |
|-----------|--------|-------------------|
| I. Test-First | PASS | Engine, action handlers, post-processors all have clear test boundaries. Unit tests mock IPage; integration tests validate definition loading; E2E tests verify full flow. |
| II. SOLID Design | PASS | SRP (engine orchestrates, handlers execute, post-processors transform), OCP (new action types = new handler class), DIP (engine depends on IActionHandler abstractions), YAGNI (11 types derived from existing code only), KISS (flat action list over state machine). |
| III. Quality Attributes | PASS | Startup validation (reliability), existing Polly pipeline via loop/retry (resilience), operator-only JSON authoring (security), per-action logging FR-013 (supportability). |
| IV. Versioning | PASS | schemaVersion field in definitions. |
| V. Deployment | PASS | JSON files bundled as content files in Docker image. No manual steps. |

**Post-design gate result: PASS** — No violations. No complexity tracking entries needed.
