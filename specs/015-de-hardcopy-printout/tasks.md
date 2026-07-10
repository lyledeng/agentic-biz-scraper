# Tasks: German Hardcopy Printout Retrieval with FAB Translation

**Input**: Design documents from `/specs/015-de-hardcopy-printout/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. Each production task is preceded by its Red test(s) per Constitution Principle I (Test-First / Red-Green-Refactor).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- **🔴 RED**: Write failing test(s) first
- **🟢 GREEN**: Implement minimum code to pass
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Add Markdig dependency and FAB translator configuration

- [X] T001 Add Markdig 1.1.2 PackageReference to `Directory.Packages.props` and `src/BizScraper.Api/BizScraper.Api.csproj`
- [X] T002 [P] Create `FabDocumentTranslatorOptions` configuration class in `src/BizScraper.Api/Common/Configuration/FabDocumentTranslatorOptions.cs` with `EndpointUrl`, `AuthToken`, and `TimeoutSeconds` (default 60) properties
- [X] T003 [P] Add `FabDocumentTranslator` configuration section to `src/BizScraper.Api/appsettings.json` and `src/BizScraper.Api/appsettings.Development.json`
- [X] T004 Register `FabDocumentTranslatorOptions` via Options pattern in `src/BizScraper.Api/Program.cs` (`builder.Services.Configure<FabDocumentTranslatorOptions>(...)`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions and infrastructure services that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 Create `IDocumentTranslator` interface and `DocumentTranslationResult` record in `src/BizScraper.Api/Common/Interfaces/IDocumentTranslator.cs`
- [X] T006 [P] Create `FabTranslationRequest` and `FabTranslationInput` records in `src/BizScraper.Api/Infrastructure/DocumentTranslation/FabTranslationRequest.cs`
- [X] T007 [P] Create `FabTranslationResponse` and `FabTranslationOutput` records in `src/BizScraper.Api/Infrastructure/DocumentTranslation/FabTranslationResponse.cs`
- [X] T008 🔴 RED Write `FabDocumentTranslatorTests` in `tests/BizScraper.UnitTests/Infrastructure/DocumentTranslation/FabDocumentTranslatorTests.cs` — test: successful translation returns Markdown, 5xx response throws, timeout throws, empty/null translatedMarkdown throws, Polly retries on transient failure; all tests fail (no implementation yet)
- [X] T009 🟢 GREEN Implement `FabDocumentTranslator` service in `src/BizScraper.Api/Infrastructure/DocumentTranslation/FabDocumentTranslator.cs` — Flurl.Http POST with `x-authentication` header, Polly retry pipeline (2 attempts, 1s delay), validates non-null/non-empty `translatedMarkdown` response; T008 tests pass
- [X] T010 🔴 RED Write `MarkdownToPdfConverterTests` in `tests/BizScraper.UnitTests/Infrastructure/Pdf/MarkdownToPdfConverterTests.cs` — test: simple Markdown produces non-empty PDF bytes, pipe table renders, headings render, empty input throws; all tests fail
- [X] T011 🟢 GREEN Implement `MarkdownToPdfConverter` in `src/BizScraper.Api/Infrastructure/Pdf/MarkdownToPdfConverter.cs` — Markdig `Markdown.ToHtml()` with `UseAdvancedExtensions()`, wrap in HTML document with table/heading styles, render via Playwright `page.SetContentAsync()` + `page.PdfAsync()`; T010 tests pass
- [X] T012 Register `IDocumentTranslator`/`FabDocumentTranslator` and `MarkdownToPdfConverter` as services in `src/BizScraper.Api/Program.cs`
- [X] T013 [P] Update extraction JavaScript in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json` actions 004 and 012 to also extract the registration header from `span.fontWeightBold` inside each result row and store as `registrationId` in each result object
- [X] T014 Add `RegistrationId` field to `GermanyBusinessResult` record in `src/BizScraper.Api/Features/GermanySearch/Models/GermanyBusinessResult.cs`
- [X] T015 Update `SearchGermanyBusinessesHandler` mapping in `src/BizScraper.Api/Features/GermanySearch/Handlers/SearchGermanyBusinessesHandler.cs` to populate `RegistrationId` from the extracted registration header field

**Checkpoint**: Foundation ready — Markdig, FAB translator, PDF converter, and enhanced search extraction all in place. Germany search API now returns `registrationId` per result. User story implementation can begin.

---

## Phase 3: User Story 1 — Retrieve and Translate a German Hardcopy Printout (Priority: P1) 🎯 MVP

**Goal**: A caller can POST to `/api/v1/germany-search/current-hardcopy-printout` with a search term and registration ID, and receive an English-translated PDF of the German AD document.

**Independent Test**: `curl -k -X POST "https://localhost:8443/api/v1/germany-search/current-hardcopy-printout" -H "Content-Type: application/json" -d '{"searchTerm":"Claes und Rohde","registrationId":"Paderborn HRA 3059"}' --output hardcopy.pdf` → receive a PDF

### Implementation for User Story 1

- [X] T016 [P] [US1] Create `HardcopyPrintoutRequest` record in `src/BizScraper.Api/Features/GermanySearch/Models/HardcopyPrintoutRequest.cs` with `SearchTerm` and `RegistrationId` fields
- [X] T017 [P] [US1] Create `HardcopyPrintoutResult` record in `src/BizScraper.Api/Features/GermanySearch/Models/HardcopyPrintoutResult.cs` with `TranslatedPdf`, `TranslatedDocumentUrl`, `OriginalDocumentUrl`, and `CorrelationId` fields
- [X] T018 [P] [US1] Create `GetHardcopyPrintoutCommand` record in `src/BizScraper.Api/Features/GermanySearch/Commands/GetHardcopyPrintoutCommand.cs` implementing `ICommand<HardcopyPrintoutResult>` with `SearchTerm` and `RegistrationId`
- [X] T019 [P] [US1] Register `HardcopyPrintoutRequest` in `src/BizScraper.Api/Common/Serialization/AppJsonSerializerContext.cs` (note: `FabTranslationRequest` and `FabTranslationResponse` were registered in Phase 2 T006/T007)
- [X] T020 [P] [US1] Add hardcopy-specific log messages to `src/BizScraper.Api/Features/GermanySearch/Logging/GermanySearchLog.cs` — HardcopyStarted, HardcopyCompleted, HardcopyFailed, HardcopyAdDownloaded, HardcopyTranslationReceived (EventIds 1310-1319)
- [X] T021 [P] [US1] Add hardcopy-specific OTel metrics to `src/BizScraper.Api/Features/GermanySearch/Metrics/GermanySearchMetrics.cs` — HardcopyRequestStarted, HardcopyRequestCompleted, HardcopyRequestFailed counters and HardcopyDuration histogram
- [X] T022 🔴 RED [US1] Write `GetHardcopyPrintoutHandlerTests` in `tests/BizScraper.UnitTests/Features/GermanySearch/GetHardcopyPrintoutHandlerTests.cs` — test with mocked `IDocumentTranslator`, `MarkdownToPdfConverter`, `IBlobStorageClient`, `ISemaphoreManager`; scenarios: (a) happy path returns translated PDF bytes with blob URLs, (b) handler calls blob storage for both original and translated PDFs, (c) handler calls IDocumentTranslator with base64 PDF; all tests fail
- [X] T023 🟢 GREEN [US1] Implement `GetHardcopyPrintoutHandler` (part 1: navigation + download) in `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs` — acquire DE semaphore → search Handelsregister via scraping engine → locate matching registration row → click AD link via `page.waitForEvent('download')` → read PDF bytes → capture diagnostic screenshots at key points (search results, matched row, AD page) → upload original PDF to blob storage
- [X] T024 🟢 GREEN [US1] Implement `GetHardcopyPrintoutHandler` (part 2: translate + PDF + store) in `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs` — call `IDocumentTranslator.TranslateAsync()` with base64-encoded PDF → call `MarkdownToPdfConverter` → upload translated PDF to blob storage → return `HardcopyPrintoutResult`; T022 handler tests pass
- [X] T025 [US1] Register `GetHardcopyPrintoutHandler` in LiteBus command module in `src/BizScraper.Api/Program.cs`
- [X] T026 🔴 RED [US1] Write `HardcopyPrintoutEndpointTests` in `tests/BizScraper.UnitTests/Features/GermanySearch/HardcopyPrintoutEndpointTests.cs` — test: valid request returns 200 with PDF content-type and expected response headers (`X-Document-Url`, `X-Original-Document-Url`, `X-Correlation-Id`, `Content-Disposition`); all tests fail
- [X] T027 🟢 GREEN [US1] Add POST endpoint in `src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs` — dispatches `GetHardcopyPrintoutCommand` via `ICommandMediator`, returns `Results.File()` with `X-Document-Url`, `X-Original-Document-Url`, `X-Correlation-Id`, and `Content-Disposition` response headers; T026 tests pass

**Checkpoint**: Core hardcopy retrieval + translation + PDF generation works end-to-end. A caller can POST and receive a translated PDF. Diagnostic screenshots are captured. Audit is deferred to Phase 7.

---

## Phase 4: User Story 2 — Handle Missing or Unavailable Hardcopy (Priority: P1)

**Goal**: Callers receive clear, actionable error responses when a registration is not found or the AD link is unavailable.

**Independent Test**: POST with a registration ID that doesn't appear in search results → receive 404 with RFC 9457 Problem Details.

### Implementation for User Story 2

- [X] T028 🔴 RED [US2] Add error-path test cases to `GetHardcopyPrintoutHandlerTests` — test: no matching row → throws typed exception, AD link inactive → throws typed exception, empty searchTerm → throws, empty registrationId → throws; all new tests fail
- [X] T029 🟢 GREEN [US2] Add registration-not-found and AD-unavailable error handling in `GetHardcopyPrintoutHandler` in `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs` — throw typed exceptions for: no matching row found (→ 404), AD link inactive (→ 404); T028 tests pass
- [X] T030 🔴 RED [US2] Add endpoint error-path test cases to `HardcopyPrintoutEndpointTests` — test: 400 for empty fields, 404 for typed exceptions, 503 with Retry-After for semaphore full; all new tests fail
- [X] T031 🟢 GREEN [US2] Add request validation in POST endpoint in `src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs` — return 400 Problem Details when `searchTerm` or `registrationId` is empty/missing; map typed exceptions to 404 Problem Details; T030 tests pass
- [X] T032 🟢 GREEN [US2] Add 503 Service Unavailable with `Retry-After` header when semaphore is full in `GetHardcopyPrintoutHandler` in `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs`; T030 503 test passes
- [X] T033 🟢 GREEN [US2] Add 413 Content Too Large handling in `GetHardcopyPrintoutHandler` — enforce configurable maximum document size limit (`MaxDocumentSizeBytes` in `FabDocumentTranslatorOptions`); return 413 Problem Details when exceeded via endpoint mapping (FR-022); add corresponding test case in `HardcopyPrintoutEndpointTests`

**Checkpoint**: All error paths return proper RFC 9457 Problem Details with correct HTTP status codes (400, 404, 413, 503).

---

## Phase 5: User Story 3 — FAB Translation Failure Handling (Priority: P2)

**Goal**: FAB agent failures (5xx, timeout, malformed response) are handled with proper 502/504 responses and audit entries.

**Independent Test**: Simulate FAB returning 500 → system returns 502 with descriptive Problem Details.

### Implementation for User Story 3

- [X] T034 🔴 RED [US3] Add FAB-failure test cases to `GetHardcopyPrintoutHandlerTests` — test: FAB 5xx → handler stores original PDF and re-throws, FAB timeout → handler stores original PDF and re-throws, FAB empty response → handler stores original PDF and re-throws; all new tests fail
- [X] T035 🟢 GREEN [US3] Add FAB error handling in `GetHardcopyPrintoutHandler` in `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs` — catch `FlurlHttpException` (5xx → re-throw typed), `FlurlHttpTimeoutException` (→ re-throw typed), empty/null translation (→ throw typed); store original German PDF in blob even on translation failure; T034 tests pass
- [X] T036 🔴 RED [US3] Add FAB-failure endpoint test cases to `HardcopyPrintoutEndpointTests` — test: FAB typed exceptions → 502/ 504 Problem Details; tests fail
- [X] T037 🟢 GREEN [US3] Map FAB-specific exceptions to Problem Details responses in POST endpoint in `src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs` — 502 Bad Gateway for service errors, 504 Gateway Timeout for timeouts; T036 tests pass

**Checkpoint**: All FAB failure scenarios return descriptive errors. The original German PDF is preserved even when translation fails.

---

## Phase 6: User Story 4 — Test UI Hardcopy Printout Capability (Priority: P2)

**Goal**: The test-ui displays a `registrationId` column in search results and an "AD" button that triggers the hardcopy endpoint and opens the translated PDF.

**Independent Test**: In the test-ui, search for a company → results show `registrationId` column → click "AD" button → PDF opens in a new tab.

### Implementation for User Story 4

- [X] T038 [P] [US4] Add `registrationId` field to the Germany search result model in `src/test-ui/src/app/core/services/germany-search.service.ts` and add `getHardcopyPrintout(searchTerm: string, registrationId: string)` method that POSTs to `/api/v1/germany-search/current-hardcopy-printout` and returns a Blob
- [X] T039 [US4] Add "Registration" column to the Germany search results table and "AD" action button in `src/test-ui/src/app/features/germany-search/germany-search.component.html` — button shows loading state, disabled during request
- [X] T040 [US4] Add AD button click handler in `src/test-ui/src/app/features/germany-search/germany-search.component.ts` — calls `getHardcopyPrintout()`, creates a Blob URL from the response, opens in new tab via `window.open()`, shows error banner on failure

**Checkpoint**: Test-ui enables manual testing of the full hardcopy retrieval workflow.

---

## Phase 7: User Story 5 — Audit Trail for Hardcopy Requests (Priority: P3)

**Goal**: Every hardcopy request (success or failure) is persisted to the audit trail with full traceability.

**Independent Test**: Call the endpoint → verify an audit record exists with search term, registration ID, outcome, duration, and document URL.

### Implementation for User Story 5

- [X] T041 🔴 RED [US5] Write `HardcopyPrintoutIntegrationTests` in `tests/BizScraper.IntegrationTests/Features/GermanySearch/HardcopyPrintoutIntegrationTests.cs` — test: successful request writes audit entry with `HardcopySuccess` status, correlation ID, document URL, and duration; failed request writes audit entry with `HardcopyError` status and error detail; use Testcontainers.MsSql; all tests fail
- [X] T042 🟢 GREEN [US5] Add audit trail persistence in `GetHardcopyPrintoutHandler` in `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs` — write `SearchAuditEntry` with `HardcopySuccess`/`HardcopyError` status, searchTerm combined with registrationId, correlation ID, document URL, and duration; ensure audit is written in both success and all error paths (in `finally` or catch blocks); T041 integration tests pass

**Note**: Diagnostic screenshot capture was implemented in T023 (Phase 3) as part of the handler's navigation flow. T042 adds the audit persistence that references those screenshot URLs.

**Note**: FR-018 (FAB system prompt configuration) is an external manual step — the FAB dashboard must be configured per `contracts/fab-document-translator-config.md`. No automated task applies; verify manually before E2E testing.

**Checkpoint**: Full audit traceability — every request leaves an audit record and diagnostic screenshots.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation

- [X] T043 Build solution and verify zero warnings with `dotnet build BizScraper.slnx`
- [X] T044 Run full test suite with `dotnet test BizScraper.slnx` and verify all existing tests still pass (188+) plus all new tests
- [X] T045 [P] Create k6 functional test in `tests/k6/germany-hardcopy-printout.js` — POST to hardcopy endpoint, verify 200 with PDF content-type, verify response headers present, assert end-to-end duration < 90s (SC-001)
- [X] T046 Run quickstart.md validation — execute the curl example from `specs/015-de-hardcopy-printout/quickstart.md` and confirm PDF is returned

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (T001 for Markdig, T004 for options registration) — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2) completion
- **User Story 2 (Phase 4)**: Depends on US1 (Phase 3) — adds error handling to the handler/endpoint created in US1
- **User Story 3 (Phase 5)**: Depends on US1 (Phase 3) — adds FAB-specific error handling
- **User Story 4 (Phase 6)**: Depends on Foundational (Phase 2) — specifically T013-T015 (search returns registrationId) and US1 (Phase 3) — the API endpoint must exist
- **User Story 5 (Phase 7)**: Depends on US1 (Phase 3) — adds audit to the handler created in US1
- **Polish (Phase 8)**: Depends on all stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational — core happy path
- **US2 (P1)**: Depends on US1 — error paths added to US1's handler/endpoint
- **US3 (P2)**: Depends on US1 — FAB error paths added to US1's handler/endpoint
- **US4 (P2)**: Depends on Foundational (registrationId in search) + US1 (hardcopy endpoint exists)
- **US5 (P3)**: Depends on US1 — audit added to US1's handler

### Within Each User Story (TDD Cycle)

- 🔴 RED test tasks precede their corresponding 🟢 GREEN implementation tasks
- Models/records before handlers (no test needed — pure data)
- Logging/metrics can parallel with models (no test needed — infrastructure plumbing)
- Handler tests → handler implementation → endpoint tests → endpoint implementation

### Parallel Opportunities

- T002, T003 can run in parallel (Setup phase — different files)
- T006, T007, T013 can run in parallel (Foundational — different files)
- T016, T017, T018, T019, T020, T021 can run in parallel (US1 models/logging/metrics — different files)
- T038 can run in parallel with US1 implementation (US4 service layer — different project)
- US3 and US4 can run in parallel after US1 completes (different code areas)
- US5 can run in parallel with US3/US4

---

## TDD Example: FabDocumentTranslator

```
# Step 1 — RED: Write failing tests (T008)
tests/BizScraper.UnitTests/Infrastructure/DocumentTranslation/FabDocumentTranslatorTests.cs
  ✗ TranslateAsync_ValidPdf_ReturnsMarkdown
  ✗ TranslateAsync_5xxResponse_Throws
  ✗ TranslateAsync_Timeout_Throws
  ✗ TranslateAsync_EmptyTranslation_Throws

# Step 2 — GREEN: Implement minimum code (T009)
src/BizScraper.Api/Infrastructure/DocumentTranslation/FabDocumentTranslator.cs
  ✓ TranslateAsync_ValidPdf_ReturnsMarkdown
  ✓ TranslateAsync_5xxResponse_Throws
  ✓ TranslateAsync_Timeout_Throws
  ✓ TranslateAsync_EmptyTranslation_Throws
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T004)
2. Complete Phase 2: Foundational (T005-T015) — includes search enhancement + TDD for translator/converter
3. Complete Phase 3: User Story 1 (T016-T027) — TDD for handler + endpoint
4. **STOP and VALIDATE**: Test US1 with curl — confirm PDF returned
5. Proceed to US2 (error handling) and US3 (FAB resilience)

### Incremental Delivery

- **After Phase 3**: Core API works — caller can translate any AD document
- **After Phase 4**: Error handling complete — useful error messages for all failure modes (400, 404, 413, 503)
- **After Phase 5**: FAB resilience — production-ready external dependency handling (502, 504)
- **After Phase 6**: Test-ui works — manual testing capability
- **After Phase 7**: Audit trail — compliance and debugging support
- **After Phase 8**: Polish — all tests pass, k6 validated (including SC-001 90s timing), quickstart confirmed
