# Tasks: WY Certificate of Good Standing Fix

**Input**: Design documents from `/specs/020-wy-cert-good-standing-fix/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No project initialization needed — existing project. Verify branch and build.

- [X] T001 Verify branch `020-wy-cert-good-standing-fix` is checked out and `dotnet build` succeeds

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Fix structural issues in the JSON definition that affect ALL user stories. Must complete before any story work.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T002 Fix duplicate action ordering in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-entity-details.json` — renumber action-009b from order 9 to order 10, then renumber action-010 through action-017 sequentially (11–18) to eliminate the duplicate order 9 conflict (FR-005)
- [X] T003 Add `certificateError` extracted variable to the `variables` array in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-entity-details.json` (data-model.md: New Variable)
- [X] T004 Add `certificateError` to the `output.compose` block in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-entity-details.json` — map `"certificateError": "certificateError"` alongside existing `"certificateUrl": "certificateLocalPath"` (data-model.md: Output Compose Update)
- [X] T005 Verify `dotnet build` succeeds and all existing unit tests pass after foundational changes

**Checkpoint**: JSON definition has correct ordering and certificate error plumbing. User story implementation can now begin.

---

## Phase 3: User Story 1 — Download Certificate for Eligible WY Entity (Priority: P1) 🎯 MVP

**Goal**: Fix the WY certificate flow so eligible entities (all standings = "Good") receive a populated `certificate` section with download URL in the v2 API response.

**Independent Test**: Call `POST /api/v2/execute-script` with `us-wy-entity-details` for Filing ID `2017-000757675`. Verify `data.certificate.available = true` with a valid `proxyUrl`.

### Implementation for User Story 1

- [X] T006 [US1] Add `element-exists` condition on the download action (action-017, now order 18 after renumbering) in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-entity-details.json` — add `"condition": { "type": "element-exists", "selector": "#MainContent_ContinueButton" }` matching the CO pattern in `us-co-entity-details.json` action-004 (FR-001)
- [X] T007 [US1] Add certificate **ineligibility-state** screenshot action after the certificate search click (action-016) in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-entity-details.json` — action-016 already captures milestone `wy-cert-02-verification-page` for eligible entities; this new action MUST capture a separate milestone `wy-cert-02-ineligibility-alert` when the Print button is absent, to support diagnostics for ineligible entities (FR-006). Do NOT duplicate the existing action-016 screenshot.
- [X] T008 [US1] Fix `MapWyDetails()` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs` — read `certificateUrl` and `certificateError` from the composed output and map to `CertificateSection`, mirroring the `MapCoDetails()` pattern: when `certificateUrl` is non-empty, return `CertificateSection { Available = true, Downloads = [...] }`; when `certificateError` is non-empty, return `CertificateSection { Available = false, Error = certificateError }`; otherwise return null (R7, data-model.md: Mapper Changes)
- [X] T009 [P] [US1] Add unit test `MapWyDetails_ReturnsCertificateSection_WhenCertificateUrlPresent` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs` — verify that WY output with a `certificateUrl` property maps to `CertificateSection.Available = true` with correct `Downloads` entry
- [X] T010 [P] [US1] Add unit test `MapWyDetails_ReturnsCertificateNull_WhenNoCertificateData` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs` — verify that WY output without `certificateUrl` or `certificateError` maps to `Certificate = null`
- [X] T011 [US1] Verify `dotnet test tests/BizScraper.UnitTests/` passes — all existing mapper tests plus new T009/T010 tests green

**Checkpoint**: WY entities in good standing now receive `certificate.available = true` with download URL in v2 response.

---

## Phase 4: User Story 2 — Report Ineligibility for Non-Good-Standing Entities (Priority: P2)

**Goal**: When an entity is not in good standing, return `certificate.available = false` with a descriptive error message instead of an empty certificate field.

**Independent Test**: Call `POST /api/v2/execute-script` with `us-wy-entity-details` for Filing ID `2015-000679087`. Verify `data.certificate.available = false` with error message containing "not in good standing".

### Implementation for User Story 2

- [X] T012 [US2] Add extract action (action-016b) after the certificate search click in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-entity-details.json` — insert a new extract action with `group: "certificate"` that runs JavaScript to detect the ineligibility alert text "Certificates may only be printed when the entity Standing is" and stores the error message in `certificateError` variable; assign it an order value between action-016 (search click) and action-017 (download) (FR-002, FR-003, R2)
- [X] T013 [P] [US2] Add unit test `MapWyDetails_ReturnsCertificateNotAvailable_WhenCertificateErrorPresent` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs` — verify that WY output with `certificateError` but no `certificateUrl` maps to `CertificateSection { Available = false, Error = "..." }`
- [X] T014 [US2] Verify `dotnet test tests/BizScraper.UnitTests/` passes — all mapper tests green including T013

**Checkpoint**: WY entities not in good standing now receive `certificate.available = false` with descriptive error in v2 response.

---

## Phase 5: User Story 3 — Graceful Failure for Certificate Flow Errors (Priority: P3)

**Goal**: Verify that certificate flow errors (timeouts, page errors) do not block entity details retrieval. The existing `JsonDrivenEntityDetailScraper` already wraps the certificate group in a try/catch with a 15-second independent timeout — this phase validates that behavior.

**Independent Test**: Verify existing entity details tests pass and certificate group errors are caught without blocking the main response.

### Implementation for User Story 3

- [X] T015 [US3] Review and verify the certificate group error handling in `src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs` — confirm the `group: "certificate"` actions are executed in a separate try/catch with a 15-second timeout, and that certificate errors populate `certificateError` instead of propagating (FR-007, FR-008)
- [X] T016 [P] [US3] Add unit test `MapWyDetails_ReturnsCertificateWithError_WhenBothUrlAndErrorPresent` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs` — verify that when both `certificateUrl` and `certificateError` are present (download succeeded but upload failed), the certificate section includes both the download reference and the error
- [X] T017 [US3] Verify entity details (details, registeredAgent, parties, documents) continue to be returned even when certificate section has an error — run `dotnet test tests/BizScraper.UnitTests/` and confirm all tests pass

**Checkpoint**: Certificate flow errors are gracefully handled and never block entity details retrieval.

---

## Phase 6: V1 Business-Search Endpoint Removal & Test Migration (FR-010, FR-011, FR-012)

**Goal**: Remove the last remaining v1 live endpoint (`/api/v1/business-search`), migrate all v1 tests to v2, and validate all entity-details definitions work via `/api/v2/execute-script`.

**Independent Test**: Verify `GET /api/v1/business-search` returns 404, and all migrated integration tests hit `/api/v2/execute-script` successfully.

### Implementation for V1 Removal

- [X] T018 Remove the `MapBusinessSearchEndpoints()` call from `src/BizScraper.Api/Program.cs` (line ~199: `app.MapBusinessSearchEndpoints();`) (FR-010, R5)
- [X] T019 Delete `src/BizScraper.Api/Features/BusinessSearch/Endpoints/BusinessSearchEndpoints.cs` (FR-010, R5)
- [X] T020 Remove the `using BizScraper.Api.Features.BusinessSearch.Endpoints;` import from `src/BizScraper.Api/Program.cs` (cleanup dead import after T018/T019)
- [X] T021 Verify `dotnet build src/BizScraper.Api/` succeeds after v1 endpoint removal — ensure no compile errors from orphaned references

### Test Migration (v1 → v2)

- [X] T022 Migrate `tests/BizScraper.IntegrationTests/Features/BusinessSearch/BusinessSearchEndpointTests.cs` — change `GET /api/v1/business-search?state=CO&name=Acme` to `POST /api/v2/execute-script` with `{ "definition": "us-co-business-search", "parameters": { "searchTerm": "Acme" } }` and update assertions for v2 response shape (FR-011)
- [X] T023 Migrate `tests/BizScraper.IntegrationTests/Features/BusinessSearch/MultiStateEndpointTests.cs` — change all `GET /api/v1/business-search?state=...&name=...` calls to `POST /api/v2/execute-script` with corresponding definition slugs (`us-co-business-search`, `us-wy-business-search`); update missing-state and unsupported-state tests to verify v2 validation behavior (FR-011)
- [X] T024 Migrate `tests/BizScraper.IntegrationTests/Features/BusinessSearch/ErrorResponseContractTests.cs` — change `GET /api/v1/business-search` calls to `POST /api/v2/execute-script` and update expected error response shapes for v2 ProblemDetails format (FR-011)
- [X] T025 Migrate `tests/BizScraper.IntegrationTests/Features/BusinessSearch/OpenApiContractTests.cs` — update to verify v2 execute-script endpoint is documented in OpenAPI spec; remove references to v1 business-search route (FR-011)
- [X] T026 Update `tests/BizScraper.IntegrationTests/Features/BusinessSearch/TestApplicationFactory.cs` to configure v2 execute-script infrastructure (GenericScriptScraper, FlowDefinitionLoader, etc.) instead of v1 IStateSearchScraper keyed services only (FR-011)
- [X] T027 Update `tests/BizScraper.IntegrationTests/Features/BusinessSearch/MultiStateTestApplicationFactory.cs` to configure v2 execute-script infrastructure alongside IStateSearchScraper registrations — ensure v2 endpoint resolves correctly (FR-011)
- [X] T028 Migrate `tests/k6/business-search.js` — change all `GET /api/v1/business-search?name=...` calls to `POST /api/v2/execute-script` with `us-co-business-search` definition; update response assertions for v2 shape; **fix preexisting bug**: add the required `state`/`definition` parameter that was missing from the v1 k6 script (FR-011)
- [X] T029 Verify `dotnet test tests/BizScraper.IntegrationTests/` passes — all migrated tests green for v2 (FR-012)
- [X] T029b Add integration test or manual validation step to verify `de-de-entity-details` works via `POST /api/v2/execute-script` with `{ "definition": "de-de-entity-details", ... }` — DE entity-details was never served via v1 but FR-012/SC-007 requires all 3 definitions to be validated end-to-end (FR-012). Verified: `de-de-entity-details.json` definition loads correctly via FlowDefinitionLoader; v2 execute-script route resolves DE definitions. Live E2E validation deferred to deployment (requires German Handelsregister access).
- [X] T030 Verify `dotnet test tests/BizScraper.UnitTests/` passes — no regressions from v1 removal

**Checkpoint**: V1 business-search endpoint removed. All tests migrated to v2 and passing.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Update documentation, current-specs snapshot, and validate the quickstart.

- [X] T031 [P] Update `specs/current-specs/contracts/deprecated-business-search.md` — change status from "Deprecated" to "REMOVED" and note it was removed in feature 020 (plan.md: V1 Endpoint Removal)
- [X] T032 [P] Update `specs/current-specs/contracts/deprecated-entity-details.md` — change status from "Deprecated" to "REMOVED" and note v1 entity-details was removed in feature 018, business-search v1 removed in feature 020
- [X] T033 [P] Update `specs/current-specs/spec.md` — add section or note under §3 (Entity Details) documenting that WY certificate flow now correctly populates the `CertificateSection` with download URL for good-standing entities and error message for ineligible entities; update §2 to note v1 business-search endpoint is removed (not just deprecated)
- [X] T034 [P] Update `specs/current-specs/spec.md` section on deprecated v1 endpoints — remove `/api/v1/business-search` from the deprecated list and add it to a "Removed" section alongside `/api/v1/entity-details`
- [X] T035 Run full test suite: `dotnet test` at solution root — confirm zero failures across unit, integration, and E2E tests
- [X] T036 Run `dotnet build -c Release` to verify release build compiles cleanly with TreatWarningsAsErrors
- [X] T037 Validate `specs/020-wy-cert-good-standing-fix/quickstart.md` test scenarios — verify curl commands match the updated API behavior

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — core certificate download fix
- **US2 (Phase 4)**: Depends on Phase 2 — can run in parallel with US1 (different actions in JSON definition)
- **US3 (Phase 5)**: Depends on Phase 3 and Phase 4 — validates graceful failure after both fixes are in place
- **V1 Removal (Phase 6)**: Depends on Phase 2 only — independent of US1/US2/US3 certificate work
- **Polish (Phase 7)**: Depends on all previous phases being complete

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational (Phase 2). No dependency on US2 or US3.
- **US2 (P2)**: Depends on Foundational (Phase 2). No dependency on US1, but logically follows it.
- **US3 (P3)**: Depends on US1 + US2 (validates combined behavior).
- **V1 Removal**: Independent of US1/US2/US3 — can be done in parallel with user story work.

### Within Each Phase

- JSON definition changes before mapper changes (definition provides the data, mapper consumes it)
- Mapper changes before unit tests
- All tests must pass before moving to next phase

### Parallel Opportunities

**Within Phase 3 (US1)**:
- T009 and T010 (unit tests) can run in parallel — different test methods, same file

**Within Phase 4 (US2)**:
- T012 (JSON definition) and T013 (unit test) can run in parallel — different files

**Across Phases**:
- Phase 3 (US1) and Phase 4 (US2) can be worked in parallel after Phase 2 completes
- Phase 6 (V1 Removal) can be worked in parallel with Phases 3–5
- Phase 7 tasks T031–T034 (docs) can all run in parallel

---

## Parallel Example: Phase 3 + Phase 6

```text
After Phase 2 (Foundational) completes:

Stream A (Certificate Fix):        Stream B (V1 Removal):
├── T006 (add condition)           ├── T018 (remove endpoint call)
├── T007 (add screenshot)          ├── T019 (delete endpoint file)
├── T008 (fix mapper)              ├── T020 (remove import)
├── T009 ─┬─ T010 (unit tests)    ├── T021 (verify build)
│          │                       ├── T022–T028 (migrate tests)
├── T011 (verify)                  └── T029–T030 (verify tests)
├── T012 (ineligibility extract)
├── T013 (unit test)
├── T014 (verify)
├── T015–T017 (US3 validation)
```

---

## Implementation Strategy

### MVP (Phase 1–3): Certificate Download Fix

Deliver the core bug fix: WY entities in good standing receive a populated certificate section. This is the minimum viable fix that resolves the reported issue.

### Incremental Delivery

1. **MVP**: Phases 1–3 (certificate download works for eligible entities)
2. **+Ineligibility**: Phase 4 (clear error messages for ineligible entities)
3. **+Resilience**: Phase 5 (validated graceful failure)
4. **+Cleanup**: Phase 6 (v1 removal, test migration)
5. **+Polish**: Phase 7 (documentation, current-specs)
