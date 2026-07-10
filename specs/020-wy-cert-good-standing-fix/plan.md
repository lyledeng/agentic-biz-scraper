# Implementation Plan: WY Certificate of Good Standing Fix

**Branch**: `020-wy-cert-good-standing-fix` | **Date**: 2026-03-31 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/020-wy-cert-good-standing-fix/spec.md`

## Summary

Fix the WY entity details certificate flow in `us-wy-entity-details.json` to correctly download the Certificate of Good Standing for eligible entities, report ineligibility for non-good-standing entities, and remove the remaining deprecated v1 API endpoints with full test migration to v2. The approach: add `element-exists` condition on the download action (matching CO pattern), add check-text detection for the ineligibility alert, fix duplicate action ordering, remove the v1 business-search endpoint (v1 entity-details already removed in spec 018), migrate all v1 tests to v2, and update current-specs.

## Technical Context

**Language/Version**: .NET 10 / C# 14  
**Primary Dependencies**: Playwright, Flurl.Http, Polly, LiteBus, System.Text.Json  
**Storage**: Azure Blob Storage (certificate PDFs), SQLite (audit trail)  
**Testing**: xUnit + NSubstitute, Testcontainers, k6  
**Target Platform**: Azure AKS (Linux containers)  
**Project Type**: Web service (REST API)  
**Performance Goals**: Certificate flow ≤ 15s timeout, entity details p95 < 30s  
**Constraints**: Certificate flow must not block entity details retrieval  
**Scale/Scope**: 3 state entity-details definitions (CO, WY, DE), 3 business-search definitions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Tests written before implementation: download condition unit tests, ineligibility detection tests, v2 migration tests |
| II. SOLID Design | ✅ PASS | Changes are in JSON definition (Open/Closed: engine unchanged); reuses existing `element-exists` condition pattern |
| III. Quality Attributes | ✅ PASS | Resilience: certificate errors don't block entity details; Observability: screenshots at verification page; Supportability: explicit error messages |
| IV. Versioning | ✅ PASS | No version bump needed — bug fix in JSON definition |
| V. Hands-Free Deployment | ✅ PASS | No infra changes; JSON definition deployed with existing Docker image |
| VI. Documentation | ✅ PASS | Update current-specs snapshot, update deprecated-entity-details contract to mark as fully removed |

**Post-Design Re-Check**:

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Certificate condition unit tests, mapper unit tests, v2 migration integration tests — all Red-first |
| II. SOLID Design | ✅ PASS | Open/Closed: engine unchanged; mapper pattern mirrors CO; no new abstractions |
| III. Quality Attributes | ✅ PASS | New finding: mapper bug means v2 never returned certificate for WY (R7). Fix surfaces certificate in response. Resilience preserved. |
| IV. Versioning | ✅ PASS | No version bump needed |
| V. Hands-Free Deployment | ✅ PASS | No infra changes |
| VI. Documentation | ✅ PASS | Current-specs update, contract doc, quickstart doc |

**Gate Result**: PASS — proceed to Phase 2 (tasks).

**Critical Additional Finding During Design (R7)**: The `EntityDetailMapper.MapWyDetails()` method does NOT map the `certificateUrl` from the composed output to the response `Certificate` section. This means the v2 response has always returned `certificate: null` for WY, regardless of whether the download succeeded. This mapper bug must be fixed alongside the JSON definition fix.

## Project Structure

### Documentation (this feature)

```text
specs/020-wy-cert-good-standing-fix/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (files affected)

```text
src/BizScraper.Api/
├── Infrastructure/Scraping/Definitions/
│   └── us-wy-entity-details.json        # Certificate flow fix (FR-001–FR-009)
├── Features/BusinessSearch/
│   ├── Endpoints/BusinessSearchEndpoints.cs  # REMOVE (FR-010)
│   ├── Exceptions/                           # REMOVE if orphaned
│   ├── Logging/                              # RETAIN if used by v2 search flow
│   ├── Metrics/                              # RETAIN if used by v2 search flow
│   └── Models/                               # RETAIN (used by v2 execute-script)
├── Features/EntityDetails/                   # RETAIN all (used by v2 flow)
├── Features/ExecuteScript/
│   └── Endpoints/ExecuteScriptEndpoints.cs   # Remove v1 business-search tombstone if present
└── Program.cs                                # Remove MapBusinessSearchEndpoints() call

tests/
├── BizScraper.IntegrationTests/
│   └── Features/BusinessSearch/
│       ├── BusinessSearchEndpointTests.cs    # Migrate to v2
│       ├── MultiStateEndpointTests.cs        # Migrate to v2
│       ├── ErrorResponseContractTests.cs     # Migrate to v2
│       └── OpenApiContractTests.cs           # Migrate to v2
├── BizScraper.UnitTests/                     # Add certificate condition tests
└── k6/
    └── business-search.js                    # Migrate to v2
```

## Complexity Tracking

No constitution violations. All changes follow existing patterns.
