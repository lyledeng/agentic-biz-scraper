# Implementation Plan: Entity Details Retrieval & Certificate of Good Standing Download

**Branch**: `004-entity-details-endpoint` | **Date**: 2026-03-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-entity-details-endpoint/spec.md`
**User directive**: Follow current conventions in the current code. Add rollback capabilities to Liquibase changelogs.

## Summary

New `GET /api/v1/entity-details?detailsUrl={url}` endpoint that navigates to a CO SOS entity detail page via Playwright, scrapes structured data from the "Details" and "Registered Agent" tables, optionally downloads the "Certificate of Good Standing" PDF when available, uploads it to Azure Blob Storage, records an audit trail entry in a dedicated table with the certificate URL, and returns all data as JSON. Follows existing vertical-slice CQRS architecture with shared Playwright browser pool, diagnostics infrastructure, and blob storage client.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (net10.0, LangVersion latest)
**Primary Dependencies**: Microsoft.Playwright 1.58.0, LiteBus 4.3.0 (CQRS), Azure.Storage.Blobs 12.24.0, Polly 8.6.6, Microsoft.Data.SqlClient 7.0.0, Swashbuckle 7.3.1
**Storage**: Azure SQL Managed Instance (audit trail), Azure Blob Storage (diagnostics + certificates)
**Testing**: xUnit 2.9.3 + NSubstitute 5.3.0 (unit/integration), Testcontainers.MsSql 4.11.0 (integration), Microsoft.Playwright (E2E)
**Target Platform**: Linux containers on Azure AKS, HTTPS-only on port 8443
**Project Type**: Web service (ASP.NET Core Minimal API)
**Performance Goals**: Entity detail retrieval < 30s, certificate download + upload < 45s (SC-001, SC-002)
**Constraints**: Configurable concurrent entity detail requests (default 3), browser pool shared with search
**Scale/Scope**: Single API project, new vertical slice feature alongside existing BusinessSearch feature

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First (NON-NEGOTIABLE) | PASS | Unit tests for handler, URL validator, scraper; integration tests for audit repository + endpoint; E2E tests for live entity detail retrieval. All test layers planned. |
| II. SOLID Design | PASS | New `IEntityDetailScraper` interface (DIP), dedicated handler class (SRP), reuses existing `IBlobStorageClient` (ISP), separate models per feature (OCP). |
| III. Architectural Quality Attributes | PASS | SSRF-prevention URL validation (Security), retry pipeline for uploads (Resilience), structured logging via `[LoggerMessage]` (Supportability), dedicated audit table (Auditability), OpenTelemetry metrics (Observability). |
| IV. Hybrid SemVer+CalVer | PASS | Liquibase changelogs use `1.0.2603.2` (next PATCH in current YYMM window). |
| V. Hands-Free Deployment & Rollback | PASS | Liquibase changelogs include rollback blocks. No manual deployment steps. |

## Project Structure

### Documentation (this feature)

```text
specs/004-entity-details-endpoint/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── openapi.yaml
│   └── internal-contracts.md
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/CososWebScraping.Api/
├── Features/
│   └── EntityDetails/
│       ├── Endpoints/
│       │   └── EntityDetailsEndpoints.cs
│       ├── Handlers/
│       │   └── GetEntityDetailsHandler.cs
│       ├── Queries/
│       │   └── GetEntityDetailsQuery.cs
│       ├── Models/
│       │   ├── EntityDetailRequest.cs
│       │   ├── EntityDetailResponse.cs
│       │   ├── EntityDetailResult.cs
│       │   ├── RegisteredAgentResult.cs
│       │   ├── CertificateResult.cs
│       │   └── EntityDetailScrapeResult.cs
│       ├── Exceptions/
│       │   └── InvalidDetailsUrlException.cs
│       ├── Logging/
│       │   └── EntityDetailsLogMessages.cs
│       └── Metrics/
│           └── EntityDetailsMetrics.cs
├── Common/
│   └── Interfaces/
│       └── IEntityDetailScraper.cs       # New scraper interface
├── Domain/
│   └── Entities/
│       └── EntityDetailAuditEntry.cs     # New audit entity
├── Infrastructure/
│   ├── Scraping/
│   │   └── PlaywrightEntityDetailScraper.cs  # New scraper implementation
│   └── Persistence/
│       └── EntityDetailAuditRepository.cs    # New audit repository

tests/CososWebScraping.UnitTests/
└── Features/
    └── EntityDetails/
        ├── GetEntityDetailsHandlerTests.cs
        ├── EntityDetailRequestValidationTests.cs
        ├── UrlAllowlistValidationTests.cs
        └── EntityDetailScrapingTests.cs

tests/CososWebScraping.IntegrationTests/
├── Features/
│   └── EntityDetails/
│       ├── EntityDetailsEndpointTests.cs
│       └── EntityDetailAuditRepositoryTests.cs
└── Infrastructure/
    └── (existing BlobStorageIntegrationTests.cs — reused)

tests/CososWebScraping.EndToEndTests/
└── Scenarios/
    └── EntityDetails/
        └── EntityDetailRetrievalTests.cs

deploy/liquibase/
├── masterfiles/
│   └── master-1.0.2603.0.yaml           # Updated to include new changelog
└── changelog/
    └── ddl/
        └── 1.0.2603.2-create-entity-detail-audit-table.sql  # New
```

**Structure Decision**: Follows established vertical-slice pattern from BusinessSearch feature. New `EntityDetails` feature folder mirrors the existing structure (Endpoints, Handlers, Queries, Models, Exceptions, Logging, Metrics). Shared infrastructure (scraper, repository) placed in existing `Infrastructure/` directories. New scraper interface in `Common/Interfaces/` follows the `IBusinessEntityScraper` pattern.

## Complexity Tracking

No constitution violations. All design choices follow KISS/YAGNI — minimum changes to existing code, maximum reuse of shared infrastructure.

## Phase 0: Research

**Output**: [research.md](research.md)

All unknowns from Technical Context resolved:

| # | Unknown | Resolution | Reference |
|---|---------|-----------|-----------|
| R-001 | Playwright PDF download strategy | Use `page.WaitForEvent("download")` + `download.SaveAs()` for PDF capture | research.md §R-001 |
| R-002 | SSRF prevention for user-supplied URL | Compile-time `[GeneratedRegex]` allowlist: HTTPS + www.sos.state.co.us + /biz/BusinessEntityDetail.do | research.md §R-002 |
| R-003 | Concurrency control model | Dedicated `SemaphoreSlim(3,3)` keyed singleton, independent from search semaphore | research.md §R-003 |
| R-004 | Audit table design | New `dbo.EntityDetailAuditEntries` with explicit CertificateUrl column | research.md §R-004 |
| R-005 | Scraper architecture | New `IEntityDetailScraper` + implementation reusing existing browser pool | research.md §R-005 |
| R-006 | Blob storage path for certificate | `{correlationId}/{documentnumber}-certificateofgoodstanding.pdf` in same container | research.md §R-006 |
| R-007 | Liquibase rollback strategy | `-- rollback` blocks in all new changelogs per user directive | research.md §R-007 |

## Phase 1: Design & Contracts

### Data Model

**Output**: [data-model.md](data-model.md)

**Entities extracted**:
- `EntityDetailAuditEntry` — 12 fields, dedicated SQL table with rollback-capable Liquibase changelog
- `EntityDetailResponse` — top-level API response record (Details + RegisteredAgent? + Certificate?)
- `EntityDetailResult` — 9 fields from CO SOS "Details" table
- `RegisteredAgentResult` — 3 nullable fields from CO SOS "Registered Agent" table
- `CertificateResult` — availability flag + storage URL + filename + error
- `EntityDetailScrapeResult` — internal scraper output (not API-exposed)

### Interface Contracts

**Output**: [contracts/openapi.yaml](contracts/openapi.yaml), [contracts/internal-contracts.md](contracts/internal-contracts.md)

**External contract** (OpenAPI 3.0.3):
- `GET /api/v1/entity-details?detailsUrl={url}` → `EntityDetailResponse`
- Error responses: 400 (SSRF validation), 502 (upstream), 503 (busy, Retry-After)

**Internal contracts**:
- `GetEntityDetailsQuery` : `IQuery<EntityDetailResponse>` — CQRS query
- `IEntityDetailScraper.ScrapeAsync()` → `EntityDetailScrapeResult` — scraper interface
- `GetEntityDetailsHandler` — orchestrates validation, scraping, upload, audit
- `EntityDetailAuditRepository.SaveAsync()` — persistence
- `InvalidDetailsUrlException` → mapped to 400 in GlobalExceptionHandler
- DI registrations: keyed semaphore, scraper singleton, repository singleton, handler scoped

### Quickstart

**Output**: [quickstart.md](quickstart.md)

## Constitution Check (Post-Design)

*Re-evaluation after Phase 1 design artifacts are complete.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First (NON-NEGOTIABLE) | PASS | Test structure defined: unit tests for handler/validator/scraper, integration tests for repository + endpoint, E2E tests for live scraping. TDD red-green-refactor workflow applies. |
| II. SOLID Design | PASS | `IEntityDetailScraper` (DIP), `GetEntityDetailsHandler` has single responsibility (orchestration), `IBlobStorageClient` reused (ISP), separate models per feature (OCP), composition via DI (Composition over Inheritance). |
| III. Architectural Quality Attributes | PASS | SSRF allowlist with `[GeneratedRegex]` (Security), Polly retry for uploads (Resilience), `[LoggerMessage]` structured logging (Supportability), dedicated `EntityDetailAuditEntries` table with explicit CertificateUrl column (Auditability), OpenTelemetry metrics + health checks (Observability), stateless handler with shared browser pool (Scalability). |
| IV. Hybrid SemVer+CalVer | PASS | Liquibase changelog version `1.0.2603.2` — MAJOR=1, MINOR=0, YYMM=2603 (March 2026), PATCH=2 (next after existing .0 and .1). |
| V. Hands-Free Deployment & Rollback | PASS | Liquibase changelog includes `-- rollback DROP TABLE dbo.EntityDetailAuditEntries;` block. No manual deployment steps. CI/CD pipeline applies migrations automatically. |

**Gate result**: PASS — all 5 principles satisfied. No violations, no unjustified complexity.
