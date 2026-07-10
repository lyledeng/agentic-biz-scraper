# Implementation Plan: Cloud Screenshot Storage & Per-Request Folder Organization

**Branch**: `003-cloud-screenshot-storage` | **Date**: 2026-03-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/003-cloud-screenshot-storage/spec.md`

## Summary

Extend the existing diagnostics subsystem to (1) organize all local diagnostic artifacts (screenshots and traces) into per-request subfolders named by correlation ID, (2) upload those artifacts to Azure Blob Storage after local save, using a dedicated `IBlobStorageClient` abstraction backed by `Azure.Storage.Blobs`, and (3) persist the cloud storage folder URL to the audit trail via a new nullable `DiagnosticsUrl` column on `SearchAuditEntries`. Cloud upload is opt-in via configuration, uses connection-string authentication, auto-creates the container at startup, and retries failed uploads 3× with exponential backoff. Upload failures never affect the search response.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`, `LangVersion=latest`)
**Primary Dependencies**: Microsoft.Playwright 1.58.0, Polly 8.6.6, Flurl.Http 4.0.2, LiteBus 4.3.0, Microsoft.Data.SqlClient 7.0.0, OpenTelemetry, Swashbuckle — **new**: Azure.Storage.Blobs (latest stable)
**Storage**: Azure SQL Managed Instance (audit trail via raw ADO.NET), local filesystem (diagnostics), Azure Blob Storage (cloud diagnostics — new)
**Testing**: xUnit 2.9.3 + NSubstitute 5.3.0 (unit), Testcontainers.MsSql + WebApplicationFactory (integration), E2E tests
**Target Platform**: Linux containers (Kestrel on port 8443 HTTPS)
**Project Type**: Web service (ASP.NET Core Minimal API)
**Performance Goals**: Cloud uploads ≤10s per file; uploads must not add latency to search responses
**Constraints**: TreatWarningsAsErrors, AnalysisLevel=latest-all, Central Package Management, no managed identity (connection string only)
**Scale/Scope**: Single API service, ~30 source files, 3 test projects

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Test-First (TDD)** | ✅ PASS | Plan includes unit tests for new `IBlobStorageClient`, updated scraper path logic, updated audit repository, updated handler. Integration tests for container auto-creation and E2E for upload flow. |
| **II. SOLID Design** | ✅ PASS | New `IBlobStorageClient` abstraction follows DIP/ISP. `PlaywrightBusinessEntityScraper` gains cloud upload via injected interface (composition). Single Responsibility maintained — scraper captures, storage client uploads. |
| **III. Quality Attributes** | ✅ PASS | Resilience: 3× retry with exponential backoff via Polly. Security: connection string from Key Vault/User Secrets, not in source. Supportability: structured log messages for upload events. Auditability: DiagnosticsUrl persisted. |
| **IV. Hybrid SemVer+CalVer** | ✅ PASS | New Liquibase migration follows `1.0.YYMM.PATCH` convention (next: `1.0.2603.1`). |
| **V. Hands-Free Deployment** | ✅ PASS | No manual steps. Connection string injected at runtime from Key Vault. Container auto-created. Configuration-only toggle. |

**Gate Result**: ALL PASS — proceed to Phase 0.

### Post-Design Re-Check (after Phase 1)

All gates re-evaluated after data-model.md and contracts/ were produced. No new violations. Design introduces one interface (`IBlobStorageClient`) with two implementations (real + null object), one new options class (`CloudStorageOptions`), and one schema migration. Complexity remains minimal.

## Project Structure

### Documentation (this feature)

```text
specs/003-cloud-screenshot-storage/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks — NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/CososWebScraping.Api/
├── Common/
│   ├── Configuration/
│   │   ├── DiagnosticsOptions.cs          # MODIFY — add CloudStorage sub-config
│   │   └── PlaywrightOptions.cs           # unchanged
│   └── Interfaces/
│       ├── IBusinessEntityScraper.cs      # unchanged
│       └── IBlobStorageClient.cs          # NEW — upload abstraction
├── Domain/
│   └── Entities/
│       └── SearchAuditEntry.cs            # MODIFY — add DiagnosticsUrl
├── Features/
│   └── BusinessSearch/
│       ├── Handlers/
│       │   └── SearchBusinessEntitiesHandler.cs  # MODIFY — pass DiagnosticsUrl to audit
│       └── Logging/
│           └── BusinessSearchLogMessages.cs      # MODIFY — add cloud upload log messages
├── Infrastructure/
│   ├── Persistence/
│   │   └── AuditTrailRepository.cs        # MODIFY — add DiagnosticsUrl column
│   └── Scraping/
│       └── PlaywrightBusinessEntityScraper.cs  # MODIFY — per-request folders + cloud upload
│   └── Storage/
│       └── AzureBlobStorageClient.cs      # NEW — Azure.Storage.Blobs implementation
├── Program.cs                             # MODIFY — register IBlobStorageClient, validate config
├── appsettings.json                       # MODIFY — add CloudStorage config section
└── appsettings.Development.json           # MODIFY — add CloudStorage dev overrides

deploy/liquibase/
├── changelog/ddl/
│   └── 1.0.2603.1-add-diagnostics-url-column.sql  # NEW — ALTER TABLE
└── masterfiles/
    └── master-1.0.2603.0.yaml             # MODIFY — include new changeset

tests/CososWebScraping.UnitTests/
├── Common/Configuration/
│   └── DiagnosticsOptionsTests.cs         # MODIFY — test CloudStorage sub-config
├── Infrastructure/
│   ├── AuditTrailRepositoryTests.cs       # MODIFY — test DiagnosticsUrl param
│   └── AzureBlobStorageClientTests.cs     # NEW — unit tests for blob client
└── Features/BusinessSearch/
    └── SearchBusinessEntitiesHandlerTests.cs  # MODIFY — test DiagnosticsUrl flow

tests/CososWebScraping.IntegrationTests/
└── Infrastructure/
    ├── AuditTrailRepositoryIntegrationTests.cs  # MODIFY — test DiagnosticsUrl column
    └── BlobStorageIntegrationTests.cs           # NEW — Azurite container tests
```

**Structure Decision**: Follows the existing single-project structure. New code goes into `Infrastructure/Storage/` for the blob client and `Common/Interfaces/` for the abstraction, matching the established pattern of interface + infrastructure implementation.

## Complexity Tracking

No constitution violations to justify. The design adds one new interface (`IBlobStorageClient`) and one implementation (`AzureBlobStorageClient`), which is the minimum needed to keep the scraper testable without cloud dependencies.
