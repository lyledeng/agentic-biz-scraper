# Implementation Plan: Unified Entity Schema

**Branch**: `018-unified-entity-schema` | **Date**: 2026-03-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/018-unified-entity-schema/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Standardize the business entity search and detail response shapes across all supported jurisdictions (US-CO, US-WY, DE-DE). Replace `detailsUrl` with an opaque `uniqueKey`, rename definition slugs to `{country}-{state}-{action}` format, remove all deprecated v1 endpoints (business-search, entity-details, germany-search, hardcopy-printout), introduce a `/api/v2` prefix for execute-script and definitions, create a new `de-de-entity-details` definition for German hardcopy document download, and update the test UI to consume all unified shapes.

## Technical Context

**Language/Version**: .NET 10 (LTS) with C# 14, `net10.0` target framework, language version `latest`
**Primary Dependencies**: ASP.NET Core Minimal API, LiteBus (CQRS), Playwright (browser automation), Flurl.Http (outbound HTTP), Polly (resilience), System.Text.Json (STJ source generation)
**Storage**: Azure Blob Storage (documents), Azure SQL Managed Instance (audit trail), Liquibase (schema migrations)
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers + Azure SQL Edge (integration), k6 (performance), Playwright (E2E)
**Target Platform**: Azure AKS (Linux containers), Alpine-based images
**Project Type**: Web service (scraping API) + Angular SPA (test UI)
**Performance Goals**: Per-definition concurrency semaphores, 503 with Retry-After when at capacity
**Constraints**: Non-root containers, read-only filesystem, HTTPS-only (self-signed TLS), rate limiting middleware
**Scale/Scope**: 6 scraping definitions, 3 API routes (v2 execute-script, v2 definitions, v1 documents), 1 Angular test UI

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for new models, integration tests for endpoint routing (410, 404), E2E tests for definition execution |
| II. SOLID Design | PASS | New unified models follow SRP; existing vertical slice architecture extended, not replaced; composition via shared model records |
| III. Quality Attributes | PASS | Resilience patterns (Polly) preserved; audit trail maintained; observability via OpenTelemetry unchanged; partial-success pattern for DE translation failures |
| IV. Hybrid SemVer+CalVer | PASS | API version bump to v2 (`2.0.2603.0`); definition schema version unchanged |
| V. Hands-Free Deployment | PASS | No new infra provisioning; Helm charts updated for v2 routes; Liquibase changelogs if audit trail schema changes |
| VI. Documentation | PASS | OpenAPI spec updated for v2 endpoints; JSON definition metadata updated; code doc-comments on all new public records |
| Design Standards | PASS | Global spec snapshot updated in `specs/current-specs/` |
| Quality Gates | PASS | All gates apply: test, static analysis, security, review, build |

**Gate result**: PASS — no violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/018-unified-entity-schema/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── execute-script-v2-api.md
│   └── unified-search-result-schema.md
└── tasks.md             # Phase 2 output (not created by this command)
```

### Source Code (repository root)

```text
src/
├── BizScraper.Api/
│   ├── Features/
│   │   ├── BusinessSearch/         # REMOVE: deprecated endpoint code
│   │   │   ├── Endpoints/          # DELETE
│   │   │   ├── Handlers/           # DELETE
│   │   │   ├── Queries/            # DELETE
│   │   │   └── Models/             # MODIFY: NormalizedSearchResult → UnifiedSearchResult (shared)
│   │   │
│   │   ├── EntityDetails/          # REMOVE: deprecated endpoint code
│   │   │   ├── Endpoints/          # DELETE
│   │   │   ├── Handlers/           # DELETE
│   │   │   ├── Queries/            # DELETE
│   │   │   └── Models/             # MODIFY: EntityDetailResponse → UnifiedEntityDetailResponse (shared)
│   │   │
│   │   ├── GermanySearch/          # DELETE entirely
│   │   │
│   │   ├── ExecuteScript/          # MODIFY: v2 route, new handler for de-de-entity-details
│   │   │   ├── Endpoints/          # UPDATE: v2 prefix, 410 for v1
│   │   │   ├── Handlers/           # UPDATE: produce unified shapes
│   │   │   └── Models/             # UPDATE: unified response models
│   │   │
│   │   ├── Documents/              # UNCHANGED (stays at /api/v1)
│   │   └── HealthCheck/            # UNCHANGED
│   │
│   ├── Common/
│   │   └── Models/                 # NEW: shared unified models (UnifiedSearchResult, UnifiedEntityDetailResponse, DocumentEntry, DownloadReference)
│   │
│   └── Infrastructure/
│       └── Scraping/
│           └── Definitions/        # RENAME: definition slugs + NEW: de-de-entity-details.json
│               ├── us-co-business-search.json     (was co-business-search.json)
│               ├── us-co-entity-details.json      (was co-entity-details.json)
│               ├── us-wy-business-search.json     (was wy-business-search.json)
│               ├── us-wy-entity-details.json      (was wy-entity-details.json)
│               ├── de-de-business-search.json     (was de-business-search.json)
│               └── de-de-entity-details.json      (NEW)
│
├── test-ui/
│   └── src/
│       ├── app/
│       │   ├── models/             # UPDATE: unified interfaces
│       │   ├── services/           # UPDATE: v2 API base, uniqueKey
│       │   └── components/         # UPDATE: results grid, detail view, documents section

tests/
├── BizScraper.UnitTests/          # UPDATE: new model tests, remove deprecated handler tests
├── BizScraper.IntegrationTests/   # UPDATE: v2 endpoint tests, 410/404 tests
└── BizScraper.EndToEndTests/      # UPDATE: unified shapes validation
```

**Structure Decision**: Extend existing vertical slice architecture. New unified models placed in `Common/Models/` since they are shared across the ExecuteScript feature and may be referenced by multiple handlers. Deprecated feature folders (BusinessSearch/Endpoints, EntityDetails/Endpoints, GermanySearch) are deleted entirely.

## Complexity Tracking

No constitution violations — table not needed.
