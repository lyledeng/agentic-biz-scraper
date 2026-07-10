# Tasks: Cloud Screenshot Storage & Per-Request Folder Organization

**Input**: Design documents from `specs/003-cloud-screenshot-storage/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Included per constitution (Principle I — Test-First, NON-NEGOTIABLE).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Package References)

**Purpose**: Add the Azure Blob Storage SDK dependency required by cloud upload features.

- [X] T001 Add Azure.Storage.Blobs package to Directory.Packages.props and CososWebScraping.Api.csproj

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared infrastructure that MUST be complete before user story implementation. Includes configuration model, interface definitions, schema migration, entity changes, and logging. US1 can proceed in parallel with this phase (it has no cloud infrastructure dependencies).

**⚠️ CRITICAL**: US2, US3, US4, US5 cannot begin until this phase is complete.

- [X] T002 [P] Add CloudStorageOptions class and CloudStorage property to DiagnosticsOptions in src/CososWebScraping.Api/Common/Configuration/DiagnosticsOptions.cs
- [X] T003 [P] Define IBlobStorageClient interface with UploadAsync and GetFolderUrl methods in src/CososWebScraping.Api/Common/Interfaces/IBlobStorageClient.cs
- [X] T004 Create NullBlobStorageClient no-op implementation of IBlobStorageClient in src/CososWebScraping.Api/Infrastructure/Storage/NullBlobStorageClient.cs
- [X] T005 [P] Add cloud upload and diagnostics URL source-generated log messages to src/CososWebScraping.Api/Features/BusinessSearch/Logging/BusinessSearchLogMessages.cs
- [X] T006 [P] Add CloudStorage configuration section to src/CososWebScraping.Api/appsettings.json and src/CososWebScraping.Api/appsettings.Development.json
- [X] T007 [P] Add nullable DiagnosticsUrl property to SearchAuditEntry in src/CososWebScraping.Api/Domain/Entities/SearchAuditEntry.cs
- [X] T008 [P] Create Liquibase migration adding DiagnosticsUrl column in deploy/liquibase/changelog/ddl/1.0.2603.1-add-diagnostics-url-column.sql
- [X] T009 Update Liquibase masterfile to include new changeset in deploy/liquibase/masterfiles/master-1.0.2603.0.yaml

**Checkpoint**: Foundation ready — user story implementation can now begin. T002–T003 and T005–T008 can all run in parallel (different files, no cross-dependencies). T004 depends on T003. T009 depends on T008.

---

## Phase 3: User Story 1 — Organize Local Screenshots in Per-Request Folders (Priority: P1) 🎯 MVP

**Goal**: All diagnostic artifacts (screenshots and traces) for a single search request are saved in a subfolder named `{correlationId}` under the local diagnostics output path, instead of the current flat directory.

**Independent Test**: Trigger a search request. Verify all screenshots and traces are written into `{outputPath}/{correlationId}/` rather than directly into `{outputPath}/`.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T010 [US1] Write unit tests for per-request subfolder path construction — update existing BuildDiagnosticArtifactPath tests to expect `{outputDir}/{correlationId}/{timestamp}_{milestone}.{ext}` and add TryEnsureDiagnosticsOutputDirectory test with correlationId subdirectory creation in tests/CososWebScraping.UnitTests/Common/Configuration/DiagnosticsOptionsTests.cs

### Implementation for User Story 1

- [X] T011 [US1] Refactor BuildDiagnosticArtifactPath to place files in a `{sanitizedCorrelationId}` subdirectory and update TryEnsureDiagnosticsOutputDirectory to create per-request subdirectory in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs

**Checkpoint**: At this point, all local diagnostic artifacts are organized in per-request subfolders. Verifiable independently without any cloud infrastructure.

---

## Phase 4: User Story 2 — Upload Screenshots to Cloud Storage (Priority: P2)

**Goal**: Each screenshot captured during a scraping session is uploaded to the configured Azure Blob Storage container under a virtual folder matching the request's correlation ID, after being saved locally.

**Independent Test**: Enable cloud upload via configuration. Trigger a search request. Verify screenshots appear in the blob container under `{correlationId}/`.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] [US2] Write unit tests for AzureBlobStorageClient retry logic, GetFolderUrl computation, and NullBlobStorageClient no-op behavior in tests/CososWebScraping.UnitTests/Infrastructure/AzureBlobStorageClientTests.cs
- [X] T013 [P] [US2] Write integration test for blob upload and container auto-creation using Azurite testcontainer in tests/CososWebScraping.IntegrationTests/Infrastructure/BlobStorageIntegrationTests.cs

### Implementation for User Story 2

- [X] T014 [US2] Implement AzureBlobStorageClient with BlobContainerClient, Polly retry pipeline (3× exponential backoff, 30s timeout), and GetFolderUrl in src/CososWebScraping.Api/Infrastructure/Storage/AzureBlobStorageClient.cs
- [X] T015 [US2] Register IBlobStorageClient in DI with conditional logic (AzureBlobStorageClient when Enabled, NullBlobStorageClient when disabled) and container auto-creation at startup in src/CososWebScraping.Api/Program.cs
- [X] T016 [US2] Inject IBlobStorageClient into PlaywrightBusinessEntityScraper constructor and add cloud upload call with try/catch/log after local screenshot save in CaptureScreenshotAsync in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs

**Checkpoint**: Screenshots are uploaded to cloud storage after local save. Upload failures are logged and swallowed. NullBlobStorageClient ensures zero overhead when disabled.

---

## Phase 5: User Story 5 — Persist Cloud Storage URL to Audit Trail (Priority: P2)

**Goal**: Each audit trail entry includes the cloud storage folder URL for that request's diagnostic artifacts, enabling direct navigation from audit records to cloud-stored screenshots and traces.

**Independent Test**: Enable cloud upload. Trigger a search request. Query `dbo.SearchAuditEntries` and verify the `DiagnosticsUrl` column contains the expected blob folder URL.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T017 [P] [US5] Write unit tests for DiagnosticsUrl parameter in AuditTrailRepository INSERT statement in tests/CososWebScraping.UnitTests/Infrastructure/AuditTrailRepositoryTests.cs
- [X] T018 [P] [US5] Write unit tests for DiagnosticsUrl computation and propagation in SearchBusinessEntitiesHandler in tests/CososWebScraping.UnitTests/Features/BusinessSearch/SearchBusinessEntitiesHandlerTests.cs
- [X] T019 [P] [US5] Write integration test for DiagnosticsUrl column persistence and retrieval in tests/CososWebScraping.IntegrationTests/Infrastructure/AuditTrailRepositoryIntegrationTests.cs

### Implementation for User Story 5

- [X] T020 [US5] Add DiagnosticsUrl parameter to INSERT statement and command parameters in src/CososWebScraping.Api/Infrastructure/Persistence/AuditTrailRepository.cs
- [X] T021 [US5] Update SearchBusinessEntitiesHandler to compute DiagnosticsUrl via IBlobStorageClient.GetFolderUrl and pass it to WriteAuditAsync in src/CososWebScraping.Api/Features/BusinessSearch/Handlers/SearchBusinessEntitiesHandler.cs

**Checkpoint**: Audit trail entries include the cloud storage folder URL. URL is populated even when upload fails (per FR-014). URL is null when cloud upload is disabled.

---

## Phase 6: User Story 3 — Upload Trace Archives to Cloud Storage (Priority: P3)

**Goal**: Trace archives (`.zip` files) are uploaded to the same blob container and per-request folder as screenshots, giving operators one centralized location for all diagnostic artifacts.

**Independent Test**: Enable cloud upload and tracing. Trigger a search that produces a trace. Verify the trace `.zip` appears in the blob container alongside screenshots.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T022 [US3] Write unit tests for trace archive cloud upload in StopTracingAsync — verify upload is called with correct blob path and failures are logged and swallowed in tests/CososWebScraping.UnitTests/Features/BusinessSearch/DiagnosticsCloudUploadTests.cs

### Implementation for User Story 3

- [X] T023 [US3] Add cloud upload call with try/catch/log after local trace save in StopTracingAsync in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs

**Checkpoint**: All diagnostic artifact types (screenshots and traces) are uploaded to cloud storage. The upload pipeline is fully functional for all artifact types.

---

## Phase 7: User Story 4 — Configure Cloud Storage Settings (Priority: P3)

**Goal**: Operators can enable/disable cloud upload, change container names, and point to different storage accounts via configuration alone — no code changes required.

**Independent Test**: Toggle `CloudStorage:Enabled` to `true` with missing connection string. Verify the application fails fast at startup. Set valid config, restart, verify uploads work with new settings.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T024 [P] [US4] Write unit tests for CloudStorageOptions configuration binding, default values, and custom value parsing in tests/CososWebScraping.UnitTests/Common/Configuration/DiagnosticsOptionsTests.cs
- [X] T025 [P] [US4] Write integration test for startup fail-fast when Enabled is true but ConnectionString or ContainerName is empty in tests/CososWebScraping.IntegrationTests/Features/BusinessSearch/CloudStorageStartupTests.cs

### Implementation for User Story 4

- [X] T026 [US4] Add ValidateDataAnnotations and custom Validate callbacks for CloudStorageOptions with ValidateOnStart in src/CososWebScraping.Api/Program.cs

**Checkpoint**: Configuration is validated at startup. Invalid config fails fast. Disabled config skips blob client initialization entirely.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Test doubles, build verification, and end-to-end validation.

- [X] T027 [P] Update test doubles for IBlobStorageClient in tests/CososWebScraping.UnitTests/TestSupport/DiagnosticsTestDoubles.cs
- [X] T028 [P] Verify solution builds with zero warnings via `dotnet build /warnaserror`
- [X] T029 Run quickstart.md validation steps to verify end-to-end flow (local folders, cloud upload, audit trail URL)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS US2, US3, US4, US5
- **US1 (Phase 3)**: Depends on Phase 1 only — can start in PARALLEL with Phase 2
- **US2 (Phase 4)**: Depends on Phase 2 completion
- **US5 (Phase 5)**: Depends on Phase 2 + Phase 4 (needs IBlobStorageClient.GetFolderUrl injected by US2)
- **US3 (Phase 6)**: Depends on Phase 4 (reuses upload infrastructure from US2)
- **US4 (Phase 7)**: Depends on Phase 2 + Phase 4 (validates config already registered)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

```
Phase 1: Setup
    │
    ├──────────────────────┐
    ▼                      ▼
Phase 2: Foundational    Phase 3: US1 (P1) ←── can start in parallel
    │
    ▼
Phase 4: US2 (P2)
    │
    ├──────────┐
    ▼          ▼
Phase 5:    Phase 6:    Phase 7:
US5 (P2)    US3 (P3)    US4 (P3) ←── these three can run in parallel
    │          │          │
    └──────────┴──────────┘
               │
               ▼
         Phase 8: Polish
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD — Constitution I)
- Models/interfaces before services
- Services before consumers
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 2** (6 of 8 tasks parallel):
- T002, T003, T005, T006, T007, T008 — all different files, no cross-dependencies
- Then: T004 (after T003), T009 (after T008)

**Phase 3** (US1): Sequential — T010 (test) → T011 (implement)

**Phase 4** (US2):
- T012 and T013 in parallel (different test files)
- Then: T014 → T015 → T016 sequential

**Phase 5** (US5):
- T017, T018, T019 all in parallel (different test files)
- Then: T020 → T021 sequential

**Phase 6** (US3): Sequential — T022 (test) → T023 (implement)

**Phase 7** (US4):
- T024 and T025 in parallel (different test files)
- Then: T026

**Phase 8**: T027 and T028 in parallel, then T029

---

## Parallel Example: Phase 2 Foundation

```text
# Batch 1 — all parallel (different files, no deps):
T002: CloudStorageOptions in DiagnosticsOptions.cs
T003: IBlobStorageClient in IBlobStorageClient.cs
T005: Log messages in BusinessSearchLogMessages.cs
T006: Config section in appsettings.json + appsettings.Development.json
T007: DiagnosticsUrl in SearchAuditEntry.cs
T008: Liquibase SQL file

# Batch 2 — after batch 1:
T004: NullBlobStorageClient (needs T003 interface)
T009: Liquibase masterfile (needs T008 SQL file)
```

## Parallel Example: User Story 2

```text
# Batch 1 — tests in parallel:
T012: Unit tests for AzureBlobStorageClient
T013: Integration tests with Azurite

# Batch 2 — implementation sequential:
T014: Implement AzureBlobStorageClient
T015: Register in DI with container auto-creation
T016: Inject into scraper + upload screenshots
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 3: US1 (can skip Phase 2 — US1 has no cloud deps)
3. **STOP and VALIDATE**: Local screenshots are in per-request subfolders
4. This alone delivers value — organized local diagnostics

### Incremental Delivery

1. Phase 1 (Setup) + Phase 2 (Foundation) + Phase 3 (US1) → Local folders organized ✅
2. Phase 4 (US2) → Screenshots uploaded to cloud ✅
3. Phase 5 (US5) → Audit trail has cloud URLs ✅
4. Phase 6 (US3) → Traces also uploaded to cloud ✅
5. Phase 7 (US4) → Config validation hardened ✅
6. Phase 8 (Polish) → Build clean, quickstart validated ✅

Each phase adds value without breaking previous phases.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Cloud upload failures never affect search responses (FR-007)
- NullBlobStorageClient ensures zero overhead when cloud upload is disabled
- Constitution requires TDD: tests written first, must fail before implementation
- All log messages use `[LoggerMessage]` source generation (no string interpolation)
- All config uses `IOptions<T>` pattern with `ValidateOnStart`
