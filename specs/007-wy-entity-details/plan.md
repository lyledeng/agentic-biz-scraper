# Implementation Plan: Wyoming Entity Details Support

**Branch**: `007-wy-entity-details` | **Date**: 2026-03-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-wy-entity-details/spec.md`

## Summary

Extend the existing `/api/v1/entity-details` endpoint to support Wyoming (WY) entity details in addition to Colorado (CO). The endpoint gains a required `state` query parameter. A new `wy-entity-details.json` flow definition drives the WY scraping logic through the existing JSON scraping engine, including CAPTCHA resolution via the FAB captcha-solver. The `EntityDetailResult` model is extended with nullable WY-specific fields. New models (`PartyResult`, `HistoryDocumentResult`) capture WY-specific data. The most recent 3 history filing documents are downloaded and uploaded to Azure Blob Storage. The audit table gains a `StateCode` column.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS), `net10.0`
**Primary Dependencies**: Playwright (Azure Playwright Workspaces), Flurl.Http, Polly, System.Text.Json (source-generated), LiteBus
**Storage**: Azure SQL Managed Instance (audit trail), Azure Blob Storage (diagnostics, documents)
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers w/ Azure SQL Edge, Playwright E2E
**Target Platform**: Azure AKS, Kestrel on port 8443 (HTTPS only)
**Project Type**: Web service (ASP.NET Core Minimal API)
**Performance Goals**: WY entity details response in <30 seconds (including CAPTCHA)
**Constraints**: Max 3 concurrent entity detail requests (semaphore), strict URL allowlist validation per state
**Scale/Scope**: 2 states (CO, WY), single endpoint, ~15 new/modified source files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | **PASS** | Unit tests for URL validation, model parsing, handler logic; integration tests for audit persistence; E2E tests for full WY scrape flow |
| II. SOLID Design | **PASS** | Existing IEntityDetailScraper abstraction reused; strategy pattern via JSON flow engine; ISP via per-state flow definitions; DIP via constructor injection |
| III. Architectural Quality | **PASS** | Audit trail for all operations; structured logging with correlation ID; Polly resilience; screenshots+traces for diagnostics |
| IV. Hybrid SemVer+CalVer | **PASS** | Liquibase changelog follows `2.0.2603.3` pattern (next available version) |
| V. Hands-Free Deployment | **PASS** | Liquibase migration with rollback; no manual steps; config via appsettings.json |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/007-wy-entity-details/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Features/
│   └── EntityDetails/
│       ├── Endpoints/EntityDetailsEndpoints.cs     # MODIFY: add required state param
│       ├── Handlers/GetEntityDetailsHandler.cs      # MODIFY: state-aware routing, WY URL validation
│       ├── Queries/GetEntityDetailsQuery.cs         # MODIFY: add State property
│       └── Models/
│           ├── EntityDetailResponse.cs              # MODIFY: add Parties, HistoryDocuments
│           ├── EntityDetailResult.cs                # MODIFY: add WY-specific nullable fields
│           ├── EntityDetailScrapeResult.cs           # MODIFY: add parties, historyDocuments
│           ├── PartyResult.cs                       # NEW
│           └── HistoryDocumentResult.cs             # NEW
├── Infrastructure/
│   └── Scraping/
│       ├── Definitions/
│       │   └── wy-entity-details.json              # NEW: WY flow definition
│       └── Engine/
│           └── JsonDrivenEntityDetailScraper.cs     # MODIFY: state-parameterized
├── Domain/
│   └── Entities/
│       └── EntityDetailAuditEntry.cs                # MODIFY: add StateCode column
└── Program.cs                                       # MODIFY: DI keyed by state

deploy/liquibase/
├── changelog/ddl/
│   └── 2.0.2603.3-add-state-column-to-entity-detail-audit.sql  # NEW
└── masterfiles/
    └── master-2.0.2603.0.yaml                       # MODIFY: add new changelog

tests/
├── BizScraper.UnitTests/Features/EntityDetails/     # NEW/MODIFY: WY tests
└── BizScraper.IntegrationTests/Features/EntityDetails/ # NEW/MODIFY: WY audit tests
```

**Structure Decision**: Extends the existing vertical slice under `Features/EntityDetails/` and `Infrastructure/Scraping/`. No new projects needed. The JSON-driven engine already supports multiple flow definitions.

## Complexity Tracking

No constitution violations — table not applicable.
