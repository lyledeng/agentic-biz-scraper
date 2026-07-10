# Tasks: WY History Document Cloud Storage

**Input**: Design documents from `/specs/008-wy-history-doc-storage/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, quickstart.md

**Tests**: Test tasks are included per Constitution Principle I (Test-First, NON-NEGOTIABLE). Tests are written BEFORE implementation in each user story phase (Red-Green-Refactor).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Database migration and scraping definition change required before any user story work

- [X] T001 Create Liquibase changelog to add DocumentStorageUrl column in deploy/liquibase/changelog/ddl/2.0.2603.4-add-document-storage-url-to-entity-detail-audit.sql
- [X] T002 Register new changelog in Liquibase master file deploy/liquibase/masterfiles/master-1.0.2603.0.yaml

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure changes that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Remove 3-document limit from history extraction JavaScript in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json
- [X] T004 [P] Add DocumentStorageUrl property to EntityDetailAuditEntry in src/BizScraper.Api/Domain/Entities/EntityDetailAuditEntry.cs
- [X] T005 [P] Add DocumentStorageUrl column and parameter to SQL insert and ConfigureSaveCommand in src/BizScraper.Api/Infrastructure/Persistence/EntityDetailAuditRepository.cs
- [X] T006 [P] Add source-generated log messages for document download/upload events in src/BizScraper.Api/Features/EntityDetails/Logging/EntityDetailsLogMessages.cs
- [X] T007 [P] Add document download counter metric in src/BizScraper.Api/Features/EntityDetails/Metrics/EntityDetailsMetrics.cs
- [X] T008 [P] Update integration test table creation DDL to include DocumentStorageUrl column in tests/BizScraper.IntegrationTests/Features/EntityDetails/EntityDetailAuditRepositoryTests.cs

**Checkpoint**: Foundation ready — entity detail audit accepts DocumentStorageUrl, integration test schema aligned, all history documents are extracted, logging and metrics instrumented. User story implementation can now begin.

---

## Phase 3: User Story 1 + 2 + 4 — Download, Upload, Organize, and Audit (Priority: P1) 🎯 MVP

**Goal**: After scraping WY entity details, iterate through all history documents, download each PDF sequentially via Playwright browser context, save locally to the per-request diagnostics folder, upload to Azure Blob Storage under `{correlationId}/{fileName}`, replace the `storageUrl` field with the cloud storage URL, and persist the document storage folder URL to the audit trail. This phase merges US2 (folder organization) and US4 (audit trail persistence) into US1 as they are inherently addressed by the same implementation.

**Independent Test**: Call `GET /api/v1/entity-details?detailsUrl=...&state=WY` for an entity with history documents. Verify each document's `storageUrl` in the response points to Azure Blob Storage (not WY SOS). Verify blobs are stored under `{correlationId}/`. Verify audit trail `DocumentStorageUrl` is populated.

### Tests for User Story 1 (Write FIRST — must FAIL before implementation) 🔴

- [X] T009 [P] [US1] Create unit test file tests/BizScraper.UnitTests/Features/EntityDetails/HistoryDocumentDownloadTests.cs — test that when a WY scrape returns history documents with storageUrls and CloudStorage is enabled, the handler builds new HistoryDocumentResult instances (via `with` expression) with cloud storage URLs. Test that when CloudStorage is disabled, original WY SOS URLs are returned unchanged. Test that blob path follows `{correlationId}/{fileName}` convention (US2). Test that pre-slugified filenames from extraction JS are used directly (no re-sanitization needed — filenames are already slugified by wy-entity-details.json extraction JavaScript).
- [X] T010 [P] [US1] Add unit test to tests/BizScraper.UnitTests/Features/EntityDetails/GetEntityDetailsHandlerTests.cs — verify that when WY scrape returns history documents, the handler processes documents and the response includes cloud storage URLs. Verify WriteAuditAsync receives the documentStorageUrl parameter (US4).

### Implementation for User Story 1 🟢

- [X] T011 [US1] Add history document download and upload orchestration loop in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs — after the existing certificate upload block (line ~91), iterate through scraped.HistoryDocuments sequentially: for each document, download via Playwright browser context APIRequestContext.GetAsync(storageUrl), validate content-type is application/pdf, save to local diagnostics path {correlationId}/{fileName}, upload via IBlobStorageClient.UploadAsync, and build a new HistoryDocumentResult using `with` expression to replace StorageUrl with the cloud blob URL (HistoryDocumentResult is a `sealed record` — construct new instances, do not mutate). Guard the entire block with the existing CloudStorage.Enabled config toggle. Note: filenames from wy-entity-details.json extraction JS are pre-slugified — no additional sanitization needed (resolves edge case). Note: upload retries (FR-006) are satisfied by the existing AzureBlobStorageClient retry pipeline (3 retries, exponential backoff via Polly).
- [X] T012 [US1] Update WriteAuditAsync calls in GetEntityDetailsHandler to pass documentStorageUrl parameter in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs — add a documentStorageUrl parameter to the WriteAuditAsync method and include it in the EntityDetailAuditEntry construction. Pass the blob folder URL from the success path and null from error/validation/disabled paths. This satisfies US4 (audit trail persistence).

**Checkpoint**: WY entity detail requests download ALL history documents, save locally, upload to blob storage under per-request folders, return cloud storage URLs, and persist the document storage URL to the audit trail. Tests T009/T010 pass. This is the MVP.

---

## Phase 4: User Story 3 — Handle Document Download Failures Gracefully (Priority: P2)

**Goal**: If an individual document download or upload fails, populate the `error` field on that document entry, log a warning, and continue processing remaining documents. The entity details response MUST NOT fail due to document failures.

**Independent Test**: Simulate a document whose WY SOS URL returns a non-PDF response, zero-byte response, or network error. Verify the response still contains entity details and the failing document has an `error` field populated.

### Tests for User Story 3 (Write FIRST — must FAIL before implementation) 🔴

- [X] T013 [US3] Add error handling test cases to tests/BizScraper.UnitTests/Features/EntityDetails/HistoryDocumentDownloadTests.cs — test that when a document download fails, the error field is populated and the original WY SOS storageUrl is retained as fallback. Test that other documents continue processing successfully. Test that a non-PDF content-type response sets the error field and skips upload. Test that a zero-byte response with application/pdf content-type sets the error field and skips upload (edge case: corrupt/empty document). Test that a per-document timeout sets the error field and continues to the next document.

### Implementation for User Story 3 🟢

- [X] T014 [US3] Add per-document try/catch with error handling in the download loop in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs — wrap each document download/validate/save/upload iteration in a try/catch that catches non-cancellation exceptions, logs a warning via the log message from T006, builds a new HistoryDocumentResult (via `with` expression) with the error field populated, retains the original WY SOS storageUrl as fallback, and continues to the next document.
- [X] T015 [US3] Add content-type and zero-byte validation before local save in the download loop in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs — after downloading via APIRequestContext.GetAsync, check the response Content-Type header for application/pdf. If not PDF, log a warning, set the error field to describe the unexpected content type, skip local save and upload, and continue. Additionally, verify response body length > 0; if zero bytes, log a warning, set the error field ("Downloaded document is empty"), and skip upload.
- [X] T016 [US3] Add per-document download timeout enforcement in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs — use a CancellationTokenSource with a configurable timeout (default 30 seconds) for each document download call. On timeout, catch OperationCanceledException from the per-document token (not the request-level token), log a warning, set error, and continue.

**Checkpoint**: Individual document failures are isolated. The entity details response always returns, with error details on failed documents and cloud URLs on successful ones. Tests T013 pass.

---

## Phase 5: Audit Trail Integration Test

**Purpose**: Validate the DocumentStorageUrl column persists correctly end-to-end via integration test with Testcontainers

- [X] T017 [US4] Create integration test file tests/BizScraper.IntegrationTests/Features/EntityDetails/DocumentStorageAuditTests.cs — test that after saving an EntityDetailAuditEntry with a DocumentStorageUrl value, the row is persisted and retrievable from the database. Test that when DocumentStorageUrl is null, the column stores NULL. Uses Testcontainers with Azure SQL Edge (same pattern as EntityDetailAuditRepositoryTests.cs).

**Checkpoint**: All user stories validated with test coverage across unit and integration layers.

---

## Phase 6: Polish & Validation

**Purpose**: End-to-end validation

- [X] T018 Run quickstart.md validation steps to confirm feature end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 for database column availability — BLOCKS all user stories
- **User Story 1+2+4 (Phase 3)**: Depends on Phase 2 — core download/upload/organize/audit logic. Tests (T009, T010) written first, then implementation (T011, T012).
- **User Story 3 (Phase 4)**: Depends on Phase 3 — adds error handling around the download loop. Tests (T013) written first, then implementation (T014, T015, T016).
- **Audit Integration Test (Phase 5)**: Depends on Phase 2 (T008 DDL) + Phase 3 (T012 audit wiring) — validates audit column end-to-end
- **Polish (Phase 6)**: Depends on all prior phases

### Test-First Ordering (Constitution I Compliance)

- **T009, T010 MUST be completed before T011** — tests must fail (Red) before implementation (Green)
- **T013 MUST be completed before T014** — error handling tests must fail before error handling code
- **T017 depends on T008 (DDL) + T012 (audit wiring)** but is a standalone integration test

### Parallel Opportunities

**Phase 2 (Foundational)**: T004, T005, T006, T007, T008 — all [P], different files.

**Phase 3 (Tests)**: T009 and T010 — different files, can run in parallel.

**Phase 5**: T017 can run in parallel with Phase 4 if US3 error handling is not needed for the audit test.

---

## Parallel Example: Phase 2 (Foundational)

```bash
# Launch all foundational tasks together (different files):
Task T004: "Add DocumentStorageUrl property to EntityDetailAuditEntry.cs"
Task T005: "Add DocumentStorageUrl to EntityDetailAuditRepository.cs SQL and ConfigureSaveCommand"
Task T006: "Add document download/upload log messages to EntityDetailsLogMessages.cs"
Task T007: "Add document download counter metric to EntityDetailsMetrics.cs"
Task T008: "Update integration test DDL to include DocumentStorageUrl column"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (Liquibase migration)
2. Complete Phase 2: Foundational (all [P] tasks in parallel)
3. Write failing tests: T009, T010 (Red 🔴)
4. Implement: T011, T012 (Green 🟢)
5. **STOP and VALIDATE**: All tests pass. WY entity details downloads docs, returns cloud URLs, audit trail populated.

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Tests T009, T010 → Fail (Red) → Implement T011, T012 → Pass (Green) → **MVP!**
3. Tests T013 → Fail (Red) → Implement T014, T015, T016 → Pass (Green) → Error handling complete
4. Integration test T017 → Audit trail validated
5. T018 → Quickstart validated → Final deploy

### File Modification Summary

| File | Tasks | Change Description |
|------|-------|--------------------|
| deploy/liquibase/changelog/ddl/2.0.2603.4-*.sql | T001 | New: ALTER TABLE ADD DocumentStorageUrl |
| deploy/liquibase/masterfiles/master-1.0.2603.0.yaml | T002 | Add changelog reference |
| src/.../Definitions/wy-entity-details.json | T003 | Remove `docs.length >= 3` limit |
| src/.../Entities/EntityDetailAuditEntry.cs | T004 | Add DocumentStorageUrl property |
| src/.../Persistence/EntityDetailAuditRepository.cs | T005 | Add column to SQL + ConfigureSaveCommand |
| src/.../Logging/EntityDetailsLogMessages.cs | T006 | Add 4 log messages (download start/complete/fail, content-type invalid) |
| src/.../Metrics/EntityDetailsMetrics.cs | T007 | Add DocumentDownloaded counter |
| tests/.../EntityDetailAuditRepositoryTests.cs | T008 | Update DDL to include new column |
| tests/.../HistoryDocumentDownloadTests.cs | T009, T013 | New: unit tests for download/upload + error handling |
| tests/.../GetEntityDetailsHandlerTests.cs | T010 | Add document download handler test |
| src/.../Handlers/GetEntityDetailsHandler.cs | T011, T012, T014, T015, T016 | Download loop, audit wiring, error handling, validation, timeout |
| tests/.../DocumentStorageAuditTests.cs | T017 | New: integration test for audit column |

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- **Test-First**: Tests MUST be written and FAIL before implementation (Constitution I)
- HistoryDocumentResult is a `sealed record` — use `with` expression to create modified instances
- Filenames from WY SOS extraction JS are pre-slugified — no additional sanitization needed
- Upload retry (FR-006) is satisfied by existing AzureBlobStorageClient Polly pipeline (3 retries, exponential backoff)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The download loop in T011 follows the exact pattern of the existing certificate upload (handler lines 76–91)
