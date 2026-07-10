# Tasks: Multi-State Business Entity Search

**Input**: Design documents from `/specs/005-multi-state-search/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included per Constitution Principle I (Test-First NON-NEGOTIABLE). Unit tests for normalized models, handler validation, CAPTCHA solver. Integration tests for endpoint + audit. E2E tests for Wyoming search.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Single project**: `src/CososWebScraping.Api/` at repository root
- **Test projects**: `tests/CososWebScraping.UnitTests/`, `tests/CososWebScraping.IntegrationTests/`, `tests/CososWebScraping.EndToEndTests/`
- **Liquibase**: `deploy/liquibase/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration classes, database schema, and appsettings changes needed before implementation begins

- [X] T001 [P] Add WyomingSearchUrl property to src/CososWebScraping.Api/Common/Configuration/PlaywrightOptions.cs
- [X] T002 [P] Create FabAgentOptions configuration class with CaptchaSolver nested section (EndpointUrl, AuthToken, TimeoutSeconds) in src/CososWebScraping.Api/Common/Configuration/FabAgentOptions.cs
- [X] T003 [P] Add Playwright:WyomingSearchUrl and FabAgent:CaptchaSolver configuration sections to src/CososWebScraping.Api/appsettings.json
- [X] T004 [P] Create DDL changelog with rollback block in deploy/liquibase/changelog/ddl/2.0.2603.0-add-state-column-to-search-audit.sql
- [X] T005 Add new changelog entry to deploy/liquibase/masterfiles/master-1.0.2603.0.yaml referencing 2.0.2603.0-add-state-column-to-search-audit.sql

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Interfaces, shared models, entity modifications, exception types, DI registrations, and CO scraper refactoring that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 [P] Create IStateSearchScraper interface with StateCode property and SearchAsync(string searchTerm, string correlationId, CancellationToken) returning NormalizedSearchScrapeResult (new wrapper: IReadOnlyList<NormalizedSearchResult>, SiteTotalCount, Truncated, PagesScraped) in src/CososWebScraping.Api/Common/Interfaces/IStateSearchScraper.cs
- [X] T007 [P] Create ICaptchaSolver interface with SolveAsync(byte[] imageBytes, CancellationToken) returning string in src/CososWebScraping.Api/Common/Interfaces/ICaptchaSolver.cs
- [X] T008 [P] Create NormalizedSearchResult record with common fields (name, identifier, status, entityType, formationDate, detailsUrl, state) and nullable state-specific fields (event?, standingTax?, standingRA?), and NormalizedSearchScrapeResult record (IReadOnlyList<NormalizedSearchResult>, SiteTotalCount, Truncated, PagesScraped) in src/CososWebScraping.Api/Features/BusinessSearch/Models/NormalizedSearchResult.cs
- [X] T009 [P] Create NormalizedSearchResponse record with results array, totalCount, siteTotalCount, truncated, pagesScraped, searchTerm, and state in src/CososWebScraping.Api/Features/BusinessSearch/Models/NormalizedSearchResponse.cs
- [X] T010 [P] Create CaptchaResolutionException class in src/CososWebScraping.Api/Features/BusinessSearch/Exceptions/CaptchaResolutionException.cs
- [X] T011 [P] Create FabCaptchaSolverRequest (with nested FabCaptchaSolverInput: format, data), FabCaptchaSolverResponse (with nested FabCaptchaSolverOutput: captchaText, confidence) models in src/CososWebScraping.Api/Infrastructure/CaptchaSolving/FabCaptchaSolverModels.cs
- [X] T012 [P] Add State property (string?) to SearchBusinessEntitiesQuery record in src/CososWebScraping.Api/Features/BusinessSearch/Queries/SearchBusinessEntitiesQuery.cs
- [X] T013 [P] Add State property (string?) to SearchAuditEntry entity in src/CososWebScraping.Api/Domain/Entities/SearchAuditEntry.cs
- [X] T014 Register NormalizedSearchResult, NormalizedSearchScrapeResult, NormalizedSearchResponse, FabCaptchaSolverRequest, and FabCaptchaSolverResponse in AppJsonSerializerContext with [JsonSerializable] attributes in src/CososWebScraping.Api/Common/Serialization/AppJsonSerializerContext.cs
- [X] T015 Add CaptchaResolutionException → 502 Bad Gateway mapping in GlobalExceptionHandler switch expression in src/CososWebScraping.Api/Middleware/GlobalExceptionHandler.cs
- [X] T016 Refactor PlaywrightBusinessEntityScraper to implement IStateSearchScraper (StateCode = "CO") with internal BusinessEntityResult → NormalizedSearchResult mapping (DocumentNumber→identifier, Form→entityType, Event→event, standingTax/RA=null, state="CO"), keeping existing IBusinessEntityScraper temporarily, in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [X] T017 Update AuditTrailRepository to include State column in INSERT SQL and ConfigureSaveCommand parameter binding in src/CososWebScraping.Api/Infrastructure/Persistence/AuditTrailRepository.cs
- [X] T018 Register keyed IStateSearchScraper for CO, keyed SemaphoreSlim(1,1) for CO and WY, bind FabAgentOptions config, and bind PlaywrightOptions WyomingSearchUrl in src/CososWebScraping.Api/Program.cs

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 — Wyoming Business Entity Search (Priority: P1) 🎯 MVP

**Goal**: Enable searching for business entities registered in Wyoming via the FAB AI CAPTCHA-solving flow, Playwright browser automation, and ASP.NET WebForms postback pagination

**Independent Test**: Send a search request with `state=WY` and `name=Wendy` and verify the response contains Wyoming filing data (name, filing number, status, standing tax/RA, filed-on date, details URL)

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T019 [P] [US1] Create FabCaptchaSolverTests unit tests covering successful solve (extract output.captchaText), FAB timeout/retry via Polly, 4xx/5xx error handling, and low-confidence response handling in tests/CososWebScraping.UnitTests/Features/BusinessSearch/FabCaptchaSolverTests.cs
- [X] T020 [P] [US1] Create WyomingSearchScrapingTests unit tests covering CAPTCHA detection heuristic ("What code is in the image?"), result extraction from WY HTML structure, pagination logic (postback click + network-idle), and partial-result truncation in tests/CososWebScraping.UnitTests/Features/BusinessSearch/WyomingSearchScrapingTests.cs
- [X] T021 [P] [US1] Create WyomingSearchTests E2E test verifying known entity "Wendy" returns results with filing number 2018-000797412 and 37 total results across 2 pages in tests/CososWebScraping.EndToEndTests/Scenarios/BusinessSearch/WyomingSearchTests.cs

### Implementation for User Story 1

- [X] T022 [US1] Implement FabCaptchaSolver with Flurl.Http POST using input wrapper format ({ input: { format, data } }), Bearer auth from FabAgentOptions, Polly retry pipeline for transient 5xx/timeouts, and response extraction from output.captchaText in src/CososWebScraping.Api/Infrastructure/CaptchaSolving/FabCaptchaSolver.cs
- [X] T023 [US1] Implement PlaywrightWyomingSearchScraper (IStateSearchScraper, StateCode="WY") with CAPTCHA detection ("What code is in the image?" heuristic), FAB solving via ICaptchaSolver, Filing Name field submission, result extraction mapping directly to NormalizedSearchResult (FilingNumber→identifier, parsed EntityType→entityType, event=null, standingTax/RA populated, state="WY", details URL resolved to absolute), and ASP.NET postback pagination via click + network-idle in src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightWyomingSearchScraper.cs
- [X] T024 [P] [US1] Add Wyoming-specific log messages (WySearchStarted, CaptchaDetected, CaptchaSolved, CaptchaFailed, WyPageScraped, WySearchCompleted) with state dimension to src/CososWebScraping.Api/Features/BusinessSearch/Logging/BusinessSearchLogMessages.cs
- [X] T025 [P] [US1] Add state dimension tag to existing metrics counters and add CAPTCHA-specific counters (captcha_solves_total, captcha_failures_total) to src/CososWebScraping.Api/Features/BusinessSearch/Metrics/BusinessSearchMetrics.cs
- [X] T026 [US1] Register WY keyed IStateSearchScraper (PlaywrightWyomingSearchScraper) and ICaptchaSolver (FabCaptchaSolver) singleton in src/CososWebScraping.Api/Program.cs

**Checkpoint**: Wyoming scraper functional — can be tested independently via unit tests and E2E once endpoint is wired (Phase 4)

---

## Phase 4: User Story 2 — Normalized Multi-State API (Priority: P1)

**Goal**: Accept a mandatory `state` query parameter (CO or WY) on the existing `/api/v1/business-search` endpoint, route to the correct keyed scraper, enforce per-state concurrency, and return the normalized response

**Independent Test**: Send search requests with `state=CO` and `state=WY` using the same search term and verify both return the same normalized response structure with state-appropriate data

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (Constitution Principle I)**

- [X] T027 [P] [US2] Create StateParameterValidationTests unit tests covering missing state → 400, unsupported state (TX) → 400, empty name → 400, whitespace name → 400, valid CO+name → success, valid WY+name → success in tests/CososWebScraping.UnitTests/Features/BusinessSearch/StateParameterValidationTests.cs
- [X] T028 [P] [US2] Create MultiStateEndpointTests integration tests covering CO search returns normalized response, WY search returns normalized response, concurrent same-state requests → 503 with Retry-After, concurrent cross-state requests → both succeed in tests/CososWebScraping.IntegrationTests/Features/BusinessSearch/MultiStateEndpointTests.cs

### Implementation for User Story 2

- [X] T029 [US2] Modify BusinessSearchEndpoints to accept mandatory state query parameter, validate state (CO/WY case-insensitive) and name inputs returning 400 for invalid, pass state to SearchBusinessEntitiesQuery, and return NormalizedSearchResponse in src/CososWebScraping.Api/Features/BusinessSearch/Endpoints/BusinessSearchEndpoints.cs
- [X] T030 [US2] Refactor SearchBusinessEntitiesHandler for state-based scraper routing via IServiceProvider.GetRequiredKeyedService<IStateSearchScraper>(state), per-state semaphore via keyed SemaphoreSlim, and NormalizedSearchResponse envelope assembly from NormalizedSearchScrapeResult (no field mapping needed — scrapers return normalized results) in src/CososWebScraping.Api/Features/BusinessSearch/Handlers/SearchBusinessEntitiesHandler.cs

**Checkpoint**: Full multi-state search functional — both CO and WY searches work through the normalized endpoint

---

## Phase 5: User Story 3 — Normalized Response Schema (Priority: P1)

**Goal**: Ensure the response JSON follows a unified schema with common fields for both states and state-specific nullable fields, so consumers process results without branching logic

**Independent Test**: Compare CO and WY responses and confirm identical top-level structure; CO has `event` populated and `standingTax`/`standingRA` null; WY has `standingTax`/`standingRA` populated and `event` null

> Core schema implementation completed in Phase 2 (T008, T009, T014). Mapping logic implemented in Phase 4 (T030). This phase validates schema correctness and adds OpenAPI documentation.

### Tests for User Story 3

- [X] T031 [P] [US3] Create NormalizedResponseMappingTests verifying CO mapping (DocumentNumber → identifier, Form → entityType, Event → event, standingTax/RA null) and WY mapping (FilingNumber → identifier, parsed entityType, standingTax/RA populated, event null) in tests/CososWebScraping.UnitTests/Features/BusinessSearch/NormalizedResponseMappingTests.cs

### Implementation for User Story 3

- [X] T032 [US3] Update OpenAPI response metadata to .Produces<NormalizedSearchResponse>(200) and .ProducesProblem for 400/422/502/503 on the business search endpoint in src/CososWebScraping.Api/Features/BusinessSearch/Endpoints/BusinessSearchEndpoints.cs

**Checkpoint**: Normalized schema verified — CO and WY responses share identical structure, documented in OpenAPI

---

## Phase 6: User Story 4 — Backward-Compatible Existing Colorado Search (Priority: P2)

**Goal**: Ensure existing Colorado search workflows continue functioning with `state=CO`, audit trail records state, and all previously available CO fields are preserved in the normalized response

**Independent Test**: Run existing CO search test scenarios with `state=CO` parameter and verify all original fields (DocumentNumber → identifier, Form → entityType, Event → event, FormationDate, DetailsUrl) are present in normalized output

### Tests for User Story 4

- [X] T033 [P] [US4] Create AuditTrailStateColumnTests integration tests verifying State column is written as "CO" for CO searches, "WY" for WY searches, and existing NULL rows are handled gracefully in tests/CososWebScraping.IntegrationTests/Features/BusinessSearch/AuditTrailStateColumnTests.cs

### Implementation for User Story 4

- [X] T034 [US4] Update existing Colorado search unit/integration tests to use state=CO parameter and validate normalized response includes all previously available fields (DocumentNumber → identifier, Form → entityType, Event → event, Status, FormationDate, DetailsUrl) in existing test files

**Checkpoint**: Colorado backward compatibility confirmed — all existing CO data preserved in normalized response, audit trail records state

---

## Phase 7: User Story 5 — Semantic Scraping Action Abstraction (Priority: P2)

**Goal**: Confirm that scraping logic is expressed using semantic intent (navigate, submit, extract, paginate) via the IStateSearchScraper interface, with state-specific DOM details encapsulated in each scraper

**Independent Test**: Verify that adding a hypothetical third state requires only implementing IStateSearchScraper — no changes to handler, endpoint, or response models

> Core abstraction implemented in Phase 2 (T006 — IStateSearchScraper interface, T016 — CO implements it) and Phase 3 (T023 — WY implements it). This phase adds semantic documentation.

### Implementation for User Story 5

- [X] T035 [US5] Add XML documentation to IStateSearchScraper interface methods describing semantic intent (SearchAsync = "submit search query to the state's Secretary of State website and extract all results across pages") and verify both CO and WY scrapers fulfill the contract without handler coupling in src/CososWebScraping.Api/Common/Interfaces/IStateSearchScraper.cs

**Checkpoint**: Semantic abstraction verified — CO and WY scrapers implement identical interface, handler has zero DOM knowledge

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, validation, and final verification across all user stories

- [X] T036 Remove IBusinessEntityScraper interface and update all references to use IStateSearchScraper in src/CososWebScraping.Api/Common/Interfaces/IBusinessEntityScraper.cs and src/CososWebScraping.Api/Program.cs
- [X] T037 Run quickstart.md verification scenarios — CO search for "Wendy" (expect identifier 19871296702), WY search for "Wendy" (expect identifier 2018-000797412, 37 total results, 2 pages)
- [X] T038 [P] Code cleanup — resolve analyzer warnings and verify build passes with TreatWarningsAsErrors and EnforceCodeStyleInBuild
- [X] T039 [P] Verify full test suite passes (unit + integration + E2E) with dotnet test across all test projects

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) — needs interfaces, models, config
- **US2 (Phase 4)**: Depends on Foundational (Phase 2) + US1 (Phase 3) — handler needs WY scraper registered
- **US3 (Phase 5)**: Depends on US2 (Phase 4) — validates mapping logic from handler
- **US4 (Phase 6)**: Depends on US2 (Phase 4) — needs normalized endpoint to verify CO compatibility
- **US5 (Phase 7)**: Depends on US1 (Phase 3) — needs both CO and WY scrapers implemented
- **Polish (Phase 8)**: Depends on all user stories being complete

### Within Phase Task Dependencies

**Phase 1**:
- T001–T004: All parallel (independent files)
- T005: Depends on T004 (changelog must exist before master YAML references it)

**Phase 2**:
- T006–T013: All parallel (independent new/modified files)
- T014: Depends on T008, T009, T011 (needs model types to register in serializer context)
- T015: Depends on T010 (needs exception type for mapping)
- T016: Depends on T006 (needs IStateSearchScraper interface)
- T017: Depends on T013 (needs State property on entity)
- T018: Depends on T006, T007, T016 (needs interfaces + CO scraper refactored for keyed registration)

**Phase 3 (US1)**:
- T019, T020, T021: All parallel (test files, write first — Red phase)
- T022: Depends on T007, T011 (ICaptchaSolver interface + FAB models)
- T023: Depends on T006, T022 (IStateSearchScraper interface + CAPTCHA solver)
- T024, T025: Parallel (independent log/metrics files)
- T026: Depends on T022, T023 (needs concrete types for DI registration)

**Phase 4 (US2)**:
- T027, T028: Parallel (test files, write first — Red phase)
- T029: Depends on T008, T009, T012 (normalized models + query with State)
- T030: Depends on T016, T023, T029 (both scrapers + endpoint wired)

### Parallel Opportunities

- **Phase 1**: T001 ‖ T002 ‖ T003 ‖ T004, then T005
- **Phase 2**: T006–T013 (8 tasks in parallel), then T014 ‖ T015 ‖ T016 ‖ T017, then T018
- **Phase 3**: T019 ‖ T020 ‖ T021 (tests first), then T022 → T023 (sequential — solver before scraper), T024 ‖ T025 (parallel), then T026
- **Phase 4**: T027 ‖ T028 (tests first), then T029 → T030 (sequential — endpoint before handler)
- **Phase 5**: T031 (test) → T032 (OpenAPI metadata)
- **Phase 6**: T033 (test) → T034 (existing test updates)
- **Phase 8**: T036 → T037, T038 ‖ T039

---

## Parallel Example: User Story 1

```text
# Launch all tests for US1 together (write first, ensure they FAIL):
Task T019: "FabCaptchaSolverTests in tests/.../FabCaptchaSolverTests.cs"
Task T020: "WyomingSearchScrapingTests in tests/.../WyomingSearchScrapingTests.cs"
Task T021: "WyomingSearchTests E2E in tests/.../WyomingSearchTests.cs"

# Then implement sequentially (solver before scraper):
Task T022: "FabCaptchaSolver in src/.../FabCaptchaSolver.cs"
Task T023: "PlaywrightWyomingSearchScraper in src/.../PlaywrightWyomingSearchScraper.cs"

# In parallel with T022/T023 (independent files):
Task T024: "WY log messages in BusinessSearchLogMessages.cs"
Task T025: "WY metrics in BusinessSearchMetrics.cs"

# Finally register DI:
Task T026: "Register WY keyed services in Program.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 + 2)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (Wyoming scraper + CAPTCHA solver)
4. Complete Phase 4: User Story 2 (Wire normalized endpoint)
5. **STOP and VALIDATE**: Both CO and WY searches return normalized responses → **Deploy/Demo (MVP!)**

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → WY scraper tested independently
3. Add User Story 2 → Full multi-state endpoint functional → **Deploy/Demo (MVP!)**
4. Add User Story 3 → Schema validated, OpenAPI documented
5. Add User Story 4 → CO backward compatibility confirmed
6. Add User Story 5 → Semantic abstraction verified
7. Polish → cleanup, quickstart validation, full test suite green

### Key Technical Decisions (from research.md)

- **R1**: Playwright click + network-idle for WY ASP.NET postback pagination
- **R2**: Flurl.Http + Polly retry for FAB CAPTCHA solver HTTP POST (input/output wrapper format)
- **R3**: IStateSearchScraper with keyed DI (state code as key) — no factory class
- **R4**: `string?` nullable fields for state-specific data in NormalizedSearchResult
- **R5**: Per-state keyed `SemaphoreSlim(1,1)` — CO and WY can run simultaneously
- **R6**: CAPTCHA detection by "What code is in the image?" text → screenshot → FAB POST (input wrapper) → extract output.captchaText → submit
- **R7**: Nullable `State NVARCHAR(10) NULL` column — no backfill of existing rows
- **R8**: WY scraper injects PlaywrightBusinessEntityScraper for shared browser pool (same pattern as EntityDetail scraper)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Write tests FIRST, verify they fail, then implement (Constitution Principle I)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Version: `2.0.2603.0` (MAJOR bump — breaking change: required state param, normalized response schema)
- FAB API uses input/output wrapper format: `{ "input": { "format": "base64", "data": "..." } }` → `{ "output": { "captchaText": "...", "confidence": "..." } }`
