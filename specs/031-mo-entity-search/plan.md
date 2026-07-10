# Implementation Plan: Missouri Business Entity Search Integration

**Branch**: `031-mo-entity-search` | **Date**: 2026-04-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/031-mo-entity-search/spec.md`

## Summary

Add Missouri (MO) business entity search and detail capabilities to BizScraper via two new v2 execute-script definitions (`us-mo-business-search`, `us-mo-entity-details`). The MO SOS site at `https://bsd.sos.mo.gov/BusinessEntity/BESearch.aspx?SearchType=0` is an ASP.NET Web Forms application requiring Playwright browser automation for postback-based form submission and pagination. Search results include business name, charter number, type, status, creation date, registered agent, and previous names. Entity details span three tabs (General Information, Filings, Principal Office Address) with filing PDFs downloaded to Azure Blob Storage and served via the existing proxy URL pattern. No anti-bot measures detected; remote Azure Playwright Workspaces will be used.

## Technical Context

**Language/Version**: .NET 10 (LTS) with C# 14, `net10.0` target  
**Primary Dependencies**: Playwright (browser automation), Flurl.Http (outbound HTTP), Polly (resilience), LiteBus (CQRS), Azure.Storage.Blobs, System.Text.Json (source-generated)  
**Storage**: Azure Blob Storage (filing PDFs), Azure SQL MI (audit trail)  
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers (Azure SQL Edge), k6 (performance)  
**Target Platform**: Azure AKS (Linux containers)  
**Project Type**: Web service (ASP.NET Minimal API)  
**Performance Goals**: Search <15s, Entity detail <10s per spec SC-001/SC-002  
**Constraints**: Remote Playwright Workspaces (no anti-bot), per-definition browser config (feature 030)  
**Scale/Scope**: Adding 2 JSON definitions + Test UI state entry; no new C# projects, endpoints, or DB schema changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for extraction scripts, integration tests for definition loading, e2e tests for search+detail flow |
| II. SOLID Design | PASS | Follows existing vertical-slice pattern; no new abstractions beyond JSON definitions |
| III. Architectural Quality | PASS | Resilience via Polly, diagnostics via correlation ID, per-document error handling |
| IV. Hybrid SemVer+CalVer | PASS | No version-impacting changes (definition-only addition) |
| V. Hands-Free Deployment | PASS | CI/CD already handles definition JSON files in Docker image |
| VI. Documentation | PASS | JSON definition self-documents via `description` fields; API contracts via contracts/ |
| Design Standards: API versioned | PASS | Uses existing v2 execute-script endpoint |
| Design Standards: Global Snapshot | PASS | `specs/current-specs/` to be updated with MO additions |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/031-mo-entity-search/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── us-mo-business-search.json
│   └── us-mo-entity-details.json
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── BizScraper.Api/
│   └── Infrastructure/
│       └── Scraping/
│           └── Definitions/
│               ├── us-mo-business-search.json   # NEW — search definition
│               └── us-mo-entity-details.json    # NEW — detail definition
└── test-ui/
    └── src/app/features/
        └── search/
            └── search.component.ts              # MODIFIED — add MO state

tests/
├── BizScraper.UnitTests/                        # NEW — extraction script tests
└── BizScraper.IntegrationTests/                 # NEW — definition loading tests
```

**Structure Decision**: No new C# projects, endpoints, or database schema changes. This feature follows the established JSON-driven definition pattern introduced in feature 014 and refined in features 018/022. New content is:
1. Two JSON definition files in `Infrastructure/Scraping/Definitions/`
2. Test UI state map entries for MO
3. Tests validating the new definitions
