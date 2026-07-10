# Tasks: Germany Business Entity Search (Handelsregister)

**Input**: Design documents from `/specs/013-germany-business-search/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/germany-search-api.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4, US5)
- Include exact file paths in descriptions

**⚠️ TDD Mandate (Constitution Principle I)**: Every implementation task follows the Red-Green-Refactor cycle. Write a failing test first, then implement the minimum code to pass, then refactor. This applies to all tasks below — test authoring is implicit in each implementation task, not listed separately.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Database migration, configuration, and feature directory scaffolding

- [X] T001 Create feature directory structure: src/BizScraper.Api/Features/GermanySearch/Endpoints/, Handlers/, Logging/, Metrics/, Models/, Queries/
- [X] T002 [P] Create Liquibase changelog adding WarningMessage NVARCHAR(500) NULL column to SearchAuditEntries table in deploy/liquibase/changelog/ddl/2.0.2603.4-add-warning-column-to-search-audit.sql
- [X] T003 [P] Add changelog include for 2.0.2603.5-add-warning-column-to-search-audit.sql to deploy/liquibase/masterfiles/master-1.0.2603.0.yaml
- [X] T004 [P] Add GermanySearchUrl configuration to Playwright section in src/BizScraper.Api/appsettings.json and src/BizScraper.Api/appsettings.Development.json with value "https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml"

**Checkpoint**: Database schema ready, config values present, feature directories exist.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, serialization, logging, metrics, and audit infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T005 [P] Create GermanyBusinessResult record (CompanyName, RegisteredOffice, Status) in src/BizScraper.Api/Features/GermanySearch/Models/GermanyBusinessResult.cs
- [X] T006 [P] Create GermanySearchResponse record (Results[], TotalCount, PagesScraped, SearchTerm, Warning) in src/BizScraper.Api/Features/GermanySearch/Models/GermanySearchResponse.cs
- [X] T007 [P] Create SearchGermanyBusinessesQuery LiteBus query record (Name string) in src/BizScraper.Api/Features/GermanySearch/Queries/SearchGermanyBusinessesQuery.cs
- [X] T008 [P] Create GermanySearchLog with [LoggerMessage] source-generated methods (search started, search completed, search failed, audit saved, warning detected, page scraped) in src/BizScraper.Api/Features/GermanySearch/Logging/GermanySearchLog.cs
- [X] T009 [P] Create GermanySearchMetrics with OpenTelemetry counters and histograms (search_total, search_duration_ms, search_results_count, search_pages_scraped, search_warnings_total) in src/BizScraper.Api/Features/GermanySearch/Metrics/GermanySearchMetrics.cs
- [X] T010 Register GermanySearchResponse and GermanyBusinessResult types in source-generated AppJsonSerializerContext in src/BizScraper.Api/Common/Serialization/AppJsonSerializerContext.cs
- [X] T011 Extend AuditTrailRepository to accept and persist WarningMessage (nullable) in the SearchAuditEntries INSERT statement in src/BizScraper.Api/Infrastructure/Persistence/AuditTrailRepository.cs
- [X] T011a [P] Add input validation for empty/whitespace search term returning 400 Bad Request with RFC 9457 Problem Details before contacting the Handelsregister in src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs (FR-008 MUST — required for MVP)

**Checkpoint**: Foundation ready — all models, serialization, logging, metrics, and audit support in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Single-Page Germany Business Name Search (Priority: P1) 🎯 MVP

**Goal**: A consumer sends a company name to `GET /api/v1/germany-search` and receives a JSON array of matching business entity records scraped from the first page of Handelsregister results.

**Independent Test**: Search for "Rohde & Schwarz Group Services GmbH" → exactly 1 result with CompanyName, RegisteredOffice "Leipzig", Status "currently registered".

### Implementation for User Story 1

- [X] T012 [P] [US1] Create de-business-search.json flow definition for single-page search in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json — actions: navigate to GermanySearchUrl, dismiss cookie consent if present, fill form:schlagwoerter with ${searchTerm}, click form:btnSuche, wait for results page load, extract results from table.ui-panelgrid.ui-widget elements (CompanyName from .paddingBottom20Px, RegisteredOffice from .sitzSuchErgebnisse, Status from adjacent cell), detect no-results via "0-0 of 0 records" paginator text, screenshot milestones per step
- [X] T013 [P] [US1] Configure de-business-search.json (covered by existing *.json wildcard in csproj) as Content/CopyIfNewer in src/BizScraper.Api/BizScraper.Api.csproj
- [X] T014 [US1] Create SearchGermanyBusinessesHandler implementing IQueryHandler in src/BizScraper.Api/Features/GermanySearch/Handlers/SearchGermanyBusinessesHandler.cs — acquire per-state semaphore (return 503 if busy), dispatch to IStateSearchScraper keyed "DE", map scrape results to GermanySearchResponse, persist audit entry via AuditTrailRepository with State="DE", record metrics and structured logging
- [X] T015 [US1] Create GermanySearchEndpoints.MapGermanySearchEndpoints() extension method in src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs — MapGet /api/v1/germany-search with required name query parameter, dispatch SearchGermanyBusinessesQuery via LiteBus, return TypedResults with X-Correlation-Id header
- [X] T016 [US1] Register GermanySearch services in DI in src/BizScraper.Api/Program.cs — register JsonDrivenSearchScraper as keyed IStateSearchScraper for "DE", register LiteBus handler, call app.MapGermanySearchEndpoints()
- [ ] T017 [US1] Validate single-page search (manual E2E validation) end-to-end: curl "Rohde & Schwarz Group Services GmbH" → 1 result with expected fields; curl nonsense term → empty results array with totalCount 0; curl empty name → 400 Bad Request; verify screenshot files exist in blob storage container under the correlation ID folder (FR-017)

**Checkpoint**: Single-page Germany search works end-to-end. MVP complete — API accepts a company name and returns scraped results from the first page.

---

## Phase 4: User Story 2 — Multi-Page Result Pagination (Priority: P2)

**Goal**: When results span multiple pages (up to 10 pages, 100 results), the API automatically navigates all AJAX-paginated pages and aggregates every record into a single response.

**Independent Test**: Search for "Rohde" → up to 100 results across 10 pages, pagesScraped=10.

### Implementation for User Story 2

- [X] T018 [US2] Extend de-business-search.json with AJAX pagination loop in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json — add loop action with loopType: "pagination": save current paginator text (span.ui-paginator-current), click next a.ui-paginator-page link, wait-for-condition polling until paginator text changes (AJAX refresh), extract results from refreshed page, aggregate into results array, terminate when a.ui-paginator-next has ui-state-disabled class, maxIterations: 10, screenshot milestone per page
- [ ] T019 [US2] Validate multi-page search (manual E2E validation): curl "Rohde" → response contains up to 100 results, pagesScraped equals number of paginator pages visited, totalCount matches results array length

**Checkpoint**: Multi-page pagination works. Broad searches return complete aggregated result sets across all AJAX pages.

---

## Phase 5: User Story 3 — Exceeded-Hits Warning Passthrough (Priority: P2)

**Goal**: When the Handelsregister displays the "maximum number of 100 hits exceeded" warning, the API includes the warning message in the JSON response alongside results.

**Independent Test**: Search for "Rohde" → response includes both results array (100 records) and warning field with exceeded-hits message.

### Implementation for User Story 3

- [X] T020 [US3] Add check-text action for exceeded-hits warning detection to de-business-search.json in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json — check div.warning.displayBlock for "The maximum number of 100 hits has been exceeded" text, onMatch: "store" to save warning into ${warningMessage} variable (do NOT throw — warning is data, not error per R4 decision)
- [X] T021 [US3] Update SearchGermanyBusinessesHandler to map ${warningMessage} variable from scrape output to GermanySearchResponse.Warning field in src/BizScraper.Api/Features/GermanySearch/Handlers/SearchGermanyBusinessesHandler.cs
- [X] T022 [US3] Pass WarningMessage from scrape results to AuditTrailRepository when persisting Germany search audit entries in src/BizScraper.Api/Features/GermanySearch/Handlers/SearchGermanyBusinessesHandler.cs
- [ ] T023 [US3] Validate warning passthrough (manual E2E validation): curl "Rohde" → response warning field contains exceeded-hits message, results array has 100 records, audit entry has WarningMessage populated

**Checkpoint**: Warning detection works. Exceeded-hits message passes through to API response and audit trail without discarding results.

---

## Phase 6: User Story 4 — Test UI Germany Search Tab (Priority: P3)

**Goal**: The test-ui Angular app gains a Germany search tab where users can search, view a results table (CompanyName, RegisteredOffice, Status), and see exceeded-hits warning banners.

**Independent Test**: Open test-ui → navigate to Germany search → enter "Rohde" → verify results table and warning banner appear.

### Implementation for User Story 4

- [X] T024 [P] [US4] Create Germany search Angular component with search input field, search button, results table (CompanyName, RegisteredOffice, Status columns), exceeded-hits warning banner, no-results empty state, and loading/error indicators in src/test-ui/
- [X] T025 [US4] Add Germany search route or tab to test-ui navigation and wire HTTP service call to GET /api/v1/germany-search?name={term} following existing CO/WY search patterns in src/test-ui/
- [ ] T026 [US4] Validate test-ui Germany search (manual UI validation): open browser, navigate to Germany tab, search "Rohde", confirm results table populates with 100 rows and warning banner displays

**Checkpoint**: Test-UI provides visual Germany search capability. All API features are exercisable through the browser.

---

## Phase 7: User Story 5 — Error and Edge Case Handling (Priority: P3)

**Goal**: The API communicates clearly when input is invalid, a concurrent request is rejected, or the Handelsregister site is unreachable.

**Independent Test**: Submit empty name → 400; submit while another request runs → 503; simulate timeout → 500.

### Implementation for User Story 5

- [ ] T027 ~~Moved to T011a in Phase 2~~ (FR-008 is a MUST requirement — input validation must be in place before MVP)
- [X] T028 [P] [US5] Add pagination retry logic (handled by existing Polly retry pipeline in JsonDrivenSearchScraper) to de-business-search.json pagination loop: retry failed page up to 2 times, discard all partial results and propagate error on final failure per FR-013 in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json
- [ ] T029 [US5] Validate error responses (manual E2E validation): empty name → 400 with Problem Details, concurrent request → 503 Service Busy, site unreachable → 500 with structured error body

**Checkpoint**: All error paths return structured, actionable responses. API is production-ready for edge cases.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: E2E test scenarios and final validation across all stories

- [X] T030 [P] Create k6 test scenario covering single-result, multi-page, no-result, warning, and validation-error flows in tests/k6/germany-search.js
- [ ] T031 Run quickstart.md validation (manual E2E validation): execute all curl commands from specs/013-germany-business-search/quickstart.md and confirm expected responses match contract

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (T001 for directories) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational completion — delivers MVP
- **US2 (Phase 4)**: Depends on US1 (extends same JSON flow definition and validates multi-page)
- **US3 (Phase 5)**: Depends on US1; can partially overlap with US2 (warning detection is an independent JSON action but validation requires pagination for full 100-hit scenario)
- **US4 (Phase 6)**: Depends on US1 (API endpoint must exist for UI to call)
- **US5 (Phase 7)**: Depends on US1 (validation and error handling target the existing endpoint); US5 T028 depends on US2 (pagination retry logic requires pagination loop)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — No dependencies on other stories
- **US2 (P2)**: Depends on US1 — extends de-business-search.json with pagination loop
- **US3 (P2)**: Depends on US1 — extends de-business-search.json with warning detection; best done after US2 for full validation
- **US4 (P3)**: Depends on US1 only — Angular component calls the API endpoint
- **US5 (P3)**: Depends on US1 for validation; T028 depends on US2 for pagination retry

### Within Each User Story

- JSON flow definition changes before handler/endpoint changes
- Handler before endpoints (endpoints depend on handler types)
- DI registration after handler and endpoints are created
- Validation as final step in each story

### Parallel Opportunities

- Phase 1: T002, T003, T004 can run in parallel (different files)
- Phase 2: T005, T006, T007, T008, T009 can run in parallel (different files)
- Phase 3: T012, T013 can run in parallel (JSON definition and csproj are independent)
- Phase 4–5: US3 warning detection (T020) can start after US1; US2 pagination (T018) can proceed independently
- Phase 6–7: US4 (test-ui) and US5 (error handling) can run in parallel after US1 (different codebases)
- Phase 8: T030 (k6) is independent of T031 (quickstart validation)

---

## Parallel Example: User Story 1

```
# Launch foundation models together (Phase 2):
T005: GermanyBusinessResult model
T006: GermanySearchResponse model
T007: SearchGermanyBusinessesQuery
T008: GermanySearchLog
T009: GermanySearchMetrics

# Launch US1 JSON definition and csproj together:
T012: de-business-search.json flow definition
T013: Content/CopyIfNewer in csproj

# Sequential within US1 (handler → endpoint → DI → validate):
T014: SearchGermanyBusinessesHandler
T015: GermanySearchEndpoints
T016: DI registration in Program.cs
T017: End-to-end validation
```

---

## Parallel Example: After MVP (US1 complete)

```
# These can proceed in parallel after US1:
Developer A: US2 (T018-T019) — pagination loop
Developer B: US4 (T024-T026) — test-ui component

# After US2 completes:
Developer A: US3 (T020-T023) — warning passthrough
Developer B: US5 (T027-T029) — error handling (T028 requires US2)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (Liquibase, config, directories)
2. Complete Phase 2: Foundational (models, serialization, logging, metrics, audit)
3. Complete Phase 3: User Story 1 (JSON flow, handler, endpoint, DI)
4. **STOP and VALIDATE**: curl "Rohde & Schwarz Group Services GmbH" → 1 result
5. Deploy/demo if ready — single-page Germany search is usable

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → Test independently → Deploy/Demo (**MVP!**)
3. US2 → Multi-page pagination → Test with "Rohde" → Deploy/Demo
4. US3 → Warning passthrough → Verify warning field → Deploy/Demo
5. US4 → Test-UI tab → Visual demo ready → Deploy/Demo
6. US5 → Error hardening → Production-ready → Deploy/Demo
7. Each story adds value without breaking previous stories

### Modified Files Summary

| File | Phase | Change |
|------|-------|--------|
| deploy/liquibase/changelog/ddl/2.0.2603.4-*.sql | Setup | NEW — WarningMessage column |
| deploy/liquibase/masterfiles/master-1.0.2603.0.yaml | Setup | ADD include |
| src/BizScraper.Api/appsettings.json | Setup | ADD GermanySearchUrl |
| src/BizScraper.Api/appsettings.Development.json | Setup | ADD GermanySearchUrl |
| src/BizScraper.Api/Common/Serialization/AppJsonSerializerContext.cs | Foundation | ADD 2 types |
| src/BizScraper.Api/Infrastructure/Persistence/AuditTrailRepository.cs | Foundation | ADD WarningMessage param |
| src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json | US1/US2/US3/US5 | NEW — iteratively extended |
| src/BizScraper.Api/BizScraper.Api.csproj | US1 | ADD Content item |
| src/BizScraper.Api/Program.cs | US1 | ADD DI + endpoint mapping |
| src/test-ui/ | US4 | ADD Germany component/route |

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable after its phase
- TDD is implicit: write failing test → implement → refactor per Constitution Principle I
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- de-business-search.json is iteratively extended across US1→US2→US3→US5 (single file, layered features)
