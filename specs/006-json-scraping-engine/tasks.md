# Tasks: JSON-Driven Scraping Engine

**Input**: Design documents from `/specs/006-json-scraping-engine/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

**⚠️ TDD Mandate (Constitution Principle I)**: Every implementation task follows the Red-Green-Refactor cycle. Write a failing test first, then implement the minimum code to pass, then refactor. This applies to all tasks below — test authoring is implicit in each implementation task, not listed separately.

---

## Phase 1: Setup (Project Rename & Structure)

**Purpose**: Rename CososWebScraping → BizScraper across all artifacts, create new directory structure for the engine

- [X] T001 Rename solution file from CososWebScraping.slnx to BizScraper.slnx and update all project path references within it
- [X] T002 Rename API project directory from src/CososWebScraping.Api/ to src/BizScraper.Api/ and update BizScraper.Api.csproj (AssemblyName, RootNamespace, InternalsVisibleTo)
- [X] T003 Rename unit test project directory from tests/CososWebScraping.UnitTests/ to tests/BizScraper.UnitTests/ and update BizScraper.UnitTests.csproj (ProjectReference path)
- [X] T004 Rename integration test project directory from tests/CososWebScraping.IntegrationTests/ to tests/BizScraper.IntegrationTests/ and update BizScraper.IntegrationTests.csproj (ProjectReference path)
- [X] T005 Rename E2E test project directory from tests/CososWebScraping.EndToEndTests/ to tests/BizScraper.EndToEndTests/ and update BizScraper.EndToEndTests.csproj (ProjectReference path)
- [X] T006 Find-and-replace all C# namespace declarations from CososWebScraping.* to BizScraper.* across all .cs files in src/ and tests/
- [X] T007 Find-and-replace all using statements from CososWebScraping.* to BizScraper.* across all .cs files in src/ and tests/
- [X] T008 Update error type URIs from cososwebscraping.local to bizscraper.local in src/BizScraper.Api/Middleware/GlobalExceptionHandler.cs, src/BizScraper.Api/Middleware/RequestTimeoutMiddleware.cs, and specs/001-cosos-business-search-api/contracts/openapi.yaml
- [X] T009 Update OpenTelemetry service name in src/BizScraper.Api/appsettings.json and meter name strings in src/BizScraper.Api/Features/BusinessSearch/Metrics/BusinessSearchMetrics.cs and src/BizScraper.Api/Features/EntityDetails/Metrics/EntityDetailsMetrics.cs
- [X] T010 Update Swagger title from "CO SOS Business Entity Search API" to "BizScraper API" in src/BizScraper.Api/Program.cs
- [X] T011 Rename Dockerfile from src/cososwebscraping-dockerfile to src/bizscraper-dockerfile and update internal references (solution name, publish path, entrypoint DLL name)
- [X] T012 Update deploy/pipelines/ci.yaml (solution name, dockerfile path, Docker image tag)
- [X] T013 Rename Helm chart directory from deploy/helm/cososwebscraping/ to deploy/helm/bizscraper/ and update Chart.yaml (name, description) and values.yaml (container image repository)
- [X] T014 Update Liquibase changeset author from cososwebscraping to bizscraper in deploy/liquibase/changelog/ddl/*.sql (4 files)
- [X] T015 Update README.md with new project name, directory references, and build commands
- [X] T016 Update .specify/memory/constitution.md project name references and .github/agents/copilot-instructions.md header
- [X] T017 Update test support string literals referencing CososWebScraping (e.g., ApplicationName in tests/BizScraper.UnitTests/TestSupport/DiagnosticsTestDoubles.cs)
- [X] T018 Validate rename with full build: dotnet build BizScraper.slnx and dotnet test BizScraper.slnx
- [X] T019 Create engine directory structure: src/BizScraper.Api/Infrastructure/Scraping/Engine/, src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/, src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/, src/BizScraper.Api/Infrastructure/Scraping/Definitions/

**Checkpoint**: Project fully renamed and compiles. Engine directories exist.

---

## Phase 2: Foundational (Engine Core & Diagnostics)

**Purpose**: Core engine framework, models, action handler interface, variable substitution, and diagnostics integration. MUST be complete before any user story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T020 [P] Create FlowDefinition, Variable, OutputDeclaration, Action, ScreenshotConfig, and Condition model records in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinition.cs per data-model.md
- [X] T021 [P] Create IActionHandler interface in src/BizScraper.Api/Infrastructure/Scraping/Engine/IActionHandler.cs with method ExecuteAsync(ActionContext context, Action action, CancellationToken ct)
- [X] T022 [P] Create ActionContext class in src/BizScraper.Api/Infrastructure/Scraping/Engine/ActionContext.cs holding IPage, variable bag (Dictionary<string, object?>), correlation ID, and diagnostics references
- [X] T023 [P] Create IPostProcessor interface in src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/IPostProcessor.cs with method ProcessAsync(string rawJson, ActionContext context)
- [X] T024 [P] Create FlowDefinitionLoader in src/BizScraper.Api/Infrastructure/Scraping/Engine/FlowDefinitionLoader.cs to read JSON files from Definitions/ directory, deserialize via source-generated JsonSerializerContext, validate schema version (FR-016), required fields (FR-014), and cache definitions keyed by (state, endpoint)
- [X] T025 Create VariableSubstitution utility in src/BizScraper.Api/Infrastructure/Scraping/Engine/VariableSubstitution.cs implementing ${variableName} replacement in string parameters (FR-004), with fail-fast on unresolved variables
- [X] T026 Create ConditionEvaluator in src/BizScraper.Api/Infrastructure/Scraping/Engine/ConditionEvaluator.cs evaluating element-exists, element-visible, text-contains, variable-truthy, variable-falsy, not conditions (FR-005)
- [X] T027 Create ScrapingFlowEngine in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs implementing the core execution loop: walk actions sequentially, resolve action handler by type, evaluate conditions, substitute variables, log each action (FR-013), capture screenshot milestones (FR-009), and propagate errors (FR-011)
- [X] T028 [P] [US4] Integrate diagnostics into ScrapingFlowEngine: wrap flow execution in ExecuteWithDiagnosticsAsync for trace capture, call CaptureScreenshotAsync after actions with screenshot config, capture forced error screenshot on exception
- [X] T029 [P] Create source-generated JsonSerializerContext for FlowDefinition models in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionJsonContext.cs
- [X] T030 [P] Create source-generated LoggerMessage extension methods for engine logging in src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs (action started, action completed, action skipped, action failed, variable resolved, flow started, flow completed)
- [X] T031 Register FlowDefinitionLoader, ScrapingFlowEngine, ConditionEvaluator, and action handlers in DI in src/BizScraper.Api/Program.cs; call FlowDefinitionLoader.LoadAndValidateAll() at startup with fail-fast on validation errors

**Checkpoint**: Engine framework compiles and wires up. No action handlers yet (they come per user story).

---

## Phase 3: User Story 1 — Colorado Business Search via JSON (Priority: P1) 🎯 MVP

**Goal**: CO business search runs entirely from co-business-search.json, producing identical results to PlaywrightBusinessEntityScraper

**Independent Test**: Issue a CO business search request → engine loads JSON → returns same results as current implementation

### Implementation for User Story 1

- [X] T032 [P] [US1] Implement NavigateActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/NavigateActionHandler.cs (page.GotoAsync with waitUntil parameter)
- [X] T033 [P] [US1] Implement FillActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/FillActionHandler.cs (page.Locator(selector).FillAsync with variable substitution)
- [X] T034 [P] [US1] Implement ClickActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ClickActionHandler.cs (page.Locator(selector).ClickAsync + optional waitAfter)
- [X] T035 [P] [US1] Implement WaitForLoadActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/WaitForLoadActionHandler.cs (page.WaitForLoadStateAsync)
- [X] T036 [P] [US1] Implement ExtractActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ExtractActionHandler.cs (page.EvaluateAsync with javascript, store result in variable bag, invoke optional post-processor)
- [X] T037 [P] [US1] Implement CheckTextActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CheckTextActionHandler.cs (read page body text, match pattern, throw/skip/continue per onMatch)
- [X] T038 [P] [US1] Implement ScreenshotActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ScreenshotActionHandler.cs (CaptureScreenshotAsync with milestone and force flag)
- [X] T039 [US1] Implement LoopActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/LoopActionHandler.cs supporting pagination loopType (iterate nested actions, evaluate terminateWhen condition, respect maxIterations, track page counter variable, inject engine variables currentPage/nextPage/iterationCount, accumulate extraction results into aggregateInto variable, return partial results with truncation indicator on mid-loop failure)
- [X] T040 [US1] Create co-business-search.json flow definition in src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-business-search.json per contracts/example-co-business-search.json, with all selectors, JavaScript extraction, pagination pattern, and screenshot milestones matching current PlaywrightBusinessEntityScraper behavior
- [X] T041 [US1] Configure co-business-search.json as Content/CopyIfNewer in BizScraper.Api.csproj
- [X] T042 [US1] Create JsonDrivenSearchScraper implementing IStateSearchScraper in src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenSearchScraper.cs that loads CO flow definition, populates runtime variables (searchTerm, correlationId, config values), calls ScrapingFlowEngine.ExecuteAsync, and maps output to NormalizedSearchScrapeResult
- [X] T043 [US1] Update DI registration in src/BizScraper.Api/Program.cs to register JsonDrivenSearchScraper as keyed IStateSearchScraper for "CO" replacing the PlaywrightBusinessEntityScraper registration
- [X] T044 [US1] Verify CO business search produces identical results to previous implementation by running manual comparison test with known search terms

**Checkpoint**: Colorado business search works end-to-end via JSON engine. Old PlaywrightBusinessEntityScraper still exists but is no longer wired to CO endpoint.

---

## Phase 4: User Story 2 — Wyoming Business Search via JSON (Priority: P2)

**Goal**: WY business search runs entirely from wy-business-search.json, including CAPTCHA solving

**Independent Test**: Issue a WY business search request → engine loads JSON → handles CAPTCHA → returns same results

### Implementation for User Story 2

- [X] T045 [P] [US2] Implement WaitForConditionActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/WaitForConditionActionHandler.cs (page.WaitForFunctionAsync with javascript and timeoutMs)
- [X] T046 [P] [US2] Implement CallServiceActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CallServiceActionHandler.cs resolving named services from DI (e.g., "captcha-solver" maps to ICaptchaSolver), reading inputVariable, writing outputVariable
- [X] T047 [US2] Extend LoopActionHandler to support retry loopType: iterate nested actions until terminateWhen condition is met or maxIterations exceeded, track attempt counter variable
- [X] T048 [US2] Create WyomingResultPostProcessor implementing IPostProcessor in src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/WyomingResultPostProcessor.cs that wraps the existing ParseResultBlock logic from PlaywrightWyomingSearchScraper (label-value extraction, header parsing)
- [X] T049 [US2] Register WyomingResultPostProcessor in DI with name "wyoming-result-parser" in src/BizScraper.Api/Program.cs
- [X] T050 [US2] Create wy-business-search.json flow definition in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-business-search.json with CAPTCHA detection conditional, retry loop (max 3), call-service action for captcha-solver, search form fill/submit with retry, extract with wyoming-result-parser post-processor, pagination via #MainContent_lbtnNextHeader, and all screenshot milestones matching current PlaywrightWyomingSearchScraper
- [X] T051 [US2] Configure wy-business-search.json as Content/CopyIfNewer in BizScraper.Api.csproj
- [X] T052 [US2] Extend JsonDrivenSearchScraper to handle WY flow definition (load by state, populate WY-specific runtime variables)
- [X] T053 [US2] Update DI registration in src/BizScraper.Api/Program.cs to register JsonDrivenSearchScraper as keyed IStateSearchScraper for "WY" replacing the PlaywrightWyomingSearchScraper registration
- [X] T054 [US2] Verify WY business search produces identical results to previous implementation including CAPTCHA handling, by running manual comparison test

**Checkpoint**: Wyoming business search works end-to-end via JSON engine. Old PlaywrightWyomingSearchScraper still exists but is no longer wired.

---

## Phase 5: User Story 4 — Shared Diagnostics Integration (Priority: P2)

**Goal**: Verify that JSON-driven flows produce the same diagnostic artifacts as the current implementation

**Independent Test**: Run any scraping flow, inspect diagnostics output for screenshots + traces matching milestone names

### Implementation for User Story 4

- [X] T055 [US4] Verify screenshot milestones in co-business-search.json match the milestone names used by PlaywrightBusinessEntityScraper (01-page-load, 02-search-results, 03-page-{n}, error)
- [X] T056 [US4] Verify screenshot milestones in wy-business-search.json match the milestone names used by PlaywrightWyomingSearchScraper (wy-01-page-load, wy-captcha-*, wy-02-search-results, wy-03-page-{n})
- [X] T057 [US4] Verify trace capture wraps the full flow execution in ScrapingFlowEngine and matches the existing tracing behavior (Always/OnFailure/Off modes, cloud upload)
- [X] T058 [US4] Verify forced error screenshots are captured when an action fails, matching the existing behavior in ExecuteWithDiagnosticsAsync

**Checkpoint**: Diagnostics output is identical between JSON-driven and old implementations.

---

## Phase 6: User Story 3 — Colorado Entity Details via JSON (Priority: P3)

**Goal**: CO entity details runs entirely from co-entity-details.json, including certificate download

**Independent Test**: Provide a known entity details URL → engine loads JSON → returns entity details + downloads certificate PDF

### Implementation for User Story 3

- [X] T059 [P] [US3] Implement DownloadActionHandler in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/DownloadActionHandler.cs (page.WaitForDownloadAsync, validate extension, save to diagnostics directory with filename pattern variable substitution, store local path in variable bag)
- [X] T060 [US3] Create co-entity-details.json flow definition in src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-entity-details.json with navigate to details URL, extract Details table, extract Registered Agent table, conditional download of certificate PDF if link exists, and screenshot milestones matching current PlaywrightEntityDetailScraper
- [X] T061 [US3] Configure co-entity-details.json as Content/CopyIfNewer in BizScraper.Api.csproj
- [X] T062 [US3] Create JsonDrivenEntityDetailScraper implementing IEntityDetailScraper in src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs that loads CO entity-details flow definition, populates runtime variables (detailsUrl, correlationId, idNumber), calls ScrapingFlowEngine.ExecuteAsync, and maps output to EntityDetailScrapeResult
- [X] T063 [US3] Update DI registration in src/BizScraper.Api/Program.cs to register JsonDrivenEntityDetailScraper as IEntityDetailScraper replacing the PlaywrightEntityDetailScraper registration
- [X] T064 [US3] Verify screenshot milestones in co-entity-details.json match PlaywrightEntityDetailScraper (01-detail-page-load, 02-after-table-scraping, 03-after-certificate-download)
- [X] T065 [US3] Verify CO entity details produces identical results to previous implementation including certificate download, by running manual comparison test

**Checkpoint**: Colorado entity details works end-to-end via JSON engine. All three flows now use the engine.

---

## Phase 7: Polish & Cleanup

**Purpose**: Remove old scraper classes, final documentation, and cross-cutting cleanup

- [X] T066 Remove PlaywrightBusinessEntityScraper search-specific methods (keep shared infrastructure: browser pool, diagnostics, screenshot capture used by the engine) from src/BizScraper.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs
- [X] T067 Remove PlaywrightWyomingSearchScraper from src/BizScraper.Api/Infrastructure/Scraping/PlaywrightWyomingSearchScraper.cs and associated WY-specific unit tests
- [X] T068 Remove PlaywrightEntityDetailScraper from src/BizScraper.Api/Infrastructure/Scraping/PlaywrightEntityDetailScraper.cs
- [X] T069 Remove IBusinessEntityScraper deprecated interface from src/BizScraper.Api/Common/Interfaces/IBusinessEntityScraper.cs if it still exists
- [X] T070 Clean up DI registrations in src/BizScraper.Api/Program.cs — remove old scraper class registrations that are no longer needed
- [X] T071 Final build validation: dotnet build BizScraper.slnx with zero warnings
- [X] T072 Final test validation: dotnet test BizScraper.slnx with all tests passing
- [X] T073 Run quickstart.md validation: verify build, run, and test commands in specs/006-json-scraping-engine/quickstart.md work correctly
- [X] T074 Run performance comparison: execute equivalent requests against JSON engine vs old scrapers for CO and WY flows, verify <10% latency degradation per SC-006
- [X] T075 Verify SC-002: modify a selector in co-business-search.json, restart service, confirm new selector is used without recompilation
- [X] T076 Verify SC-003: create a minimal dummy state JSON definition, confirm engine loads it without engine code changes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — rename and create directories first
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories
- **US1 CO Search (Phase 3)**: Depends on Phase 2 — first flow to validate the engine
- **US2 WY Search (Phase 4)**: Depends on Phase 2; can run in parallel with Phase 3 after T039 (LoopActionHandler) exists
- **US4 Diagnostics (Phase 5)**: Depends on Phases 3 and 4 — verifies milestone parity after flows are migrated
- **US3 CO Details (Phase 6)**: Depends on Phase 2; can run in parallel with Phases 4-5 after core action handlers exist
- **Polish (Phase 7)**: Depends on all user story phases being complete and verified

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational. Creates core action handlers (navigate, fill, click, extract, check-text, screenshot, loop/pagination) reused by US2 and US3.
- **US2 (P2)**: Depends on Foundational. Adds wait-for-condition, call-service, loop/retry. Reuses action handlers from US1.
- **US4 (P2)**: Verification-only phase — depends on US1 and US2 being complete.
- **US3 (P3)**: Depends on Foundational. Adds download action handler. Reuses all other handlers from US1/US2.

### Within Each User Story

- Action handlers marked [P] can be implemented in parallel
- Flow definition JSON depends on action handlers being available
- Scraper adapter (JsonDriven*Scraper) depends on flow definition
- DI registration depends on adapter
- Verification is the final step

### Parallel Opportunities per User Story

**US1** (Phase 3): T032–T038 (7 action handlers) can all be built in parallel. Then T039 (loop) → T040 (JSON) → T042 (adapter) → T043 (DI) → T044 (verify) sequentially.

**US2** (Phase 4): T045–T046 (2 new handlers) in parallel. Then T047 (loop/retry) → T048–T049 (post-processor) → T050 (JSON) → T052 (adapter) → T053 (DI) → T054 (verify) sequentially.

**US3** (Phase 6): T059 (download handler) → T060 (JSON) → T062 (adapter) → T063 (DI) → T065 (verify) sequentially.

---

## Implementation Strategy

### MVP Scope
**User Story 1 (CO Business Search)** is the MVP. After Phase 3, the engine is proven and CO search works via JSON definitions. This delivers SC-002 (selector changes without recompilation) for the simplest flow.

### Incremental Delivery
1. Phase 1 + 2: Rename + engine framework (no behavior change)
2. Phase 3: CO search migrated (MVP)
3. Phase 4: WY search migrated (CAPTCHA + advanced features)
4. Phase 5: Diagnostics verified across both flows
5. Phase 6: CO entity details migrated (all flows complete)
6. Phase 7: Old code removed, final validation
