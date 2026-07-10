# Tasks: Wyoming Entity Details Support

**Input**: Design documents from `/specs/007-wy-entity-details/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Required per Constitution Principle I (Test-First). Unit, integration, and E2E test tasks are included before their corresponding implementation tasks.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Database migration and foundational model additions required by all user stories

- [X] T001 Create Liquibase migration adding StateCode column in deploy/liquibase/changelog/ddl/2.0.2603.3-add-state-column-to-entity-detail-audit.sql
- [X] T002 Register new changelog in deploy/liquibase/masterfiles/master-2.0.2603.0.yaml
- [X] T003 [P] Add StateCode property to EntityDetailAuditEntry in src/BizScraper.Api/Domain/Entities/EntityDetailAuditEntry.cs
- [X] T004 [P] Create PartyResult record in src/BizScraper.Api/Features/EntityDetails/Models/PartyResult.cs
- [X] T005 [P] Create HistoryDocumentResult record in src/BizScraper.Api/Features/EntityDetails/Models/HistoryDocumentResult.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Endpoint signature change, query model change, and model extensions that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 Add State property to GetEntityDetailsQuery in src/BizScraper.Api/Features/EntityDetails/Queries/GetEntityDetailsQuery.cs
- [X] T007 Add required state query parameter to endpoint in src/BizScraper.Api/Features/EntityDetails/Endpoints/EntityDetailsEndpoints.cs
- [X] T008 Extend EntityDetailResult with nullable WY-specific fields (SubStatus, StandingTax, StandingRA, StandingOther, InactiveDate, TermOfDuration, FormedIn, LatestAnnualReportYear, AnnualReportExempt, LicenseTaxPaid) in src/BizScraper.Api/Features/EntityDetails/Models/EntityDetailResult.cs
- [X] T009 Extend EntityDetailResponse with Parties and HistoryDocuments collections in src/BizScraper.Api/Features/EntityDetails/Models/EntityDetailResponse.cs
- [X] T010 Extend EntityDetailScrapeResult with Parties and HistoryDocuments in src/BizScraper.Api/Features/EntityDetails/Models/EntityDetailScrapeResult.cs
- [X] T011 Register PartyResult and HistoryDocumentResult in the JsonSerializerContext source generator in src/BizScraper.Api/Infrastructure/Serialization/ (or wherever BizScraperJsonContext is defined)
- [X] T012 Build and verify zero warnings zero errors

**Checkpoint**: Foundation ready — endpoint accepts state, models extended, DB migration ready. User story implementation can now begin.

---

## Phase 3: User Story 1 — Retrieve Wyoming Entity Details (Priority: P1) 🎯 MVP

**Goal**: Enable `GET /api/v1/entity-details?state=WY&detailsUrl={url}` to return WY entity details including CAPTCHA resolution

**Independent Test**: Call endpoint with a valid WY URL and `state=WY`, verify response contains entity name, filing ID, type, status, sub-status, standings, addresses, registered agent, parties, and up to 3 history documents.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T013 [P] [US1] Unit test: EntityDetailResult deserialization with WY nullable fields in tests/BizScraper.UnitTests/Features/EntityDetails/EntityDetailResultTests.cs
- [X] T014 [P] [US1] Unit test: GetEntityDetailsHandler routes to correct state-keyed IEntityDetailScraper in tests/BizScraper.UnitTests/Features/EntityDetails/GetEntityDetailsHandlerTests.cs
- [X] T015 [P] [US1] Integration test: audit trail persists StateCode = "WY" for WY requests in tests/BizScraper.IntegrationTests/Features/EntityDetails/EntityDetailAuditTests.cs

### Implementation for User Story 1

- [X] T016 [US1] Create wy-entity-details.json flow definition with navigate action to the details URL in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json
- [X] T017 [US1] Add CAPTCHA loop to wy-entity-details.json — wait-for-condition on CAPTCHA form, screenshot, extract CAPTCHA image, call-service FAB solver, fill answer, click submit, repeat up to 3 attempts
- [X] T018 [US1] Add detail extraction action to wy-entity-details.json — JavaScript extracting Name, FilingId, Type, Status, SubStatus, InitialFilingDate, StandingTax, StandingRA, StandingOther, InactiveDate, TermOfDuration, FormedIn from detail block
- [X] T019 [US1] Add address extraction action to wy-entity-details.json — JavaScript extracting PrincipalOfficeStreetAddress, PrincipalOfficeMailingAddress from address section
- [X] T020 [US1] Add registered agent extraction action to wy-entity-details.json — JavaScript extracting RA name, street address, mailing address from Additional Details section
- [X] T021 [US1] Add additional details extraction action to wy-entity-details.json — JavaScript extracting LatestAnnualReportYear, AnnualReportExempt, LicenseTaxPaid from Additional Details section
- [X] T022 [US1] Add party extraction action to wy-entity-details.json — JavaScript extracting all parties (name, role, organization, address) from Parties section
- [X] T023 [US1] Add history document download loop to wy-entity-details.json — extract up to 3 most recent history entries with document links, click to expand, download filing images, upload to blob storage; filenames follow format {filingId}-{slugified-event-title}.{ext}
- [X] T024 [US1] Add screenshot actions at key milestones in wy-entity-details.json (page load, after CAPTCHA, after extraction)
- [X] T025 [US1] Register wy-entity-details.json as an embedded/content resource in the project file or flow loader (ensure FlowLoader can resolve "WY" + "entity-details")
- [X] T026 [US1] Parameterize JsonDrivenEntityDetailScraper for state — accept state code, load correct flow definition via flowLoader.GetDefinition(stateCode, "entity-details") in src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs
- [X] T027 [US1] Register JsonDrivenEntityDetailScraper as keyed services ("CO" and "WY") for IEntityDetailScraper in src/BizScraper.Api/Program.cs (DI registration)
- [X] T028 [US1] Update GetEntityDetailsHandler to resolve IEntityDetailScraper by state key, pass state to scraper, and map Parties + HistoryDocuments to response in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T029 [US1] Update audit trail persistence to include StateCode from the query in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T030 [US1] Build and verify zero warnings zero errors

**Checkpoint**: WY entity details retrievable end-to-end — CAPTCHA solved, details/RA/parties/history extracted, audit recorded. This is the MVP.

---

## Phase 4: User Story 2 — Normalize Response Across States (Priority: P1)

**Goal**: Ensure CO and WY responses share the same JSON shape — CO returns null for WY-specific fields; WY returns null for CO-specific fields (e.g., PeriodicReportMonth, Certificate)

**Independent Test**: Call endpoint for both a CO and a WY entity. Verify both responses have identical top-level keys (details, registeredAgent, certificate, parties, historyDocuments); WY-specific fields are null for CO; CO-specific fields are null for WY.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T031 [P] [US2] Unit test: CO response returns null for WY-specific fields and null for Parties/HistoryDocuments in tests/BizScraper.UnitTests/Features/EntityDetails/ResponseNormalizationTests.cs
- [X] T032 [P] [US2] Unit test: WY response returns Certificate as null in tests/BizScraper.UnitTests/Features/EntityDetails/ResponseNormalizationTests.cs

### Implementation for User Story 2

- [X] T033 [US2] Update CO scraper/handler path to populate null for WY-specific fields on EntityDetailResult (SubStatus, StandingTax, etc.) and null for Parties/HistoryDocuments on EntityDetailResponse in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T034 [US2] Ensure WY response returns Certificate as null in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T035 [US2] Verify JSON serialization includes all nullable fields (not omitted) — confirm JsonIgnoreCondition policy does not skip nulls for entity detail fields in the serializer context
- [X] T036 [US2] Build and verify zero warnings zero errors

**Checkpoint**: CO and WY responses share identical JSON shape. No client-side branching needed.

---

## Phase 5: User Story 3 — URL Validation for Wyoming (Priority: P2)

**Goal**: Validate WY details URLs against an allowlist (HTTPS, host wyobiz.wyo.gov, path /Business/FilingDetails.aspx) and reject state/URL mismatches

**Independent Test**: Send invalid/malicious URLs with `state=WY` and verify 400 responses. Send CO URLs with `state=WY` and verify rejection. Send WY URLs with `state=CO` and verify rejection.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T037 [P] [US3] Unit test: WY URL validation accepts valid wyobiz.wyo.gov URL, rejects invalid host, rejects non-HTTPS in tests/BizScraper.UnitTests/Features/EntityDetails/UrlValidationTests.cs
- [X] T038 [P] [US3] Unit test: state-URL mismatch returns 400 (CO URL with state=WY, WY URL with state=CO) in tests/BizScraper.UnitTests/Features/EntityDetails/UrlValidationTests.cs
- [X] T039 [P] [US3] Unit test: unsupported state code (e.g., TX) returns 400 with supported states list in tests/BizScraper.UnitTests/Features/EntityDetails/UrlValidationTests.cs

### Implementation for User Story 3

- [X] T040 [US3] Add WY URL validation rules to the handler (or validator) — scheme=HTTPS, host=wyobiz.wyo.gov, path starts with /Business/FilingDetails.aspx, no fragment, no userinfo in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T041 [US3] Add state-URL mismatch validation — reject CO URLs when state=WY and WY URLs when state=CO in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T042 [US3] Add unsupported state code validation — return 400 with supported states list when state is not CO or WY in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs
- [X] T043 [US3] Build and verify zero warnings zero errors

**Checkpoint**: All invalid URLs and state mismatches rejected with clear 400 error messages.

---

## Phase 6: User Story 4 — Diagnostics and Audit for WY (Priority: P2)

**Goal**: WY entity detail scraping produces screenshots, traces, and audit records consistent with CO diagnostics

**Independent Test**: Perform a WY entity details scrape, then verify screenshot files exist in blob storage, trace files are stored with the correlation ID, and the audit DB record contains the state code.

### Implementation for User Story 4

- [X] T044 [US4] Verify wy-entity-details.json includes screenshot actions at page load, after CAPTCHA, and after extraction milestones (review T024 output)
- [X] T045 [US4] Verify trace capture is enabled for WY scraping flows — confirm Playwright tracing start/stop actions are present or inherited from engine infrastructure
- [X] T046 [US4] Verify audit trail record persists StateCode = "WY" for WY requests (review T029 output), confirm diagnostics URL (blob storage) is recorded
- [X] T047 [US4] Build and verify zero warnings zero errors

**Checkpoint**: WY diagnostics parity with CO — screenshots, traces, and audit records all present.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, cleanup, and quickstart verification

- [X] T048 [P] Verify existing CO entity details E2E test still passes (regression)
- [X] T049 [P] Run full test suite — confirm all existing tests pass with zero failures
- [X] T050 Run quickstart.md validation — build, run, and exercise both sample curl commands from quickstart.md
- [X] T051 Final build verification — zero warnings, zero errors, all tests green

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — core WY scraping functionality
- **US2 (Phase 4)**: Depends on Phase 2 and Phase 3 — normalization requires WY path to exist
- **US3 (Phase 5)**: Depends on Phase 2 and Phase 3 — shares `GetEntityDetailsHandler.cs` with US1 (T028–T029), must run after US1 completes
- **US4 (Phase 6)**: Depends on Phase 3 — verifies diagnostics produced by WY flow
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Depends only on Foundational (Phase 2). No dependency on other stories.
- **User Story 2 (P1)**: Depends on US1 being complete (needs WY code path to exist for normalization verification).
- **User Story 3 (P2)**: Depends on US1 being complete. Shares `GetEntityDetailsHandler.cs` with US1 tasks — must run sequentially after US1.
- **User Story 4 (P2)**: Depends on US1 being complete (needs WY flow to produce diagnostics).

### Within Each User Story

- Tests MUST be written and FAIL before implementation begins (Constitution Principle I)
- Flow definition actions build sequentially (navigate → CAPTCHA → extract → download)
- Models before services
- Scraper before handler
- DI registration before handler routing
- Build verification at the end of each phase

### Parallel Opportunities

- T003, T004, T005 can run in parallel (different files)
- T008, T009, T010 can run in parallel after T006/T007 (different model files)
- T013, T014, T015 can run in parallel (different test files)
- T016–T024 are sequential within the flow definition (single file)
- T031, T032 can run in parallel (same test file but independent test cases)
- T037, T038, T039 can run in parallel (same test file but independent test cases)
- T048, T049 can run in parallel (independent test runs)

---

## Parallel Example: Phase 1 Setup

```bash
# Launch in parallel (different files):
Task T003: "Add StateCode to EntityDetailAuditEntry"
Task T004: "Create PartyResult record"
Task T005: "Create HistoryDocumentResult record"
```

## Parallel Example: Phase 2 Foundational Models

```bash
# Launch in parallel (different model files):
Task T008: "Extend EntityDetailResult with WY fields"
Task T009: "Extend EntityDetailResponse with Parties/HistoryDocuments"
Task T010: "Extend EntityDetailScrapeResult"
```

## Parallel Example: US1 Tests

```bash
# Launch in parallel (different test files):
Task T013: "Unit test: EntityDetailResult deserialization"
Task T014: "Unit test: GetEntityDetailsHandler state routing"
Task T015: "Integration test: audit StateCode persistence"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (migration, new models)
2. Complete Phase 2: Foundational (endpoint change, model extensions)
3. Complete Phase 3: User Story 1 (WY flow definition, scraper, handler)
4. **STOP and VALIDATE**: Test WY entity details end-to-end
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → WY entity details work → MVP!
3. Add User Story 2 → CO/WY response normalization verified
4. Add User Story 3 → URL validation hardened
5. Add User Story 4 → Diagnostics parity confirmed
6. Polish → Regression tests, quickstart validation

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - All: User Story 1 (WY flow + scraper) — blocks US2, US3, US4
3. After US1 completes:
   - Developer A: User Story 2 (normalization)
   - Developer B: User Story 3 (URL validation), then User Story 4 (diagnostics)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Tests MUST be written and fail before implementation (Constitution Principle I)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- WY flow definition (T016–T024) is a single file built incrementally — tasks are sequential
- History document download (T023) is the most complex single task — involves loop, click-expand, download, blob upload
