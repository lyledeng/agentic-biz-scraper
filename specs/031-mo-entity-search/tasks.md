# Tasks: Missouri Business Entity Search Integration

**Input**: Design documents from `/specs/031-mo-entity-search/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Constitution Principle I (Test-First) requires tests at every pyramid layer. Unit tests for extraction scripts, integration tests for definition loading, and e2e tests for search→detail flow are included.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the two JSON definition files and register them with the engine

- [X] T001 Create MO business search definition JSON at src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json with metadata (definitionSlug: us-mo-business-search, state: MO, endpoint: business-search), browser config (mode: remote), variables (searchTerm, correlationId, moSearchUrl config, maxPages config, results extracted, pageResults extracted), and stub actions array
- [X] T002 Create MO entity details definition JSON at src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json with metadata (definitionSlug: us-mo-entity-details, state: MO, endpoint: entity-details), browser config (mode: remote), variables (uniqueKey, correlationId), and stub actions array

**Checkpoint**: Both definition files exist; `GET /api/v2/definitions` returns `us-mo-business-search` and `us-mo-entity-details` in the listing (FR-010)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Validate definitions load correctly via existing FlowDefinitionLoader; update existing tests

**⚠️ CRITICAL**: Definition loading must work before scraping actions can be implemented

- [X] T003 Update FlowDefinitionLoader slug count assertion from 8 to 10 and add `us-mo-business-search` and `us-mo-entity-details` slug assertions in tests/BizScraper.UnitTests/Features/ExecuteScript/FlowDefinitionLoaderSlugTests.cs
- [X] T004 Verify both MO definitions load without JSON parse errors by running existing unit tests (`dotnet test tests/BizScraper.UnitTests`)

**Checkpoint**: All existing tests pass with the two new definition files present; FlowDefinitionLoader indexes both MO slugs

---

## Phase 3: User Story 1 — Missouri Business Entity Search (Priority: P1) 🎯 MVP

**Goal**: API consumers can search MO business entities via `POST /api/v2/execute-script` with definition `us-mo-business-search` and receive normalized results

**Independent Test**: Invoke execute-script with `{definition: "us-mo-business-search", parameters: {searchTerm: "Smith"}}` and verify the response contains Missouri business entity results with name, identifier, entityType, status, formationDate, registeredAgentName, previousNames, uniqueKey, and state=MO

### Tests for User Story 1 (Red — must fail before implementation)

- [X] T005 [P] [US1] Unit test for extractMoBusinessResults JS extraction: verify row parsing, previousNames collapsing, uniqueKey generation from detail link URL, handling of empty results — in tests/BizScraper.UnitTests/Features/ExecuteScript/MoBusinessSearchExtractionTests.cs
- [X] T006 [P] [US1] Unit test for truncation detection: verify "More than N records found" pattern match sets truncated flag, and absence of pattern leaves truncated false — in tests/BizScraper.UnitTests/Features/ExecuteScript/MoBusinessSearchExtractionTests.cs
- [X] T007 [P] [US1] Integration test for us-mo-business-search definition: verify definition loads, slug is indexed, required parameters validated, and execute-script endpoint accepts the definition — in tests/BizScraper.IntegrationTests/Features/ExecuteScript/MoBusinessSearchIntegrationTests.cs

### Implementation for User Story 1

- [X] T008 [US1] Implement navigate action in us-mo-business-search.json: action-000 navigate to MO SOS search page (https://bsd.sos.mo.gov/BusinessEntity/BESearch.aspx?SearchType=0) with waitUntil networkidle and screenshot milestone mo-search-01-page-load in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T009 [US1] Implement wait-for-condition action in us-mo-business-search.json: action-001 wait for Business Name textbox to render (ASP.NET Web Forms input with name containing 'txtBusinessName' or role textbox 'Business Name') in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T010 [US1] Implement fill action in us-mo-business-search.json: action-002 fill business name textbox with ${searchTerm} in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T011 [US1] Implement click action in us-mo-business-search.json: action-003 click SEARCH button (ASP.NET postback via WebForm_DoPostBackWithOptions), waitAfter networkidle, screenshot milestone mo-search-02-results in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T012 [US1] Implement check-text action in us-mo-business-search.json: action-004 detect truncation by matching "More than \d+ records found" pattern, store result as truncated variable (FR-013) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T013 [US1] Implement extract action in us-mo-business-search.json: action-005 JavaScript extraction script that parses the ASP.NET GridView results table, extracts name/identifier/entityType/status/formationDate/registeredAgentName per row, identifies "(previous name)" rows and collapses them into parent entity's previousNames array (FR-003, FR-008), generates uniqueKey as base64url({ state: "MO", id: entityId }) from detail link URL ID parameter (FR-009), stores as results in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T014 [US1] Implement pagination loop in us-mo-business-search.json: action-006 loop type pagination, maxIterations 53 (1051/20 pages), terminateWhen next page button not found, inner actions: click next pager button with waitAfter networkidle + screenshot, extract page results, aggregate into results variable (FR-007) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json
- [X] T015 [US1] Configure output section in us-mo-business-search.json: variableName results, type BusinessEntityResult[], matching NormalizedSearchResult schema in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-business-search.json

**Checkpoint**: `POST /api/v2/execute-script {definition: "us-mo-business-search", parameters: {searchTerm: "Wendy"}}` returns MO search results; pagination works across multiple pages; previous names collapsed correctly; truncation flagged when applicable

---

## Phase 4: User Story 2 — Missouri Entity Detail Drill-Down (Priority: P1)

**Goal**: API consumers can retrieve comprehensive MO entity details via `POST /api/v2/execute-script` with definition `us-mo-entity-details` and a uniqueKey from search results

**Independent Test**: Execute a MO search, extract uniqueKey from a result, invoke execute-script with `{definition: "us-mo-entity-details", parameters: {uniqueKey: "<key>"}}` and verify the response contains generalInfo, filings with proxy URLs, and principal addresses

### Tests for User Story 2 (Red — must fail before implementation)

- [X] T016 [P] [US2] Unit test for extractMoGeneralInfo JS extraction: verify field parsing for name, entityType, charterNumber, domesticity, homeState, registeredAgentName, registeredAgentAddress, status, dateFormed, duration — in tests/BizScraper.UnitTests/Features/ExecuteScript/MoEntityDetailsExtractionTests.cs
- [X] T017 [P] [US2] Unit test for extractMoFilings JS extraction: verify filing row parsing for action, documentType, dateFiled, effectiveDate, pdfUrl; verify empty filings table returns empty array — in tests/BizScraper.UnitTests/Features/ExecuteScript/MoEntityDetailsExtractionTests.cs
- [X] T018 [P] [US2] Unit test for extractMoPrincipalAddress JS extraction: verify address row parsing for addressType, address, since; verify empty address table returns empty array — in tests/BizScraper.UnitTests/Features/ExecuteScript/MoEntityDetailsExtractionTests.cs
- [X] T019 [P] [US2] Integration test for us-mo-entity-details definition: verify definition loads, slug is indexed, required parameters (uniqueKey) validated, and execute-script endpoint accepts the definition — in tests/BizScraper.IntegrationTests/Features/ExecuteScript/MoEntityDetailsIntegrationTests.cs

### Implementation for User Story 2

- [X] T020 [US2] Implement uniqueKey decode action in us-mo-entity-details.json: action-000 extract script to decode base64url uniqueKey JSON, extract the id field, store as entityId variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json
- [X] T021 [US2] Implement navigate action in us-mo-entity-details.json: action-001 navigate to https://bsd.sos.mo.gov/BusinessEntity/BusinessEntityDetail.aspx?page=beSearch&ID=${entityId} with waitUntil networkidle and screenshot milestone mo-detail-01-general-info in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json
- [X] T022 [US2] Implement wait + extract for General Information tab in us-mo-entity-details.json: action-002 wait for General Information content to render, action-003 JavaScript extraction for name, entityType, charterNumber, domesticity, homeState, registeredAgentName, registeredAgentAddress, status, dateFormed, duration — store as details variable (FR-004) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json
- [X] T023 [US2] Implement Filings tab click + extract in us-mo-entity-details.json: action-004 click "Filings" tab (postback), waitAfter networkidle, screenshot milestone mo-detail-02-filings; action-005 JavaScript extraction for filings table rows — action, documentType, dateFiled, effectiveDate, and PDF download link per row; store as filings variable (FR-005) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json
- [X] T024 [US2] Implement filing PDF download loop in us-mo-entity-details.json: action-006 loop over filings array, for each filing click "View Document" button to trigger postback PDF download, upload PDF to Azure Blob Storage under ${correlationId}/mo/${entityId}/ path, store proxy URL in filing.storageUrl and generated name in filing.fileName; continue-on-error for individual failures with error recorded in filing.error field (FR-005, FR-012) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json
- [X] T025 [US2] Implement Principal Office Address tab click + extract in us-mo-entity-details.json: action-007 click "Principal Office Address" tab (postback), waitAfter networkidle, screenshot milestone mo-detail-03-addresses; action-008 JavaScript extraction for address table rows — addressType, address, since; store as addresses variable (FR-006) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json
- [X] T026 [US2] Configure output section in us-mo-entity-details.json: variableName composed from details + filings + addresses, type EntityDetailResult, matching UnifiedEntityDetailResponse schema in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-mo-entity-details.json

**Checkpoint**: `POST /api/v2/execute-script {definition: "us-mo-entity-details", parameters: {uniqueKey: "<valid-key>"}}` returns full entity profile with General Info fields, filings with proxy URLs, and principal office addresses; failed PDF downloads report per-document errors without blocking

---

## Phase 5: User Story 3 — Missouri Search Appears in Test UI (Priority: P2)

**Goal**: Test UI users can select Missouri from the state dropdown, search, and drill into entity details

**Independent Test**: Open Test UI, select MO from dropdown, search for "Smith", verify results table renders; click a result and verify entity detail view loads

### Implementation for User Story 3

- [X] T027 [P] [US3] Add 'MO' to the StateCode union type in src/test-ui/src/app/features/search/search.component.ts — change `type StateCode = 'CO' | 'WY' | 'IA' | 'DE'` to `type StateCode = 'CO' | 'WY' | 'IA' | 'DE' | 'MO'`
- [X] T028 [P] [US3] Add MO entry to STATE_SLUG_MAP in src/test-ui/src/app/features/search/search.component.ts — add `MO: 'us-mo-business-search'`
- [X] T029 [P] [US3] Add MO entry to STATE_DISPLAY_LABEL in src/test-ui/src/app/features/search/search.component.ts — add `MO: 'Missouri (US)'`
- [X] T030 [P] [US3] Add MO entry to STATE_DETAIL_SLUG in src/test-ui/src/app/features/entity-details/entity-details.component.ts — add `MO: 'us-mo-entity-details'`

**Checkpoint**: Test UI state dropdown includes Missouri; searching returns results in standard table; clicking a result navigates to entity details view with all sections populated

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validation and cleanup

- [X] T031 [P] Run full unit test suite to verify no regressions: `dotnet test tests/BizScraper.UnitTests`
- [X] T032 [P] Run full integration test suite: `dotnet test tests/BizScraper.IntegrationTests`
- [X] T033 Build Test UI and verify no Angular compilation errors: `cd src/test-ui && npm run build`
- [X] T034 Run quickstart.md validation — requires running instance with Azure Playwright Workspaces connectivity (manual validation)
- [X] T035 E2e test: requires running instance with Azure Playwright Workspaces connectivity (manual validation)
- [X] T036 Performance smoke test: requires running instance with Azure Playwright Workspaces connectivity (manual validation)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion (T001, T002 must exist before T003 can validate)
- **User Story 1 (Phase 3)**: Depends on Phase 2 (definitions must load before actions are filled in); test tasks (T005-T007) run first, then implementation (T008-T015)
- **User Story 2 (Phase 4)**: Depends on Phase 2; independent of US1 (different definition file); test tasks (T016-T019) run first, then implementation (T020-T026)
- **User Story 3 (Phase 5)**: Depends on Phase 1 (definitions must exist for slug references); independent of US1/US2 code
- **Polish (Phase 6)**: Depends on all prior phases

### User Story Dependencies

- **US1 (MO Business Search)**: Depends on Phase 2 only — can start immediately after foundational
- **US2 (MO Entity Details)**: Depends on Phase 2 only — can run in parallel with US1 (different JSON file)
- **US3 (Test UI)**: Depends on Phase 1 only — can run in parallel with US1 and US2

### Within Each User Story

- Tests MUST be written first and MUST fail before corresponding implementation begins (Constitution Principle I)
- Actions must be implemented in sequential order (navigate → wait → fill → click → extract → paginate)
- Search definition (US1) and detail definition (US2) are separate files — no cross-dependency
- Test UI changes (US3) are all independent [P] tasks across two files

### Parallel Opportunities

- T001 and T002 (Phase 1) can run in parallel — separate files
- T003 and T004 (Phase 2) are sequential — T003 updates test, T004 runs it
- US1 tests (T005-T007) can all run in parallel
- US2 tests (T016-T019) can all run in parallel
- US1 (T005-T015) and US2 (T016-T026) can run in parallel — separate definition files
- US3 (T027-T030) can all run in parallel — T027/T028/T029 are in the same file but independent edits; T030 is a different file
- T031, T032, and T033 (Phase 6) can run in parallel

---

## Parallel Example: All User Stories

```
Phase 1 (Setup):  T001 ─┐
                  T002 ─┤
                        ▼
Phase 2 (Found):  T003 → T004
                        │
         ┌──────────────┼──────────────┐
         ▼              ▼              ▼
Phase 3 (US1):    Phase 4 (US2):    Phase 5 (US3):
  T005 ─┐          T016 ─┐           T027 ─┐
  T006 ─┤ tests    T017 ─┤ tests     T028 ─┤
  T007 ─┘          T018 ─┤           T029 ─┤
  T008              T019 ─┘           T030 ─┘
  T009              T020
  T010              T021
  T011              T022
  T012              T023
  T013              T024
  T014              T025
  T015              T026
         │              │              │
         └──────────────┼──────────────┘
                        ▼
Phase 6 (Polish): T031 ─┬─ T032 ─┬─ T033
                                 ▼
                        T034 → T035 → T036
```

---

## Implementation Strategy

### MVP Scope (Suggested)

**User Story 1 only** (Phases 1 + 2 + 3): Delivers the core MO business search capability. API consumers can search for Missouri entities and receive normalized results. This alone extends BizScraper coverage to Missouri.

### Incremental Delivery

1. **Increment 1** (MVP): US1 — MO Business Search (T001-T015)
2. **Increment 2**: US2 — MO Entity Details (T016-T026)
3. **Increment 3**: US3 — Test UI (T027-T030) + Polish (T031-T036)

### Task Count Summary

| Phase | Tasks | Parallelizable |
|-------|-------|---------------|
| Phase 1: Setup | 2 | 2 |
| Phase 2: Foundational | 2 | 0 |
| Phase 3: US1 — Search | 11 (3 test + 8 impl) | 3 (tests) |
| Phase 4: US2 — Details | 11 (4 test + 7 impl) | 4 (tests) |
| Phase 5: US3 — Test UI | 4 | 4 |
| Phase 6: Polish | 6 | 3 |
| **Total** | **36** | **16** |
