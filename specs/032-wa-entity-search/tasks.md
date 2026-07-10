# Tasks: Washington Business Entity Search Integration

**Input**: Design documents from `/specs/032-wa-entity-search/`
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

- [x] T001 [P] Create WA business search definition JSON at src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json with metadata (definitionSlug: us-wa-business-search, name: "Washington Business Search", state: WA, endpoint: business-search), browser config (mode: remote), variables (searchTerm runtime required, correlationId runtime required, waSearchUrl config defaultValue "https://ccfs.sos.wa.gov/#/", maxPages config defaultValue 25, results extracted, pageResults extracted, isLastPage extracted), and stub actions array
- [x] T002 [P] Create WA entity details definition JSON at src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json with metadata (definitionSlug: us-wa-entity-details, name: "Washington Entity Details", state: WA, endpoint: entity-details), browser config (mode: remote), variables (uniqueKey runtime required, correlationId runtime required, maxFilingDocuments config defaultValue 10, details extracted, registeredAgent extracted, governors extracted, filings extracted), and stub actions array

**Checkpoint**: Both definition files exist; `GET /api/v2/definitions` returns `us-wa-business-search` and `us-wa-entity-details` in the listing (FR-012)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Validate definitions load correctly via existing FlowDefinitionLoader; update existing tests

**⚠️ CRITICAL**: Definition loading must work before scraping actions can be implemented

- [x] T003 Update FlowDefinitionLoader slug count assertion from 10 to 12 and add `us-wa-business-search` and `us-wa-entity-details` slug assertions in tests/BizScraper.UnitTests/Features/ExecuteScript/FlowDefinitionLoaderSlugTests.cs
- [x] T004 Verify both WA definitions load without JSON parse errors by running existing unit tests (`dotnet test tests/BizScraper.UnitTests`)

**Checkpoint**: All existing tests pass with the two new definition files present; FlowDefinitionLoader indexes both WA slugs

---

## Phase 3: User Story 1 — Washington Business Entity Search (Priority: P1) 🎯 MVP

**Goal**: API consumers can search WA business entities via `POST /api/v2/execute-script` with definition `us-wa-business-search` and receive normalized results with business name, UBI number, business type, principal office address, registered agent name, status, and uniqueKey

**Independent Test**: Invoke execute-script with `{definition: "us-wa-business-search", parameters: {searchTerm: "Starbucks"}}` and verify the response contains Washington business entity results in the normalized v2 format

### Tests for User Story 1 (Red — must fail before implementation)

- [x] T005 [P] [US1] Unit test for extractWaBusinessResults JS extraction: verify row parsing from CCFS search results table, extraction of name/identifier(UBI)/entityType/status/principalOfficeAddress/registeredAgentName per row, uniqueKey generation as base64url({ state: "US-WA", params: { businessId, businessType } }) from row attributes, handling of empty results — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaBusinessSearchExtractionTests.cs
- [x] T006 [P] [US1] Unit test for Cloudflare Turnstile detection: verify that presence of "challenges.cloudflare.com" or "Verify you are human" text triggers cloudflare-blocked error type — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaBusinessSearchExtractionTests.cs
- [x] T007 [P] [US1] Unit test for pagination: verify checkWaLastPage script returns truthy when no next page button exists, returns falsy when more pages available — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaBusinessSearchExtractionTests.cs
- [x] T008 [P] [US1] Integration test for us-wa-business-search definition: verify definition loads, slug is indexed, required parameters (searchTerm) validated, and execute-script endpoint accepts the definition — in tests/BizScraper.IntegrationTests/Features/ExecuteScript/WaBusinessSearchIntegrationTests.cs

### Implementation for User Story 1

- [x] T009 [US1] Implement navigate action in us-wa-business-search.json: action-000 navigate to WA CCFS SPA (${waSearchUrl}) with waitUntil networkidle and screenshot milestone wa-search-01-page-load in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T010 [US1] Implement Cloudflare Turnstile detection in us-wa-business-search.json: action-001 check-text for "challenges.cloudflare.com" or "Verify you are human" pattern with onMatch throw, errorType cloudflare-blocked (edge case: Turnstile challenge) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T011 [US1] Implement wait-for-condition action in us-wa-business-search.json: action-002 wait for AngularJS SPA to render search input field (input[type='text'] or ng-model bound search field), timeoutMs 10000 in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T012 [US1] Implement fill action in us-wa-business-search.json: action-003 fill search input with ${searchTerm} in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T013 [US1] Implement click action in us-wa-business-search.json: action-004 click Search button (button[type='submit'] or AngularJS search trigger), waitAfter networkidle, screenshot milestone wa-search-02-results in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T014 [US1] Implement wait-for-condition for results render in us-wa-business-search.json: action-005 wait for AngularJS to render search results table rows or "no results" message after API response completes, timeoutMs 15000 (SPA wait strategy per research R8) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T015 [US1] Implement extract action in us-wa-business-search.json: action-006 JavaScript extraction script that parses the CCFS search results table, extracts name/identifier(UBI)/entityType/status/principalOfficeAddress/registeredAgentName per row (FR-003), generates uniqueKey as base64url({ state: "US-WA", params: { businessId, businessType } }) by extracting businessID and businessType from row's onclick/href/ng-click attributes (FR-011), stores as results in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T016 [US1] Implement isLastPage check extract in us-wa-business-search.json: action-007 JavaScript that inspects pagination controls to determine if current page is last page (no next button or disabled next), stores as isLastPage variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T017 [US1] Implement pagination loop in us-wa-business-search.json: action-008 loop type pagination, maxIterations ${maxPages} (default 25), terminateWhen isLastPage is truthy, inner actions: click next page button with waitAfter networkidle + screenshot wa-search-03-page-${currentPage}, wait for AngularJS re-render, check isLastPage, extract page results into pageResults, aggregate into results variable (FR-010) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json
- [x] T018 [US1] Configure output section in us-wa-business-search.json: variableName results, type BusinessEntityResult[], matching NormalizedSearchResult schema (name, identifier, entityType, status, principalOfficeAddress, registeredAgentName, uniqueKey, state="WA", formationDate=null) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-business-search.json

**Checkpoint**: `POST /api/v2/execute-script {definition: "us-wa-business-search", parameters: {searchTerm: "Starbucks"}}` returns WA search results; pagination works across multiple pages; uniqueKey encodes businessId+businessType; Cloudflare Turnstile blocked if challenged

---

## Phase 4: User Story 2 — Washington Entity Detail Drill-Down (Priority: P1)

**Goal**: API consumers can retrieve comprehensive WA entity details via `POST /api/v2/execute-script` with definition `us-wa-entity-details` and a uniqueKey from search results, including general info, registered agent, governors/officers, and nonprofit fields

**Independent Test**: Execute a WA search, extract uniqueKey from a result, invoke execute-script with `{definition: "us-wa-entity-details", parameters: {uniqueKey: "<key>"}}` and verify the response contains general info, registered agent, governors, and filing history

### Tests for User Story 2 (Red — must fail before implementation)

- [x] T019 [P] [US2] Unit test for extractWaGeneralInfo JS extraction: verify field parsing for name, ubiNumber, businessType, status, principalOfficeStreetAddress, principalOfficeMailingAddress, expirationDate, jurisdiction, formationDate, periodOfDuration, inactiveDate, natureOfBusiness, and 6 nonprofit fields (charitableCorporation, nonprofitEin, grossRevenueUnder500K, hasMembers, publicBenefitDesignation, hostHome) — null for non-nonprofits — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaEntityDetailsExtractionTests.cs
- [x] T020 [P] [US2] Unit test for extractWaRegisteredAgent JS extraction: verify agent name, streetAddress, mailingAddress parsing; verify null registeredAgent when section is empty — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaEntityDetailsExtractionTests.cs
- [x] T021 [P] [US2] Unit test for extractWaGovernors JS extraction: verify governor row parsing for title, governorType (INDIVIDUAL/ENTITY), entityName, firstName, lastName; verify empty array when no governors listed — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaEntityDetailsExtractionTests.cs
- [x] T022 [P] [US2] Unit test for extractWaFilingHistory JS extraction: verify filing row parsing for filingNumber, filingDateTime, effectiveDate, filingType; verify empty array when no filing history — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaEntityDetailsExtractionTests.cs
- [x] T023 [P] [US2] Integration test for us-wa-entity-details definition: verify definition loads, slug is indexed, required parameters (uniqueKey) validated, and execute-script endpoint accepts the definition — in tests/BizScraper.IntegrationTests/Features/ExecuteScript/WaEntityDetailsIntegrationTests.cs

### Implementation for User Story 2

- [x] T024 [US2] Implement uniqueKey decode action in us-wa-entity-details.json: action-000 JavaScript to decode base64url uniqueKey JSON, extract businessId and businessType fields, store as keyParams variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T025 [US2] Implement navigate action in us-wa-entity-details.json: action-001 navigate to https://ccfs.sos.wa.gov/#/BusinessSearch/${keyParams.businessId} with waitUntil networkidle and screenshot milestone wa-detail-01-page-load in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T026 [US2] Implement Cloudflare Turnstile detection in us-wa-entity-details.json: action-002 check-text for Cloudflare challenge pattern with onMatch throw, errorType cloudflare-blocked in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T027 [US2] Implement wait-for-condition for entity detail render in us-wa-entity-details.json: action-003 wait for AngularJS to render entity detail content (business name element visible), timeoutMs 15000 (SPA wait strategy per research R8) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T028 [US2] Implement extract general info action in us-wa-entity-details.json: action-004 JavaScript extraction for name, ubiNumber, businessType, status, principalOfficeStreetAddress, principalOfficeMailingAddress, expirationDate, jurisdiction, formationDate, periodOfDuration, inactiveDate, natureOfBusiness, and 6 nonprofit-specific fields (FR-004); returns null for non-applicable fields; store as details variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T029 [US2] Implement extract registered agent action in us-wa-entity-details.json: action-005 JavaScript extraction for agent name, streetAddress, mailingAddress (FR-005); store as registeredAgent variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T030 [US2] Implement extract governors action in us-wa-entity-details.json: action-006 JavaScript extraction for governors/officers table — title, governorType (INDIVIDUAL/ENTITY), entityName, firstName, lastName per row (FR-006); empty array for entities with no governors; store as governors variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T031 [US2] Implement extract filing history action in us-wa-entity-details.json: action-007 JavaScript extraction for filing history table — filingNumber, filingDateTime, effectiveDate, filingType per row (FR-007); empty array for entities with no filings; store as filings variable in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T032 [US2] Configure output section in us-wa-entity-details.json: variableName composed from details + registeredAgent + governors + filings, type WashingtonEntityDetails, with compose map matching data-model.md entity relationships in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json. Verify nameHistory is absent from the output schema (FR-014: out of scope for initial implementation)

**Checkpoint**: `POST /api/v2/execute-script {definition: "us-wa-entity-details", parameters: {uniqueKey: "<valid-key>"}}` returns full entity profile with general info (including nonprofit fields when applicable), registered agent, governors/officers, and filing history metadata; null fields for unpopulated data

---

## Phase 5: User Story 3 — Washington Filing History with Document Downloads (Priority: P2)

**Goal**: Entity detail response includes filing documents downloaded to Azure Blob Storage with proxy URLs, following the established page-level fetch() pattern for the N most recent filings

**Independent Test**: Request entity details for an active entity with filing history (e.g., Starbucks Corporation) and confirm the response includes filing records with proxy URLs that serve the actual document content

### Tests for User Story 3 (Red — must fail before implementation)

- [x] T033 [P] [US3] Unit test for fetchWaDocumentList JS script: verify page-level fetch() call to GetTransactionDocumentsList API returns parsed document list with documentType, createdDate per item — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaFilingDocumentTests.cs
- [x] T034 [P] [US3] Unit test for downloadWaDocument JS script: verify page-level fetch() to DownloadOnlineFilesByNumber returns base64-encoded content; verify error handling returns error field instead of throwing — in tests/BizScraper.UnitTests/Features/ExecuteScript/WaFilingDocumentTests.cs

### Implementation for User Story 3

- [x] T035 [US3] Implement filing document download loop in us-wa-entity-details.json: action-008 loop over first N filings (N from ${maxFilingDocuments}, default 10), for each filing call fetchWaDocumentList JavaScript via page.EvaluateAsync using page-level fetch() to GetTransactionDocumentsList API (https://ccfs-api.prod.sos.wa.gov/api/Common/GetTransactionDocumentsList) to get document list, then for each document call downloadWaDocument JavaScript via page-level fetch() to DownloadOnlineFilesByNumber endpoint to download as base64 (FR-008); upload to Azure Blob Storage under ${correlationId}/wa/${keyParams.businessId}/ path; store proxy URL in document.storageUrl and generated filename in document.fileName; continue-on-error for individual failures with error recorded in document.error field (FR-013) in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json
- [x] T036 [US3] Add WA filing document post-processing handler in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs: implement ProcessWaFilingDocumentsAsync method following the same pattern as ProcessMoFilingDocumentsAsync — accepts IPage page, iterates over filings[].documents[] from the extracted result, uses page.EvaluateAsync() with JavaScript fetch() + base64 encoding to download each document within the page session context, uploads to blob storage, and replaces storageUrl with proxy URL; wire into post-processing dispatch based on definition slug "us-wa-entity-details"
- [x] T037 [US3] Update the compose output in us-wa-entity-details.json to include documents array within each filing record — each document with documentType, createdDate, storageUrl, fileName, and error fields in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wa-entity-details.json

**Checkpoint**: Entity details for an entity with filing history include filing records with documents; proxy URLs serve actual PDF content; individual download failures produce per-document error without failing the request

---

## Phase 6: User Story 4 — Washington Search Appears in Test UI (Priority: P2)

**Goal**: Test UI users can select Washington from the state dropdown, search, and drill into entity details

**Independent Test**: Open Test UI, select WA from dropdown, search for "Starbucks", verify results table renders; click a result and verify entity detail view loads with general info, governors, filing history, and registered agent

### Implementation for User Story 4

- [x] T038 [P] [US4] Add 'WA' to the StateCode union type in src/test-ui/src/app/features/search/search.component.ts — change `type StateCode = 'CO' | 'WY' | 'IA' | 'DE' | 'MO'` to `type StateCode = 'CO' | 'WY' | 'IA' | 'DE' | 'MO' | 'WA'`
- [x] T039 [P] [US4] Add WA entry to STATE_SLUG_MAP in src/test-ui/src/app/features/search/search.component.ts — add `WA: 'us-wa-business-search'`
- [x] T040 [P] [US4] Add WA entry to STATE_DISPLAY_LABEL in src/test-ui/src/app/features/search/search.component.ts — add `WA: 'Washington (US)'`
- [x] T041 [P] [US4] Add WA entry to STATE_DETAIL_SLUG in src/test-ui/src/app/features/entity-details/entity-details.component.ts — add `WA: 'us-wa-entity-details'`

**Checkpoint**: Test UI state dropdown includes Washington; searching returns results in standard table; clicking a result navigates to entity details view with all sections populated

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation, documentation, and cleanup

- [x] T042 [P] Run full unit test suite to verify no regressions: `dotnet test tests/BizScraper.UnitTests`
- [x] T043 [P] Run full integration test suite: `dotnet test tests/BizScraper.IntegrationTests`
- [x] T044 [P] Build Test UI and verify no Angular compilation errors: `cd src/test-ui && npm run build`
- [x] T045 Update specs/current-specs/spec.md to add Washington Business Entity Search section with us-wa-business-search and us-wa-entity-details definitions, WA state code, and CCFS site reference (Design Standards: Global Snapshot)
- [x] T046 Update specs/current-specs/data-model.md to add WashingtonBusinessSearchResult, WashingtonEntityDetails, WashingtonGeneralInfo, WashingtonRegisteredAgent, WashingtonGovernor, WashingtonFilingRecord, WashingtonFilingDocument models and field mappings
- [ ] T047 Run quickstart.md validation — requires running instance with Azure Playwright Workspaces connectivity (manual validation)
- [ ] T048 E2e test: search "Starbucks" → verify results → drill into Starbucks Corporation → verify general info, governors, filing history with documents — requires running instance with Azure Playwright Workspaces connectivity (manual validation). Also verify FR-009 compliance: all data is obtained by interacting with the SPA UI elements (not by calling the backend API directly), confirming the browser-automation approach
- [ ] T049 Performance smoke test: verify search <15s and entity detail <30s for typical queries — requires running instance with Azure Playwright Workspaces connectivity (manual validation)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion (T001, T002 must exist before T003 can validate)
- **User Story 1 (Phase 3)**: Depends on Phase 2 (definitions must load before actions are filled in); test tasks (T005-T008) run first, then implementation (T009-T018)
- **User Story 2 (Phase 4)**: Depends on Phase 2; independent of US1 (different definition file); test tasks (T019-T023) run first, then implementation (T024-T032)
- **User Story 3 (Phase 5)**: Depends on Phase 4 (US2 entity details must be in place before filing documents can be downloaded); test tasks (T033-T034) run first, then implementation (T035-T037)
- **User Story 4 (Phase 6)**: Depends on Phase 1 (definitions must exist for slug references); independent of US1/US2/US3 code
- **Polish (Phase 7)**: Depends on all prior phases

### User Story Dependencies

- **US1 (WA Business Search)**: Depends on Phase 2 only — can start immediately after foundational
- **US2 (WA Entity Details)**: Depends on Phase 2 only — can run in parallel with US1 (different JSON file)
- **US3 (Filing Documents)**: Depends on US2 (filing extraction must exist before documents can be downloaded)
- **US4 (Test UI)**: Depends on Phase 1 only — can run in parallel with US1, US2, and US3

### Within Each User Story

- Tests MUST be written first and MUST fail before corresponding implementation begins (Constitution Principle I)
- Actions must be implemented in sequential order (navigate → wait → fill → click → extract → paginate)
- Search definition (US1) and detail definition (US2) are separate files — no cross-dependency
- Filing documents (US3) extends the detail definition (US2) with additional actions
- Test UI changes (US4) are all independent [P] tasks across two files

### Parallel Opportunities

- T001 and T002 (Phase 1) can run in parallel — separate files
- T003 and T004 (Phase 2) are sequential — T003 updates test, T004 runs it
- US1 tests (T005-T008) can all run in parallel
- US2 tests (T019-T023) can all run in parallel
- US3 tests (T033-T034) can run in parallel
- US1 (T005-T018) and US2 (T019-T032) can run in parallel — separate definition files
- US4 (T038-T041) can all run in parallel — T038/T039/T040 are in the same file but independent edits; T041 is a different file
- T042, T043, and T044 (Phase 7) can run in parallel

---

## Parallel Example: All User Stories

```
Phase 1 (Setup):  T001 ─┐
                  T002 ─┤
                        ▼
Phase 2 (Found):  T003 → T004
                        │
         ┌──────────────┼──────────┬────────────┐
         ▼              ▼          │            ▼
Phase 3 (US1):   Phase 4 (US2):   │     Phase 6 (US4):
  T005 ─┐         T019 ─┐         │       T038 ─┐
  T006 ─┤ tests   T020 ─┤ tests   │       T039 ─┤
  T007 ─┤         T021 ─┤         │       T040 ─┤
  T008 ─┘         T022 ─┤         │       T041 ─┘
  T009             T023 ─┘         │
  T010             T024            │
  T011             T025            │
  T012             T026            │
  T013             T027            │
  T014             T028            │
  T015             T029            │
  T016             T030            │
  T017             T031            │
  T018             T032            │
         │              │          │            │
         │              ▼          │            │
         │       Phase 5 (US3):    │            │
         │         T033 ─┐ tests   │            │
         │         T034 ─┘         │            │
         │         T035            │            │
         │         T036            │            │
         │         T037            │            │
         │              │          │            │
         └──────────────┼──────────┴────────────┘
                        ▼
Phase 7 (Polish): T042 ─┬─ T043 ─┬─ T044
                                  ▼
                  T045 → T046 → T047 → T048 → T049
```

---

## Implementation Strategy

### MVP Scope (Suggested)

**User Story 1 only** (Phases 1 + 2 + 3): Delivers the core WA business search capability. API consumers can search for Washington entities and receive normalized results. This alone extends BizScraper coverage to Washington.

### Incremental Delivery

1. **Increment 1** (MVP): US1 — WA Business Search (T001-T018)
2. **Increment 2**: US2 — WA Entity Details (T019-T032)
3. **Increment 3**: US3 — Filing Document Downloads (T033-T037)
4. **Increment 4**: US4 — Test UI (T038-T041) + Polish (T042-T049)

### Task Count Summary

| Phase | Tasks | Parallelizable |
|-------|-------|---------------|
| Phase 1: Setup | 2 | 2 |
| Phase 2: Foundational | 2 | 0 |
| Phase 3: US1 — Search | 14 (4 test + 10 impl) | 4 (tests) |
| Phase 4: US2 — Entity Details | 14 (5 test + 9 impl) | 5 (tests) |
| Phase 5: US3 — Filing Documents | 5 (2 test + 3 impl) | 2 (tests) |
| Phase 6: US4 — Test UI | 4 | 4 |
| Phase 7: Polish | 8 | 3 |
| **Total** | **49** | **20** |
