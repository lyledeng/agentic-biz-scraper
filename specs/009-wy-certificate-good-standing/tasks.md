# Tasks: WY Certificate of Good Standing

**Input**: Design documents from `/specs/009-wy-certificate-good-standing/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Tests are included per Constitution Principle I (Test-First / TDD).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup & Foundational

**Purpose**: Extend the FlowAction model with a `Group` property, declare the `certificateLocalPath` variable, add Filing ID validation, and build the scraper-level certificate orchestration. MUST be complete before certificate flow actions can execute.

- [X] T001 [P] Add optional `Group` property (type `string?`) to `FlowAction` model in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowAction.cs and register it in `FlowDefinitionJsonContext` for JSON deserialization. Existing flows without `group` continue to work unchanged.
- [X] T002 [P] Add `certificateLocalPath` variable declaration to the `variables` array in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json (add `{ "name": "certificateLocalPath", "source": "extracted" }` alongside existing variables)
- [X] T003 Add `[GeneratedRegex]` source-generated partial method for WY Filing ID validation pattern `^\d{4}-\d{9}$` in src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs per research.md §R-005
- [X] T004 Add certificate retrieval orchestration to `JsonDrivenEntityDetailScraper.ScrapeAsync()`: after the main flow + history document download completes and before `MapToResult()`, (1) split `definition.Actions` into main actions (no group) and certificate actions (`group == "certificate"`), (2) validate the Filing ID format using T003 regex — skip with warning if invalid, (3) execute certificate actions via `_engine.ExecuteActionsAsync(certActions, actionContext, certCts.Token)` with an independent `CancellationTokenSource(TimeSpan.FromSeconds(15))`, (4) wrap in try-catch catching `PlaywrightException`, `TimeoutException`, `OperationCanceledException`, and generic `Exception` — log structured warning with correlation ID, Filing ID, and exception message, ensure `certificateLocalPath` remains null so handler returns `certificate: null` — in src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs per research.md §R-002, §R-004

**NOTE**: The main flow execution (`_engine.ExecuteAsync`) must be called with only the non-grouped actions. Refactor the call to filter: `definition with { Actions = mainActions }` or call `ExecuteActionsAsync` directly for the main actions.

**Checkpoint**: Scraper infrastructure ready — certificate actions in wy-entity-details.json will be executed with independent timeout.

---

## Phase 2: User Story 1 — Retrieve WY Certificate of Good Standing (Priority: P1) MVP

**Goal**: WY entity details responses include a populated `certificate` field with storage URL and filename when the entity is eligible for a Certificate of Good Standing.

**Independent Test**: Call `GET /api/v1/entity-details?detailsUrl={wyUrl}&state=WY` for Filing ID `2017-000757675` and verify `certificate.available: true`, `certificate.storageUrl` populated, `certificate.fileName` = `2017-000757675-certificateofgoodstanding.pdf`.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T005 [P] [US1] Unit test: Filing ID regex validation — valid IDs (`2017-000757675`, `2000-000123456`) pass, invalid IDs (`ABC-123`, `2017000757675`, `2017-0007576`, empty, null) fail — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/WyCertificateFlowTests.cs
- [X] T006 [P] [US1] Unit test: Certificate timeout behavior — when certificate flow exceeds 15s, `certificateLocalPath` stays null and entity details are returned without error — mock `ScrapingFlowEngine` to delay, verify `CertificateAvailable` is false on the scrape result — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/WyCertificateFlowTests.cs
- [X] T007 [P] [US1] Unit test: Certificate flow skipped when Filing ID is invalid — scraper logs warning and `certificateLocalPath` remains null — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/WyCertificateFlowTests.cs
- [X] T008 [P] [US1] Unit test: Existing `GetEntityDetailsHandler` certificate upload logic — verify that when `CertificateAvailable` is true and `CertificateLocalPath` is set, the handler uploads to blob storage and populates `CertificateResult.StorageUrl`, `FileName`, and persists certificate URL to audit trail (reuse existing test patterns from CO) — in tests/BizScraper.UnitTests/Features/EntityDetails/WyCertificateHandlerTests.cs

### Implementation for User Story 1

- [X] T009 [US1] Append certificate actions to src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json with `"group": "certificate"` on each action. Actions in sequence: (1) `navigate` to `https://wyobiz.wyo.gov/Business/ViewCertificate.aspx` with `waitUntil: networkidle`, (2) `screenshot` milestone `wy-cert-01-search-page`, (3) `fill` selector `#MainContent_txtFilingID` with value `${idNumber}`, (4) `click` selector `#MainContent_btnSearch` with `waitAfter: networkidle`, (5) `screenshot` milestone `wy-cert-02-verification-page`, (6) `download` with `linkSelector: "button:has-text('Print')"`, `validateExtension: ".pdf"`, `filenamePattern: "${idNumber}-certificateofgoodstanding.pdf"`, `storeAs: "certificateLocalPath"`, (7) `screenshot` milestone `wy-cert-03-after-download` — per research.md §R-001, §R-003, data-model.md flow table. All 7 actions have `"group": "certificate"`.
- [X] T010 [US1] Verify the scraper orchestration (T004) correctly passes the same `ActionContext` (page, browser context, variables including `idNumber`, `correlationId`, and `__diagnosticsDir`) to `ExecuteActionsAsync` for the certificate group, so screenshots land in the same correlation folder and the download handler writes to the correct diagnostics directory — in src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs

**Checkpoint**: At this point, calling `GET /api/v1/entity-details?detailsUrl={wyUrl}&state=WY` for an eligible entity should return `certificate.available: true` with a valid `storageUrl`. Existing CO certificate and WY entity details (without certificate) must still work.

---

## Phase 3: User Story 2 — Graceful Failure When Certificate Is Unavailable (Priority: P2)

**Goal**: Entity details are always returned regardless of certificate outcome — timeout, navigation error, ineligible entity, or upload failure never block the response.

**Independent Test**: Call the endpoint for a WY entity not in good standing (or simulate a certificate page failure) and verify entity details are fully returned with `certificate` as `null` or `certificate.error` populated.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T011 [P] [US2] Unit test: Certificate page navigation failure (e.g., `PlaywrightException`) — entity details returned, `certificate` is `null`, warning logged — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/WyCertificateFlowTests.cs
- [X] T012 [P] [US2] Unit test: Download action returns non-PDF file — `certificateLocalPath` remains null, warning logged — verify via mock flow that sets `DownloadActionHandler` return with invalid extension — in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/WyCertificateFlowTests.cs
- [X] T013 [P] [US2] Unit test: Cloud storage upload failure — `certificate.available` is `true`, `certificate.error` contains upload error message, entity details still returned — reuse existing handler test patterns from CO certificate upload failure — in tests/BizScraper.UnitTests/Features/EntityDetails/WyCertificateHandlerTests.cs

### Implementation for User Story 2

- [X] T014 [P] [US2] Add `[LoggerMessage]` source-generated log methods for certificate flow events: `CertificateFlowStarted` (EventId 2012), `CertificateFlowCompleted` (EventId 2013), `CertificateFlowFailed` (EventId 2014), `CertificateFlowSkippedInvalidFilingId` (EventId 2015), `CertificateFlowTimedOut` (EventId 2016) — in src/BizScraper.Api/Features/EntityDetails/Logging/EntityDetailsLogMessages.cs (append to existing class, continue EventId sequence after 2011)

**Checkpoint**: At this point, certificate failures of any kind never block entity details. All US1 and US2 tests pass.

---

## Phase 4: User Story 3 — Diagnostics for Certificate Retrieval Flow (Priority: P3)

**Goal**: Certificate retrieval produces screenshots at key milestones for operational troubleshooting.

**Independent Test**: Trigger a WY certificate retrieval with diagnostics enabled and verify screenshot files exist for milestones `wy-cert-01-search-page`, `wy-cert-02-verification-page`, `wy-cert-03-after-download`.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T015 [P] [US3] Unit test: Verify the `wy-entity-details.json` flow definition contains exactly 3 screenshot actions within the `"group": "certificate"` actions, with milestone names `wy-cert-01-search-page`, `wy-cert-02-verification-page`, `wy-cert-03-after-download` — structural validation test in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/WyCertificateFlowTests.cs

### Implementation for User Story 3

Screenshot actions are already defined in T009 as part of the certificate group in `wy-entity-details.json`. The `ActionContext` propagation is verified in T010. No additional implementation task needed — US3 is fully covered by T009 + T010.

**Checkpoint**: All three milestones produce screenshot files under `{diagnosticsOutputPath}/{correlationId}/`.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Validate end-to-end, ensure regression safety, documentation.

- [X] T016 Verify existing CO entity details certificate flow is unaffected — run existing CO certificate unit tests and confirm they pass without modification (SC-004). Confirm CO `wy-entity-details.json` actions without `group` property execute identically.
- [X] T017 Verify existing WY entity details flow (details, registered agent, parties, history documents) works identically for entities that are not eligible for a certificate (SC-003)
- [X] T018 Run quickstart.md validation — execute the test scenario from specs/009-wy-certificate-good-standing/quickstart.md against a running local instance to confirm the documented behavior matches actual output

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup & Foundational (Phase 1)**: No dependencies — can start immediately. BLOCKS all user stories.
- **User Story 1 (Phase 2)**: Depends on Phase 1 completion
- **User Story 2 (Phase 3)**: Depends on Phase 1, integrates with Phase 2 error handling
- **User Story 3 (Phase 4)**: Depends on T009 (certificate actions with screenshots defined in Phase 2)
- **Polish (Phase 5)**: Depends on all user stories complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation (Constitution Principle I)
- Flow definition changes before scraper integration
- Scraper integration before handler verification
- Story complete before moving to next priority

### Parallel Opportunities

- T001, T002 (setup) can run in parallel
- T005, T006, T007, T008 (US1 tests) can all run in parallel
- T011, T012, T013 (US2 tests) can all run in parallel
- T014 (log messages) can run in parallel with US2 tests
- T015 (US3 test) can run in parallel with US2 tests

---

## Parallel Example: User Story 1

```text
# Phase 1 setup — can run in parallel:
T001: Add Group property to FlowAction model
T002: Add certificateLocalPath variable to wy-entity-details.json

# Phase 1 foundational — sequential:
T003: GeneratedRegex for Filing ID validation
T004: Certificate orchestration with group splitting + scoped CTS + try-catch

# Phase 2 tests — all can be written in parallel:
T005: Filing ID regex validation unit test
T006: Certificate timeout behavior unit test
T007: Certificate flow skipped for invalid Filing ID unit test
T008: Handler certificate upload logic unit test

# Phase 2 implementation — sequential within story:
T009: Append certificate actions to wy-entity-details.json
T010: Verify ActionContext propagation for certificate group
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup & Foundational (T001–T004)
2. Complete Phase 2: User Story 1 (T005–T010)
3. **STOP and VALIDATE**: Test that `certificate` field is populated for eligible WY entities
4. Deploy/demo if ready — this alone delivers the core value

### Incremental Delivery

1. Setup & Foundational → Infrastructure ready
2. User Story 1 → Certificate retrieval works → Deploy/Demo (**MVP!**)
3. User Story 2 → Graceful failure guaranteed → Deploy/Demo
4. User Story 3 → Diagnostics screenshots verified → Deploy/Demo
5. Polish → Regression validation complete

### Task Count Summary

| Phase | Tasks |
|-------|-------|
| Phase 1: Setup & Foundational | 4 |
| Phase 2: US1 (P1) | 6 (4 tests + 2 implementation) |
| Phase 3: US2 (P2) | 4 (3 tests + 1 implementation) |
| Phase 4: US3 (P3) | 1 (1 test, impl covered by T009/T010) |
| Phase 5: Polish | 3 |
| **Total** | **18** |
