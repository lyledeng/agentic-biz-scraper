# Implementation Plan: WY History Document Cloud Storage

**Branch**: `008-wy-history-doc-storage` | **Date**: 2026-03-26 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-wy-history-doc-storage/spec.md`

## Summary

Extend the WY entity details flow to download ALL history documents from the Wyoming SOS site and upload them to Azure Blob Storage, replacing the WY SOS URLs with cloud storage URLs in the API response. Documents are downloaded sequentially via the active Playwright browser context (to inherit session cookies), saved locally to the per-request diagnostics folder, then uploaded to Azure Blob Storage using the existing `IBlobStorageClient` infrastructure from feature 003. Individual download/upload failures are handled gracefully — each failed document gets an error message while the overall response succeeds. The audit trail is updated with the document storage folder URL.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS), `net10.0`
**Primary Dependencies**: Playwright (Azure Playwright Workspaces), Azure.Storage.Blobs, Polly, System.Text.Json (source-generated), LiteBus
**Storage**: Azure SQL Managed Instance (audit trail), Azure Blob Storage (documents + diagnostics)
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers w/ Azure SQL Edge + Azurite, Playwright E2E
**Target Platform**: Azure AKS, Kestrel on port 8443 (HTTPS only)
**Project Type**: Web service (ASP.NET Core Minimal API)
**Performance Goals**: History document download+upload < 30 seconds per document
**Constraints**: Sequential downloads (one at a time), max 3 concurrent entity detail requests (existing semaphore), Playwright browser context required for session cookies
**Scale/Scope**: WY entities only, ~8 modified source files, ~3 new test files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | **PASS** | Unit tests for document download/upload logic, error handling, content validation; integration tests for blob storage upload with Azurite; E2E test for full WY details with document download |
| II. SOLID Design | **PASS** | Reuses existing `IBlobStorageClient` abstraction; document download/upload logic added directly to `GetEntityDetailsHandler` following the existing certificate upload pattern (lines 76–91); single responsibility maintained — handler orchestrates scrape, download, upload, and audit as a cohesive vertical-slice workflow |
| III. Architectural Quality | **PASS** | Audit trail extended with document storage URL; structured logging per-document with correlation ID; Polly retry on upload (3 retries, exponential backoff via existing pipeline); content-type validation |
| IV. Hybrid SemVer+CalVer | **PASS** | New Liquibase changelog follows `2.0.2603.4` (next available version after `2.0.2603.3`) |
| V. Hands-Free Deployment | **PASS** | Liquibase migration with rollback; config toggle via `appsettings.json`; no manual steps |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/008-wy-history-doc-storage/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Features/
│   └── EntityDetails/
│       ├── Handlers/GetEntityDetailsHandler.cs              # MODIFY: orchestrate document download/upload after scrape
│       ├── Logging/EntityDetailsLogMessages.cs              # MODIFY: add document download/upload log messages
│       ├── Metrics/EntityDetailsMetrics.cs                  # MODIFY: add document download counter
│       └── Models/
│           └── HistoryDocumentResult.cs                     # EXISTS: no changes needed (storageUrl, fileName, error already present)
├── Common/
│   └── Interfaces/
│       └── IEntityDetailScraper.cs                          # EXISTS: no changes — scrape result already includes historyDocuments
├── Infrastructure/
│   ├── Storage/
│   │   └── AzureBlobStorageClient.cs                        # EXISTS: reuse UploadAsync method
│   ├── Scraping/
│   │   └── Definitions/
│   │       └── wy-entity-details.json                       # MODIFY: remove 3-doc limit from history extraction JS
│   └── Persistence/
│       ├── EntityDetailAuditRepository.cs                   # MODIFY: add DocumentStorageUrl column parameter
│       └── EntityDetailAuditEntry.cs                        # MODIFY: add DocumentStorageUrl property
├── Common/
│   └── Configuration/
│       └── DiagnosticsOptions.cs                            # EXISTS: reuse CloudStorage configuration
└── Program.cs                                               # EXISTS: no changes needed

deploy/liquibase/
├── changelog/ddl/
│   └── 2.0.2603.4-add-document-storage-url-to-entity-detail-audit.sql  # NEW
└── masterfiles/
    └── master-1.0.2603.0.yaml                               # MODIFY: add new changelog reference

tests/
├── BizScraper.UnitTests/
│   └── Features/EntityDetails/
│       └── HistoryDocumentDownloadTests.cs                  # NEW: unit tests for download/upload orchestration
├── BizScraper.IntegrationTests/
│   └── Features/EntityDetails/
│       └── DocumentStorageAuditTests.cs                     # NEW: integration test for audit column
└── BizScraper.UnitTests/
    └── Features/EntityDetails/
        └── GetEntityDetailsHandlerTests.cs                  # MODIFY: add tests for document download integration
```

**Structure Decision**: Follows the existing vertical slice architecture. No new projects, folders, or abstractions. The document download/upload logic is added directly to the `GetEntityDetailsHandler` (same pattern as the existing certificate upload logic). The `wy-entity-details.json` flow definition is updated to remove the 3-document limit.
