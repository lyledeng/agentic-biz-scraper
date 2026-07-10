# Implementation Plan: Germany Business Entity Search (Handelsregister)

**Branch**: `013-germany-business-search` | **Date**: 2026-03-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/013-germany-business-search/spec.md`

## Summary

Add a new API endpoint `GET /api/v1/germany-search` that scrapes the German Handelsregister (Common Register Portal) for business entity records. The endpoint uses the existing JSON-driven scraping engine with a new `de-business-search.json` flow definition that handles the site's JSF/PrimeFaces form, AJAX-based pagination, and exceeded-hits warning detection. Results return CompanyName, RegisteredOffice, and Status. Screenshots are captured per step and stored in blob storage. A new `GermanySearchHandler` follows the existing vertical-slice pattern with audit trail integration. The Angular test-ui is updated with a Germany search tab.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Azure.Storage.Blobs 12.24.0, System.Text.Json (source-generated)  
**Storage**: Azure SQL Managed Instance (audit trail via ADO.NET), Azure Blob Storage (screenshots/diagnostics), local filesystem (JSON flow definitions bundled as content files)  
**Testing**: xUnit 2.9.3, NSubstitute 5.3.0, Testcontainers.MsSql 4.11.0, Microsoft.AspNetCore.Mvc.Testing 10.0.5  
**Target Platform**: Linux container (Playwright .NET runtime image), AKS deployment via Helm  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Performance Goals**: Full 100-record scrape across 10 AJAX pages in <60 seconds  
**Constraints**: TLS cert errors ignored in dev/local only; strict TLS in staging/production; single concurrent Germany request via semaphore; AJAX pagination (page URL unchanged — must wait for DOM refresh)  
**Scale/Scope**: 1 new JSON flow definition; 1 new feature vertical slice; 1 new Angular component; 1 Liquibase changelog; re-use of existing engine, audit, blob storage, and diagnostics infrastructure

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for handler, endpoint, response mapping; integration tests for audit persistence; E2E tests via k6 against live endpoint — all following Red-Green-Refactor |
| II. SOLID Design | PASS | New feature is a self-contained vertical slice (SRP); reuses existing engine/interfaces (OCP — no engine modification); `IStateSearchScraper` abstraction (DIP); no speculative features (YAGNI) |
| III. Quality Attributes | PASS | Resilience: Polly retry pipeline reused; pagination retries (2 attempts per page); screenshots for supportability; audit trail for auditability; structured logging for observability |
| IV. Versioning | PASS | New Liquibase changelog follows `MAJOR.MINOR.YYMM.PATCH` naming; JSON flow definition uses schemaVersion: 1 |
| V. Deployment | PASS | JSON definition bundled as content file; no manual deployment steps; Helm values unchanged; CI/CD pipelines apply automatically |
| Design Standards | PASS | Options pattern for config; typed exceptions; RFC 9457 Problem Details for errors; OpenAPI documented |
| Quality Gates | PASS | All gates (test, static analysis, security, review, build) apply — no deviations |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/013-germany-business-search/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
└── BizScraper.Api/
    ├── Features/
    │   └── GermanySearch/                          # NEW feature vertical slice
    │       ├── Endpoints/
    │       │   └── GermanySearchEndpoints.cs       # MapGet /api/v1/germany-search
    │       ├── Handlers/
    │       │   └── SearchGermanyBusinessesHandler.cs
    │       ├── Logging/
    │       │   └── GermanySearchLog.cs             # [LoggerMessage] source-generated
    │       ├── Metrics/
    │       │   └── GermanySearchMetrics.cs         # OpenTelemetry counters/histograms
    │       ├── Models/
    │       │   ├── GermanySearchResponse.cs        # Response envelope with warning field
    │       │   └── GermanyBusinessResult.cs        # CompanyName, RegisteredOffice, Status
    │       ├── Queries/
    │       │   └── SearchGermanyBusinessesQuery.cs  # Litebus query record
    │
    ├── Common/
    │   └── Serialization/
    │       └── AppJsonSerializerContext.cs          # MODIFIED: add GermanySearchResponse, GermanyBusinessResult
    │
    ├── Infrastructure/
    │   ├── Scraping/
    │   │   ├── Definitions/
    │   │   │   └── de-business-search.json         # NEW JSON flow definition
    │   │   └── Engine/
    │   │       └── JsonDrivenSearchScraper.cs       # MODIFIED: register DE state code
    │   ├── Persistence/
    │   │   └── AuditTrailRepository.cs              # MODIFIED: add SaveGermanyAuditAsync (or extend existing)
    │   └── Storage/
    │       └── (no changes — reuse existing blob client)
    │
    └── Program.cs                                   # MODIFIED: register GermanySearch endpoints, DI

tests/
├── BizScraper.UnitTests/
│   └── Features/
│       └── GermanySearch/                           # NEW test suite
│           ├── SearchGermanyBusinessesHandlerTests.cs
│           ├── GermanySearchEndpointTests.cs
│           └── GermanySearchResponseMappingTests.cs
│
├── BizScraper.IntegrationTests/
│   └── Features/
│       └── GermanySearch/
│           └── GermanySearchAuditTests.cs
│
└── k6/
    └── germany-search.js                            # NEW k6 scenario

deploy/
└── liquibase/
    ├── masterfiles/
    │   └── master-1.0.2603.0.yaml                   # MODIFIED: add new changelog include
    └── changelog/
        └── ddl/
            └── 2.0.2603.4-add-warning-column-to-search-audit.sql  # NEW

src/test-ui/                                         # MODIFIED: add Germany search component
```

**Structure Decision**: New vertical slice under `Features/GermanySearch/` following the identical pattern to `Features/BusinessSearch/`. The Germany search is a separate endpoint so it gets its own feature folder. The existing JSON-driven scraping engine is reused without modification — only a new flow definition JSON file is added. The `SearchAuditEntries` table is extended with a `WarningMessage` column rather than creating a separate audit table, since the schema is otherwise identical.

## Complexity Tracking

No constitution violations — no complexity justifications needed.
