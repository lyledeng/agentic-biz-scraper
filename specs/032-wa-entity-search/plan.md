# Implementation Plan: Washington Business Entity Search Integration

**Branch**: `032-wa-entity-search` | **Date**: 2026-04-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/032-wa-entity-search/spec.md`

## Summary

Add Washington (WA) business entity search and detail capabilities to BizScraper via two new v2 execute-script definitions (`us-wa-business-search`, `us-wa-entity-details`). The WA CCFS site at `https://ccfs.sos.wa.gov/#/` is an AngularJS single-page application backed by a REST API at `ccfs-api.prod.sos.wa.gov`. Cloudflare Turnstile bot protection is present. The scraping definitions will interact with the SPA UI elements for search and navigation, then use page-level `fetch()` for filing document list retrieval and document downloads (preserving session cookies). Search results include business name, UBI number, business type, principal office address, registered agent name, status, and unique key. Entity details span general information, registered agent, governors/officers, filing history with document downloads, and nonprofit-specific fields. Remote Azure Playwright Workspaces will be used. No new C# projects, endpoints, or DB schema changes; follows the established JSON-driven definition pattern.

## Technical Context

**Language/Version**: .NET 10 (LTS) with C# 14, `net10.0` target  
**Primary Dependencies**: Playwright (browser automation), Flurl.Http (outbound HTTP), Polly (resilience), LiteBus (CQRS), Azure.Storage.Blobs, System.Text.Json (source-generated)  
**Storage**: Azure Blob Storage (filing PDFs), Azure SQL MI (audit trail)  
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers (Azure SQL Edge), k6 (performance)  
**Target Platform**: Azure AKS (Linux containers)  
**Project Type**: Web service (ASP.NET Minimal API)  
**Performance Goals**: Search <15s, Entity detail <30s per spec SC-001/SC-002  
**Constraints**: Remote Playwright Workspaces, Cloudflare Turnstile (stealth config), page-level fetch() for document APIs  
**Scale/Scope**: Adding 2 JSON definitions + Test UI state entry; no new C# projects, endpoints, or DB schema changes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for extraction scripts, integration tests for definition loading, e2e tests for search+detail flow |
| II. SOLID Design | PASS | Follows existing vertical-slice pattern; no new abstractions beyond JSON definitions |
| III. Architectural Quality | PASS | Resilience via Polly, diagnostics via correlation ID, per-document error handling, Cloudflare Turnstile edge case handled |
| IV. Hybrid SemVer+CalVer | PASS | No version-impacting changes (definition-only addition) |
| V. Hands-Free Deployment | PASS | CI/CD already handles definition JSON files in Docker image |
| VI. Documentation | PASS | JSON definition self-documents via `description` fields; API contracts via contracts/; quickstart.md |
| Design Standards: API versioned | PASS | Uses existing v2 execute-script endpoint |
| Design Standards: Global Snapshot | PASS | `specs/current-specs/` to be updated with WA additions |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/032-wa-entity-search/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── us-wa-business-search.json
│   └── us-wa-entity-details.json
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── BizScraper.Api/
│   └── Infrastructure/
│       └── Scraping/
│           └── Definitions/
│               ├── us-wa-business-search.json   # NEW — search definition
│               └── us-wa-entity-details.json    # NEW — detail definition
└── test-ui/
    └── src/app/features/
        └── search/
            └── search.component.ts              # MODIFIED — add WA state

tests/
├── BizScraper.UnitTests/                        # NEW — extraction script tests
└── BizScraper.IntegrationTests/                 # NEW — definition loading tests
```

**Structure Decision**: No new C# projects, endpoints, or database schema changes. This feature follows the established JSON-driven definition pattern introduced in feature 014 and refined in features 018/022/031. New content is:
1. Two JSON definition files in `Infrastructure/Scraping/Definitions/`
2. Test UI state map entries for WA
3. Tests validating the new definitions

## Constitution Re-Check (Post-Design)

*Re-evaluation after Phase 1 design artifacts are finalized.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Data model defines clear record types testable via unit tests; extraction scripts testable with mock HTML; integration tests for definition loading |
| II. SOLID Design | PASS | No new abstractions — WA follows identical definition pattern as CO/WY/IA/MO; data model uses composition (details + agent + governors + filings) |
| III. Architectural Quality | PASS | Per-document error handling in filing downloads; Cloudflare Turnstile detection; $N=10 configurable filing document cap; page-level fetch() preserves session cookies |
| IV. Hybrid SemVer+CalVer | PASS | No version-impacting changes |
| V. Hands-Free Deployment | PASS | JSON definitions auto-deployed via CI/CD Docker image build |
| VI. Documentation | PASS | quickstart.md with examples; contract JSON schemas; data-model.md with field mappings; JSON definition `description` fields |
| Design Standards: API versioned | PASS | Existing v2 endpoint; WA definitions registered in `/api/v2/definitions` |
| Design Standards: Global Snapshot | PASS | `specs/current-specs/` update planned in tasks phase |

**Post-Design Gate Result**: PASS — No violations introduced by Phase 1 design.

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Remote browser mode (Azure Playwright Workspaces) | Azure IPs reduce Cloudflare fingerprinting; consistent with MO approach for SPA sites |
| Page-level `fetch()` for document download | Preserves session cookies; avoids fragile modal dialog interaction; matches MO filing download pattern |
| `uniqueKey` encodes both `businessId` and `businessType` | `showBusineInfo()` requires both parameters; detail page API needs businessType |
| Nonprofit fields as nullable booleans/strings | Only present on nonprofit entities; null for all others |
| 25-result pagination, `maxPages` = 25 | CCFS returns 25 per page; 625-result cap is reasonable default |
| N=10 configurable filing document download limit | Balances thoroughness vs. execution time; most recent filings are highest value |
| Cloudflare Turnstile check-text action | Early detection prevents wasted execution if bot-blocked |
| AngularJS wait-for-condition strategy | SPA renders client-side; `networkidle` insufficient for digest cycles |
