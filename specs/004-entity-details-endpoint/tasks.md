# Tasks: Entity Details Retrieval & Certificate of Good Standing Download

**Input**: Design documents from `/specs/004-entity-details-endpoint/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and feature folder scaffolding

- [X] T001 Create EntityDetails feature folder structure per plan.md and add EntityDetails configuration section (`MaxConcurrentRequests: 3`) to src/CososWebScraping.Api/appsettings.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T002 [P] Create Liquibase DDL changelog `1.0.2603.2-create-entity-detail-audit-table.sql` with `-- rollback DROP TABLE dbo.EntityDetailAuditEntries;` block in deploy/liquibase/changelog/ddl/ and update deploy/liquibase/masterfiles/master-1.0.2603.0.yaml to include it
- [X] T003 [P] Create EntityDetailAuditEntry entity class with 12 fields (Id, DetailsUrl, EntityIdNumber, EntityName, EntityStatus, CertificateUrl, DiagnosticsUrl, DurationMs, Status, ErrorDetail, CorrelationId, CreatedAt) in src/CososWebScraping.Api/Domain/Entities/EntityDetailAuditEntry.cs per data-model.md
- [X] T004 [P] Create InvalidDetailsUrlException with primary constructor in src/CososWebScraping.Api/Features/EntityDetails/Exceptions/InvalidDetailsUrlException.cs per contracts/internal-contracts.md
- [X] T005 [P] Create all EntityDetails model records in src/CososWebScraping.Api/Features/EntityDetails/Models/: EntityDetailRequest.cs, EntityDetailResult.cs, RegisteredAgentResult.cs, CertificateResult.cs, EntityDetailResponse.cs, and EntityDetailScrapeResult.cs per data-model.md
- [X] T006 [P] Create EntityDetailsLogMessages as a static partial class with [LoggerMessage] source-generated extension methods for ILogger (started, completed, failed, rejected-busy, validation-error, certificate-download, audit-write) in src/CososWebScraping.Api/Features/EntityDetails/Logging/EntityDetailsLogMessages.cs following BusinessSearchLogMessages conventions
- [X] T007 [P] Create EntityDetailsMetrics with OpenTelemetry counters and histograms (request count, success/failure, duration, certificate downloads) in src/CososWebScraping.Api/Features/EntityDetails/Metrics/EntityDetailsMetrics.cs following BusinessSearchMetrics conventions
- [X] T008 Create IEntityDetailScraper interface with `ScrapeAsync(string detailsUrl, string correlationId, CancellationToken)` returning `EntityDetailScrapeResult` in src/CososWebScraping.Api/Common/Interfaces/IEntityDetailScraper.cs per contracts/internal-contracts.md
- [X] T009 Create GetEntityDetailsQuery record implementing `IQuery<EntityDetailResponse>` in src/CososWebScraping.Api/Features/EntityDetails/Queries/GetEntityDetailsQuery.cs per contracts/internal-contracts.md
- [X] T010 Create EntityDetailAuditRepository with parameterized INSERT into dbo.EntityDetailAuditEntries, graceful skip on missing connection string, and warning-only on failure in src/CososWebScraping.Api/Infrastructure/Persistence/EntityDetailAuditRepository.cs following AuditTrailRepository conventions
- [X] T011 Update GlobalExceptionHandler to map InvalidDetailsUrlException to 400 Bad Request in src/CososWebScraping.Api/Middleware/GlobalExceptionHandler.cs
- [X] T012 Register EntityDetailResponse, EntityDetailResult, RegisteredAgentResult, and CertificateResult types with [JsonSerializable] attributes in src/CososWebScraping.Api/Common/Serialization/AppJsonSerializerContext.cs

**Checkpoint**: Foundation ready — all models, interfaces, persistence, and cross-cutting concerns in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Retrieve Business Entity Details (Priority: P1) 🎯 MVP

**Goal**: Scrape the Details and Registered Agent tables from a CO SOS entity detail page and return structured JSON. Detect certificate link availability. Enforce SSRF-prevention URL validation and configurable concurrency.

**Independent Test**: Call `GET /api/v1/entity-details?detailsUrl=https%3A%2F%2Fwww.sos.state.co.us%2Fbiz%2FBusinessEntityDetail.do%3FqueueMode%3DPQ%26masterFileId%3D20181871974` and verify the JSON response contains all Detail and Registered Agent fields matching the CO SOS website.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T013 [P] [US1] Unit test: Create UrlAllowlistValidationTests.cs — valid HTTPS CO SOS URL accepted, HTTP scheme rejected, wrong host rejected, wrong path rejected, null/empty input rejected, URL with fragment/userinfo rejected — in tests/CososWebScraping.UnitTests/Features/EntityDetails/UrlAllowlistValidationTests.cs
- [X] T014 [P] [US1] Unit test: Create GetEntityDetailsHandlerTests.cs — successful scrape maps to EntityDetailResponse, SSRF validation rejects bad URL (throws InvalidDetailsUrlException), semaphore busy throws ServiceBusyException, audit entry written on success, audit entry written on failure, scraper exception propagates correctly — in tests/CososWebScraping.UnitTests/Features/EntityDetails/GetEntityDetailsHandlerTests.cs (use NSubstitute for IEntityDetailScraper, IBlobStorageClient, EntityDetailAuditRepository)
- [X] T015 [P] [US1] Unit test: Create EntityDetailRequestValidationTests.cs — valid EntityDetailRequest accepted, empty DetailsUrl rejected — in tests/CososWebScraping.UnitTests/Features/EntityDetails/EntityDetailRequestValidationTests.cs
- [X] T016 [P] [US1] Unit test: Create EntityDetailScrapingTests.cs — Details table extraction produces all 9 fields, Registered Agent table extraction produces 3 fields, missing Registered Agent table returns null, certificate link detected when present, certificate link absent returns CertificateAvailable=false — in tests/CososWebScraping.UnitTests/Features/EntityDetails/EntityDetailScrapingTests.cs
- [X] T017 [P] [US1] Integration test: Create EntityDetailAuditRepositoryTests.cs with Testcontainers MsSql — INSERT round-trip with all fields, graceful skip when connection string is missing, DBNull.Value for nullable fields (EntityIdNumber, EntityName, EntityStatus, CertificateUrl, DiagnosticsUrl, ErrorDetail) — in tests/CososWebScraping.IntegrationTests/Features/EntityDetails/EntityDetailAuditRepositoryTests.cs

### Implementation for User Story 1

- [X] T018 [P] [US1] Implement PlaywrightEntityDetailScraper: navigate to detailsUrl, scrape "Details" table (9 fields), scrape "Registered Agent" table (3 fields), detect "Get a certificate of good standing" link presence, return EntityDetailScrapeResult in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightEntityDetailScraper.cs — reuse existing PlaywrightBusinessEntityScraper browser pool via EnsureBrowserAsync per research.md §R-005
- [X] T019 [P] [US1] Implement GetEntityDetailsHandler as IQueryHandler<GetEntityDetailsQuery, EntityDetailResponse>: extract correlationId from TraceIdentifier, validate detailsUrl against SSRF allowlist (HTTPS + www.sos.state.co.us + /biz/BusinessEntityDetail.do) using [GeneratedRegex] per research.md §R-002, acquire keyed semaphore (throw ServiceBusyException on timeout), call scraper, build EntityDetailResponse, write audit entry on all code paths in src/CososWebScraping.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs. Ensure existing RequestTimeoutMiddleware covers this endpoint for SC-001 (30s) performance enforcement.
- [X] T020 [P] [US1] Create EntityDetailsEndpoints with `GET /api/v1/entity-details` route accepting `detailsUrl` query parameter, dispatching GetEntityDetailsQuery via IQueryMediator, returning Results.Json with 200 OK in src/CososWebScraping.Api/Features/EntityDetails/Endpoints/EntityDetailsEndpoints.cs — follow BusinessSearchEndpoints pattern with .Produces/.ProducesProblem for 400, 502, 503
- [X] T021 [US1] Add DI registrations in src/CososWebScraping.Api/Program.cs: AddKeyedSingleton<SemaphoreSlim>("EntityDetailSemaphore") with configurable count from EntityDetails:MaxConcurrentRequests, AddSingleton<EntityDetailAuditRepository>, AddSingleton<PlaywrightEntityDetailScraper>, AddSingleton<IEntityDetailScraper>, AddScoped<GetEntityDetailsHandler>, AddMeter(EntityDetailsMetrics.MeterName), and app.MapEntityDetailsEndpoints()

**Checkpoint**: Entity details retrieval is fully functional. Callers can GET entity details with SSRF validation, concurrency control, and audit trail. Certificate section shows `available: true/false` but no download yet. All US1 unit and integration tests pass GREEN.

---

## Phase 4: User Story 2 — Download Certificate of Good Standing PDF (Priority: P2)

**Goal**: When the certificate link is present and cloud storage is enabled, click the link, download the PDF via Playwright download event, upload to blob storage with Polly retry, and return the storage URL in the response and audit trail.

**Independent Test**: Call the endpoint with `masterFileId=20051063025` (Good Standing entity). Verify the response includes `certificate.storageUrl` pointing to `{correlationId}/20051063025-certificateofgoodstanding.pdf` in blob storage.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T022 [P] [US2] Unit test: Extend GetEntityDetailsHandlerTests.cs — certificate upload on success populates CertificateResult.StorageUrl and FileName, certificate upload failure still returns entity details (FR-009 graceful degradation), CertificateUrl persisted to EntityDetailAuditEntry on success, Polly retry invoked on transient upload failure — in tests/CososWebScraping.UnitTests/Features/EntityDetails/GetEntityDetailsHandlerTests.cs
- [X] T023 [P] [US2] Unit test: Extend EntityDetailScrapingTests.cs — certificate PDF download via Playwright download event, downloaded file content-type validated as application/pdf (reject non-PDF HTML error pages per edge case), CertificateLocalPath populated on successful download, CertificateLocalPath is null on download failure, document number validated as alphanumeric before filename construction — in tests/CososWebScraping.UnitTests/Features/EntityDetails/EntityDetailScrapingTests.cs

### Implementation for User Story 2

- [X] T024 [P] [US2] Extend PlaywrightEntityDetailScraper with PDF download: when certificate link is detected, register download event listener, click "Get a certificate of good standing" link, await download event, validate downloaded content is PDF (check suggestedFilename ends in .pdf or content headers; if non-PDF detected, log warning and set CertificateLocalPath=null), validate document number is alphanumeric, call download.SaveAsAsync to temp path `{diagnosticsOutputPath}/{correlationId}/{idNumber}-certificateofgoodstanding.pdf`, set CertificateLocalPath on EntityDetailScrapeResult in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightEntityDetailScraper.cs per research.md §R-001
- [X] T025 [P] [US2] Extend GetEntityDetailsHandler with certificate upload: when scrapeResult.CertificateLocalPath is not null, upload via IBlobStorageClient.UploadAsync to `{correlationId}/{idNumber}-certificateofgoodstanding.pdf` wrapped in a Polly ResiliencePipeline (3x retry with exponential backoff matching existing upload pattern), populate CertificateResult.StorageUrl and CertificateResult.FileName, persist CertificateUrl to EntityDetailAuditEntry. On upload failure, catch exception, set CertificateResult.Error, and continue returning entity details (FR-009) in src/CososWebScraping.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs per research.md §R-006

**Checkpoint**: Certificate download and upload is functional with retry resilience. Entity details are still returned even if certificate download fails (FR-009). Audit trail includes CertificateUrl when download succeeds (FR-011). All US2 tests pass GREEN.

---

## Phase 5: User Story 3 — Diagnostics for Entity Detail Scraping (Priority: P3)

**Goal**: Produce diagnostic artifacts (screenshots at milestones, error screenshots, traces) following the same conventions as the existing search feature, organized by correlation ID.

**Independent Test**: Call the endpoint with diagnostics enabled. Verify screenshots are captured at key milestones (page load, post-scrape, certificate download) and saved to the correlation-ID folder locally and in cloud storage.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T026 [US3] Unit test: Extend EntityDetailScrapingTests.cs — screenshot captured at detail page load milestone, screenshot captured after table scraping, screenshot captured after certificate download (when applicable), error screenshot force-captured on scraping failure, diagnostic artifacts uploaded to cloud storage when enabled — in tests/CososWebScraping.UnitTests/Features/EntityDetails/EntityDetailScrapingTests.cs

### Implementation for User Story 3

- [X] T027 [US3] Add diagnostic artifacts to PlaywrightEntityDetailScraper: capture screenshots at milestones (detail page loaded, after table scraping, after certificate download) via existing CaptureScreenshotAsync, capture error screenshot and trace on failure via ExecuteWithDiagnosticsAsync, upload all artifacts to cloud storage via TryUploadDiagnosticArtifactAsync in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightEntityDetailScraper.cs — reuse existing diagnostics infrastructure from PlaywrightBusinessEntityScraper

**Checkpoint**: All three user stories complete. Diagnostic artifacts produced for every request per SC-005. All US3 tests pass GREEN.

---

## Phase 6: Integration & End-to-End Tests

**Purpose**: Full-stack API contract validation and live site verification

- [X] T028 [US1] Integration test: Create EntityDetailsEndpointTests.cs with WebApplicationFactory — 200 OK for valid CO SOS URL (mocked scraper), 400 Bad Request for SSRF-rejected URL, 503 Service Unavailable when semaphore exhausted (Retry-After header present), response JSON matches EntityDetailResponse schema from contracts/openapi.yaml — in tests/CososWebScraping.IntegrationTests/Features/EntityDetails/EntityDetailsEndpointTests.cs
- [X] T029 E2E test: Create EntityDetailRetrievalTests.cs with live Playwright against CO SOS — retrieve entity details for Good Standing entity (20051063025, verify certificate available), retrieve entity details for Delinquent entity (20181871974, verify certificate not available), verify all 9 detail fields + 3 registered agent fields populated — in tests/CososWebScraping.EndToEndTests/Scenarios/EntityDetails/EntityDetailRetrievalTests.cs

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end validation and final verification

- [X] T030 Run quickstart.md validation: build solution, start API, execute curl commands from specs/004-entity-details-endpoint/quickstart.md against both test URLs (Good Standing entity 20051063025 and Delinquent entity 20181871974), verify JSON responses match expected schema from contracts/openapi.yaml, verify response times are within SC-001 (< 30s) and SC-002 (< 45s) performance thresholds

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational phase completion. Tests FIRST (T013-T017), then implementation (T018-T021).
- **User Story 2 (Phase 4)**: Depends on User Story 1 completion (extends scraper and handler from US1). Tests FIRST (T022-T023), then implementation (T024-T025).
- **User Story 3 (Phase 5)**: Depends on User Story 2 completion (adds diagnostics to scraper from US2). Tests FIRST (T026), then implementation (T027).
- **Integration & E2E (Phase 6)**: Depends on all user stories being complete
- **Polish (Phase 7)**: Depends on Phase 6 being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **User Story 2 (P2)**: Depends on US1 (extends PlaywrightEntityDetailScraper and GetEntityDetailsHandler created in US1)
- **User Story 3 (P3)**: Depends on US2 (adds diagnostics to the same scraper file that US2 modifies). US3 is conceptually independent but serialized due to shared file edits to avoid merge conflicts.

### Within Each Phase

- Tasks marked [P] can run in parallel with other [P] tasks in the same phase
- Non-[P] tasks must execute after their dependencies:
  - T008, T009 depend on T005 (models)
  - T010 depends on T003 (entity)
  - T011 depends on T004 (exception)
  - T012 depends on T005 (models)
  - T013-T017 (tests) run before T018-T021 (implementation) — TDD red-green
  - T021 depends on T018, T019, T020 (concrete types for DI wiring)
  - T022-T023 (tests) run before T024-T025 (implementation)
  - T026 (test) runs before T027 (implementation)

### Parallel Opportunities

**Phase 2 — 6 tasks in parallel:**
```text
T002 ──┐
T003 ──┤
T004 ──┤  All [P]: different files, no interdependencies
T005 ──┤
T006 ──┤
T007 ──┘
       │
       ▼
T008 ──┐  Depend on T005 (models)
T009 ──┤
T010 ──┤  T010 depends on T003 (entity)
T011 ──┤  T011 depends on T004 (exception)
T012 ──┘  T012 depends on T005 (models)
```

**Phase 3 — TDD cycle:**
```text
T013 ──┐
T014 ──┤
T015 ──┤  All [P] [US1] tests: write first, verify RED
T016 ──┤
T017 ──┘
       │
       ▼
T018 ──┐
T019 ──┤  All [P] [US1] implementation: make tests GREEN
T020 ──┘
       │
       ▼
T021 ──── DI wiring: depends on all concrete types from T018-T020
```

**Phase 4 — TDD cycle:**
```text
T022 ──┐  [P] [US2] tests: write first, verify RED
T023 ──┘
       │
       ▼
T024 ──┐  [P] [US2] implementation: make tests GREEN
T025 ──┘
```

**Phase 6 — 2 tests in parallel:**
```text
T028 ──┐  Integration test (WebApplicationFactory, mocked scraper)
T029 ──┘  E2E test (live Playwright against CO SOS)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (tests RED → implementation GREEN)
4. **STOP and VALIDATE**: All US1 tests pass, endpoint returns entity details
5. Deploy/demo if ready — callers can retrieve structured entity details

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 (tests → impl) → All US1 tests GREEN → Deploy/Demo (MVP!)
3. Add User Story 2 (tests → impl) → All US2 tests GREEN → Deploy/Demo (certificate downloads)
4. Add User Story 3 (tests → impl) → All US3 tests GREEN → Deploy/Demo (full diagnostics)
5. Add Integration & E2E tests → Full-stack validation
6. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same phase
- [Story] label maps task to specific user story for traceability
- **Tests MUST be written before implementation per Constitution Principle I (Test-First, NON-NEGOTIABLE)**
- Each user story should be independently completable and testable at its checkpoint
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All models, interfaces, and infrastructure are in Phase 2 to ensure clean compilation before story work begins
- Certificate download (US2) extends the scraper from US1 — the initial scraper detects the cert link but does not download
- Diagnostics (US3) layers onto the scraper after US2 to avoid merge conflicts in the same file (conceptually independent, serialized for pragmatic reasons)
- Performance thresholds SC-001 (30s) and SC-002 (45s) are enforced by the existing RequestTimeoutMiddleware and validated during quickstart.md validation (T030). CD pipeline k6 scenarios provide ongoing enforcement.
