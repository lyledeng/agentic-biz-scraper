# Tasks: Extract Site-Specific Code from Generic Scraping Engine

**Input**: Design documents from `/specs/038-engine-site-code-extraction/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle I (Test-First). Test tasks precede implementation in each phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new abstractions, context record, registries, and DI auto-discovery infrastructure that all user stories depend on.

- [X] T001 Create `IPostFlowDocumentProcessor` interface and `PostFlowDocumentContext` record in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/IPostFlowDocumentProcessor.cs` — interface with `SlugPrefix` property and `ProcessAsync(Dictionary<string, object?>, PostFlowDocumentContext, CancellationToken)` method per contracts/interface-contracts.md
- [X] T002 [P] Create `PostFlowDocumentProcessorRegistry` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/PostFlowDocumentProcessorRegistry.cs` — constructor takes `IEnumerable<IPostFlowDocumentProcessor>`, builds dictionary keyed by `SlugPrefix` (case-insensitive), `GetProcessor(string definitionSlug)` extracts slug prefix and returns match or null, `ExtractSlugPrefix` splits on `-` and returns first two segments
- [X] T003 [P] Create `ISearchResultMapper` interface in `src/BizScraper.Api/Features/ExecuteScript/Mappers/ISearchResultMapper.cs` — `SlugPrefix` property and `Map(JsonElement output)` returning `UnifiedSearchResult[]?`
- [X] T004 [P] Create `IEntityDetailMapper` interface in `src/BizScraper.Api/Features/ExecuteScript/Mappers/IEntityDetailMapper.cs` — `SlugPrefix` property and `Map(JsonElement output)` returning `UnifiedEntityDetailResponse?`
- [X] T005 Create `MapperRegistry` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/MapperRegistry.cs` — constructor takes `IEnumerable<ISearchResultMapper>` and `IEnumerable<IEntityDetailMapper>`, builds dictionaries keyed by `SlugPrefix`, `GetSearchMapper` / `GetDetailMapper` return match or passthrough fallback, include `PassthroughSearchMapper` and `PassthroughEntityDetailMapper` as private nested classes
- [X] T006 [P] Create `ServiceCollectionExtensions.AddAllImplementations<T>()` in `src/BizScraper.Api/Common/Extensions/ServiceCollectionExtensions.cs` — assembly-scanning extension method per research.md R-002

**Checkpoint**: Core abstractions and registries exist. Per-state implementations can now be created.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Unit tests for registries and DI infrastructure that MUST pass before any user story implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T007 [P] Unit tests for `PostFlowDocumentProcessorRegistry` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/DocumentProcessors/PostFlowDocumentProcessorRegistryTests.cs` — test `ExtractSlugPrefix` ("us-co-entity-details" → "us-co", "de-de-business-search" → "de-de"), test `GetProcessor` returns correct processor for slug, test returns null for unregistered slug, test case-insensitive matching
- [X] T008 [P] Unit tests for `MapperRegistry` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/MapperRegistryTests.cs` — test `GetSearchMapper` returns correct mapper for known slug, test returns passthrough for unknown slug, test `GetDetailMapper` returns correct mapper for known slug, test returns passthrough for unknown slug, test `ExtractSlugPrefix` consistency
- [X] T009 [P] Unit tests for `ServiceCollectionExtensions.AddAllImplementations` in `tests/BizScraper.UnitTests/Common/Extensions/ServiceCollectionExtensionsTests.cs` — test discovers implementations in assembly, test skips abstract classes, test skips interfaces, test registers with correct lifetime

**Checkpoint**: Foundation infrastructure tested — user story implementation can now begin.

---

## Phase 3: User Story 1 — Extract Post-Flow Document Processors (Priority: P1) 🎯 MVP

**Goal**: Extract `ProcessHistoryDocumentsAsync` (WY), `ProcessMoFilingDocumentsAsync` (MO), `ProcessWaFilingDocumentsAsync` (WA), `ProcessCoCertifiedDocumentsAsync` (CO) from `GenericScriptScraper.cs` into per-state `IPostFlowDocumentProcessor` implementations. Refactor `DeEntityDetailsProcessor` into `DeDocumentProcessor`. Replace 4 conditional post-flow blocks with registry-based loop.

**Independent Test**: After extraction, `GenericScriptScraper.cs` contains zero state-specific document processing code. All existing document download unit tests pass unchanged.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T010 [P] [US1] Unit tests for `WyDocumentProcessor` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/DocumentProcessors/WyDocumentProcessorTests.cs` — test `SlugPrefix` returns "us-wy", test processes historyDocuments from output (mocked IBrowserContext.APIRequest + IBlobStorageClient), test no-op when output lacks historyDocuments key, test error on download failure does not throw (logs and continues)
- [X] T011 [P] [US1] Unit tests for `MoDocumentProcessor` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/DocumentProcessors/MoDocumentProcessorTests.cs` — test `SlugPrefix` returns "us-mo", test processes filings with page-level fetch (mocked IPage.EvaluateAsync), test handles TIFF and PDF content types, test no-op when output lacks filings key
- [X] T012 [P] [US1] Unit tests for `WaDocumentProcessor` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/DocumentProcessors/WaDocumentProcessorTests.cs` — test `SlugPrefix` returns "us-wa", test processes filings with page-level fetch, test respects maxFilingDocuments from definition variables, test no-op when output lacks filings key
- [X] T013 [P] [US1] Unit tests for `CoDocumentProcessor` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/DocumentProcessors/CoDocumentProcessorTests.cs` — test `SlugPrefix` returns "us-co", test processes certifiedDocumentsViewerUrl (mocked IBrowserContext.APIRequest), test no-op when output lacks certifiedDocumentsViewerUrl key
- [X] T014 [P] [US1] Unit tests for `DeDocumentProcessor` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/DocumentProcessors/DeDocumentProcessorTests.cs` — test `SlugPrefix` returns "de-de", test processes DE entity details output (mocked IDocumentTranslator + IMarkdownToPdfConverter), test no-op when output has no translatable content

### Implementation for User Story 1

- [X] T015 [P] [US1] Create `WyDocumentProcessor` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/WyDocumentProcessor.cs` — extract `ProcessHistoryDocumentsAsync` logic from `GenericScriptScraper.cs`, implement `IPostFlowDocumentProcessor` with `SlugPrefix = "us-wy"`, use `context.BrowserContext` for API requests, `context.BlobStorage` for upload, `context.HttpContextAccessor` for proxy URLs
- [X] T016 [P] [US1] Create `MoDocumentProcessor` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/MoDocumentProcessor.cs` — extract `ProcessMoFilingDocumentsAsync` logic from `GenericScriptScraper.cs`, implement `IPostFlowDocumentProcessor` with `SlugPrefix = "us-mo"`, use `context.Page` for page-level fetch
- [X] T017 [P] [US1] Create `WaDocumentProcessor` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/WaDocumentProcessor.cs` — extract `ProcessWaFilingDocumentsAsync` logic from `GenericScriptScraper.cs`, implement `IPostFlowDocumentProcessor` with `SlugPrefix = "us-wa"`, read `maxFilingDocuments` from `context.Definition.Variables`
- [X] T018 [P] [US1] Create `CoDocumentProcessor` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/CoDocumentProcessor.cs` — extract `ProcessCoCertifiedDocumentsAsync` logic from `GenericScriptScraper.cs`, implement `IPostFlowDocumentProcessor` with `SlugPrefix = "us-co"`, use `context.BrowserContext` for API requests
- [X] T019 [P] [US1] Create `DeDocumentProcessor` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/DocumentProcessors/DeDocumentProcessor.cs` — refactor `DeEntityDetailsProcessor` logic, implement `IPostFlowDocumentProcessor` with `SlugPrefix = "de-de"`, inject `IDocumentTranslator`, `IMarkdownToPdfConverter`, `IPlaywrightPageFactory` via constructor DI
- [X] T020 [US1] Replace 4 conditional post-flow blocks in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` with registry-based loop — inject `PostFlowDocumentProcessorRegistry`, build `PostFlowDocumentContext` from existing variables, call `registry.GetProcessor(slug)?.ProcessAsync()` wrapped in try-catch per contracts/interface-contracts.md, delete `ProcessHistoryDocumentsAsync`, `ProcessMoFilingDocumentsAsync`, `ProcessWaFilingDocumentsAsync`, `ProcessCoCertifiedDocumentsAsync` static methods and their LoggerMessage declarations
- [X] T021 [US1] Remove `DeEntityDetailsProcessor` dependency and DE-specific branching from `src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs` — remove `deEntityDetailsProcessor` constructor parameter, remove `IsDeEntityDetails()` method, remove DE post-processing block (lines 93-126). DE processing now handled by `DeDocumentProcessor` in engine post-flow loop
- [X] T022 [US1] Delete `src/BizScraper.Api/Features/ExecuteScript/Handlers/DeEntityDetailsProcessor.cs` — logic moved to `DeDocumentProcessor`

**Checkpoint**: US1 complete — `GenericScriptScraper.cs` has zero state-specific document processing code. All post-flow processing runs through registry. Verify all existing document download tests pass.

---

## Phase 4: User Story 2 — Extract State-Specific Mappers (Priority: P1)

**Goal**: Extract 6 search result mapping methods and 6 entity detail mapping methods from monolithic mappers into per-state classes behind `ISearchResultMapper` and `IEntityDetailMapper`. Consolidate `WyomingResultPostProcessor` into `WySearchResultMapper`. Wire `MapperRegistry` into handler.

**Independent Test**: After extraction, `SearchResultMapper.cs` and `EntityDetailMapper.cs` contain zero state-specific branches. All existing mapper unit tests pass unchanged.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T023 [P] [US2] Unit tests for `CoSearchResultMapper` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/Search/CoSearchResultMapperTests.cs` — test `SlugPrefix` returns "us-co", test maps CO search output to `UnifiedSearchResult[]` identically to current `SearchResultMapper.MapCoResults`
- [X] T024 [P] [US2] Unit tests for `WySearchResultMapper` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/Search/WySearchResultMapperTests.cs` — test `SlugPrefix` returns "us-wy", test maps WY search output including consolidated `WyomingResultPostProcessor.ParseResultBlock` logic, test handles result blocks with status/standing/filed-on labels
- [X] T025 [P] [US2] Unit tests for `IaSearchResultMapper` in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/Search/IaSearchResultMapperTests.cs` — test `SlugPrefix` returns "us-ia", test maps IA search output with Iowa-specific field patterns
- [X] T026 [P] [US2] Unit tests for `MoSearchResultMapper`, `WaSearchResultMapper`, `DeSearchResultMapper` in respective `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/Search/` files — test `SlugPrefix` correctness and mapping parity with current per-state methods
- [X] T027 [P] [US2] Unit tests for all 6 entity detail mappers (Co, Wy, Ia, Mo, Wa, De) in respective `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/Details/` files — test `SlugPrefix` correctness and mapping parity with current `EntityDetailMapper.Map{State}Details` methods

### Implementation for User Story 2

- [X] T028 [P] [US2] Create `CoSearchResultMapper` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/Search/CoSearchResultMapper.cs` — extract `MapCoResults` logic from `SearchResultMapper.cs`, implement `ISearchResultMapper` with `SlugPrefix = "us-co"`
- [X] T029 [P] [US2] Create `WySearchResultMapper` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/Search/WySearchResultMapper.cs` — extract `MapWyResults` logic AND consolidate `WyomingResultPostProcessor.ParseResultBlock` parsing, implement `ISearchResultMapper` with `SlugPrefix = "us-wy"`
- [X] T030 [P] [US2] Create `IaSearchResultMapper` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/Search/IaSearchResultMapper.cs` — extract `MapIaResults`, implement `ISearchResultMapper` with `SlugPrefix = "us-ia"`
- [X] T031 [P] [US2] Create `MoSearchResultMapper`, `WaSearchResultMapper`, `DeSearchResultMapper` in respective `src/BizScraper.Api/Features/ExecuteScript/Mappers/Search/` files — extract corresponding methods, implement `ISearchResultMapper` with correct `SlugPrefix` values ("us-mo", "us-wa", "de-de")
- [X] T032 [P] [US2] Create all 6 entity detail mappers (`CoEntityDetailMapper`, `WyEntityDetailMapper`, `IaEntityDetailMapper`, `MoEntityDetailMapper`, `WaEntityDetailMapper`, `DeEntityDetailMapper`) in respective `src/BizScraper.Api/Features/ExecuteScript/Mappers/Details/` files — extract corresponding `Map{State}Details` methods, implement `IEntityDetailMapper` with correct `SlugPrefix` values
- [X] T033 [US2] Refactor `SearchResultMapper` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs` — replace constructor to inject `MapperRegistry`, change `MapToUnified` to delegate: `registry.GetSearchMapper(definitionSlug).Map(output)`, delete all 6 private `Map{State}Results` methods
- [X] T034 [US2] Refactor `EntityDetailMapper` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs` — replace constructor to inject `MapperRegistry`, change `MapToUnified` to delegate: `registry.GetDetailMapper(definitionSlug).Map(output)`, delete all 6 private `Map{State}Details` methods
- [X] T035 [US2] Remove `IPostProcessor` infrastructure — delete `src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/IPostProcessor.cs` and `src/BizScraper.Api/Infrastructure/Scraping/Engine/PostProcessors/WyomingResultPostProcessor.cs`, remove `IEnumerable<IPostProcessor>` injection and post-processor dictionary from `src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ExtractActionHandler.cs`, remove `postProcessor` field from WY definition extract actions in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-business-search.json`

**Checkpoint**: US2 complete — `SearchResultMapper` and `EntityDetailMapper` are thin delegators. `IPostProcessor` is fully removed. All mapper tests pass.

---

## Phase 5: User Story 3 — Remove State-Specific URL Injection (Priority: P2)

**Goal**: Move WY, IA, DE search URLs from `PlaywrightOptions` into definition variables as `searchEntryUrl`. Remove hardcoded URL injection from engine. Clean up `PlaywrightOptions`.

**Independent Test**: After changes, `PlaywrightOptions` has zero state-specific URL properties. All search definitions work with URLs from their own variables.

### Implementation for User Story 3

- [X] T036 [P] [US3] Update `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-wy-business-search.json` — rename variable `wyomingSearchUrl` to `searchEntryUrl`, add `"defaultValue": "https://wyobiz.wyo.gov/Business/FilingSearch.aspx"`, update all action script references from `{{wyomingSearchUrl}}` to `{{searchEntryUrl}}`
- [X] T037 [P] [US3] Update `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json` — rename variable `iowaSearchUrl` to `searchEntryUrl`, add `"defaultValue": "https://sos.iowa.gov/search/business/Search.aspx"`, update all action script references from `{{iowaSearchUrl}}` to `{{searchEntryUrl}}`
- [X] T038 [P] [US3] Update `src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-de-business-search.json` — rename variable `germanySearchUrl` to `searchEntryUrl`, add `"defaultValue": "https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml"`, update all action script references from `{{germanySearchUrl}}` to `{{searchEntryUrl}}`
- [X] T039 [US3] Remove state-specific URL injection from `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — delete lines that inject `wyomingSearchUrl`, `iowaSearchUrl`, `germanySearchUrl` into `actionContext.Variables` (lines ~330-334). Keep generic `searchEntryUrl` and `maxPages` injection. Note: the generic `searchEntryUrl` injection from `PlaywrightOptions.SearchEntryUrl` is intentionally retained — it serves as the config-source resolver for CO and any definition that declares `searchEntryUrl` with `source: "config"` without a `defaultValue`.
- [X] T039a [US3] Add validation to `FlowDefinitionLoader.LoadAndValidateAll` (or a post-load check) ensuring that every search definition (`endpoint: "business-search"`) declares a `searchEntryUrl` variable — fail fast at startup with a clear error message if missing
- [X] T040 [US3] Remove state-specific URL properties from `src/BizScraper.Api/Common/Configuration/PlaywrightOptions.cs` — delete `WyomingSearchUrl`, `IowaSearchUrl`, `GermanySearchUrl` properties

**Checkpoint**: US3 complete — all URLs resolved from definitions. `PlaywrightOptions` is clean.

---

## Phase 6: User Story 4 — Delete V1 Scrapers and Legacy Code (Priority: P2)

**Goal**: Delete `JsonDrivenSearchScraper` and `JsonDrivenEntityDetailScraper` entirely. Remove `IStateSearchScraper`, `IEntityDetailScraper` v1 interfaces. Clean up all DI registrations and v1 references in `Program.cs`.

**Independent Test**: After deletion, the codebase has zero references to `JsonDrivenSearchScraper` or `JsonDrivenEntityDetailScraper`. Build succeeds. All tests pass.

### Implementation for User Story 4

- [X] T041 [P] [US4] Delete `src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenSearchScraper.cs`
- [X] T042 [P] [US4] Delete `src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs`
- [X] T043 [P] [US4] Delete v1 interfaces `src/BizScraper.Api/Common/Interfaces/IStateSearchScraper.cs` and `src/BizScraper.Api/Common/Interfaces/IEntityDetailScraper.cs`
- [X] T044 [US4] Remove all `AddKeyedSingleton<IStateSearchScraper>` factory registrations from `src/BizScraper.Api/Program.cs` — delete CO, WY, and any other keyed singleton scraper factories
- [X] T045 [US4] Delete corresponding v1 scraper test files — `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/JsonDrivenSearchScraperTests.cs` and `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraperTests.cs` (if they exist)
- [X] T046 [US4] Search for and remove any remaining references to deleted classes or interfaces in the codebase — verify build succeeds with zero errors
- [X] T046a [US4] Verify WY entity-details end-to-end flow still works after v1 deletion — confirm filing ID resolution, certificate download, and history document processing all operate correctly via V2 `GenericScriptScraper` + `WyDocumentProcessor`. This validates that FR-008 logic is fully handled by V2 definitions and the new processor.

**Checkpoint**: US4 complete — all v1 code deleted. Build and tests pass.

---

## Phase 7: User Story 5 — Dynamic DI Discovery and State Prefix Resolution (Priority: P3)

**Goal**: Replace manual DI registrations with `AddAllImplementations<T>()` calls. Replace hardcoded `GetExpectedStatePrefix()` with slug-derived computation. Remove `DeEntityDetailsProcessor` registration.

**Independent Test**: After changes, `Program.cs` has zero per-state registrations. Adding a new mapper/processor class auto-registers it without code changes.

### Implementation for User Story 5

- [X] T047 [US5] Replace manual DI registrations in `src/BizScraper.Api/Program.cs` — remove `AddSingleton<IPostProcessor, WyomingResultPostProcessor>`, `AddScoped<DeEntityDetailsProcessor>`, and all `AddKeyedSingleton<IStateSearchScraper>` factories; add `AddAllImplementations<IPostFlowDocumentProcessor>()`, `AddAllImplementations<ISearchResultMapper>()`, `AddAllImplementations<IEntityDetailMapper>()`, `AddSingleton<PostFlowDocumentProcessorRegistry>()`, `AddSingleton<MapperRegistry>()`; keep `AddSingleton<SearchResultMapper>` and `AddSingleton<EntityDetailMapper>` (now thin delegators taking `MapperRegistry` as constructor dependency)
- [X] T048 [US5] Replace `GetExpectedStatePrefix()` in `src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs` — change from hardcoded 6-state switch to slug-derived computation: `slug.Split('-') → parts.Length >= 2 ? $"{parts[0]}-{parts[1]}".ToUpperInvariant() : null`
- [X] T049 [US5] Update `KnownStates` in `src/BizScraper.Api/Common/UniqueKeyEncoder.cs` (if applicable) — populate from `FlowDefinitionLoader.SlugIndex` at startup instead of hardcoded `HashSet`, or derive dynamically from slug prefix

**Checkpoint**: US5 complete — zero manual per-state registrations. State prefix resolution is data-driven.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Validation, cleanup, and documentation.

- [X] T050 [P] Verify SC-001: `GenericScriptScraper.cs` contains zero string literals matching any state identifier, URL, or definition slug — grep for "us-co", "us-wy", "us-ia", "us-mo", "us-wa", "de-de", "Wyoming", "Colorado", "Iowa", "Missouri", "Washington", "Germany"
- [X] T051 [P] Verify SC-004: `SearchResultMapper.cs` and `EntityDetailMapper.cs` contain zero state-specific mapping branches — confirm only delegator logic remains
- [X] T052 [P] Verify SC-005: Run full test suite (`dotnet test`) — all 326+ unit tests and 45 integration tests pass with zero modifications to test assertions
- [X] T053 [P] Verify SC-006: Confirm adding a hypothetical new state requires zero modifications to existing source files — review that only new files would be needed
- [X] T054 [P] Verify SC-008: `Program.cs` contains zero per-state scraper factory registrations
- [X] T055 [P] Update `specs/038-engine-site-code-extraction/quickstart.md` with final verification results
- [X] T056 Run quickstart.md validation — verify all 6 states' search and entity-detail definitions work end-to-end with identical API responses

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately. T001 first (defines interface), then T002–T006 in parallel
- **Phase 2 (Foundational)**: Depends on Phase 1 completion. T007 ∥ T008 ∥ T009 (all parallel, different files)
- **Phase 3 (US1 — Doc Processors)**: Depends on Phase 2. Tests first (T010–T014 parallel), then implementations (T015–T019 parallel), then engine wiring (T020 → T021 → T022)
- **Phase 4 (US2 — Mappers)**: Depends on Phase 2. Can start in parallel with Phase 3 if staffed. Tests first (T023–T027), then implementations (T028–T032 parallel), then refactoring (T033, T034, T035)
- **Phase 5 (US3 — URLs)**: Depends on Phase 3 (engine must be refactored first). T036 ∥ T037 ∥ T038 (parallel, different files), then T039 → T040
- **Phase 6 (US4 — V1 Deletion)**: Depends on Phase 3 (all execution must flow through V2 before v1 can be deleted). Independent of Phase 5. T041 ∥ T042 ∥ T043 (parallel deletes), then T044 → T045 → T046 → T046a
- **Phase 7 (US5 — Dynamic DI)**: Depends on Phases 3, 4, 6 (all implementations must exist, v1 removed). T047 → T048 → T049
- **Phase 8 (Polish)**: Depends on all previous phases. T050–T056 mostly parallel

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational (Phase 2) — standalone MVP
- **US2 (P1)**: Depends only on Foundational (Phase 2) — can run in parallel with US1
- **US3 (P2)**: Depends on US1 (engine refactored) — URL injection removed after post-flow blocks removed
- **US4 (P2)**: Depends on US1 (all execution must flow through V2 engine before v1 can be deleted). Independent of US3 — v1 scrapers read URLs at runtime, not at compile time.
- **US5 (P3)**: Depends on US1 + US2 + US4 — all implementations exist, v1 removed, ready for DI consolidation

### Within User Story 1

1. T010 ∥ T011 ∥ T012 ∥ T013 ∥ T014 — Write all 5 processor test files (parallel, different files)
2. T015 ∥ T016 ∥ T017 ∥ T018 ∥ T019 — Create all 5 processor implementations (parallel, different files)
3. T020 — Replace engine conditional blocks with registry loop (depends on T015–T019)
4. T021 — Clean handler (depends on T019 for DE processor existing)
5. T022 — Delete old DE processor (depends on T021)

### Within User Story 2

1. T023 ∥ T024 ∥ T025 ∥ T026 ∥ T027 — Write all mapper test files (parallel)
2. T028 ∥ T029 ∥ T030 ∥ T031 ∥ T032 — Create all mapper implementations (parallel)
3. T033 ∥ T034 — Refactor delegator mappers (parallel, different files)
4. T035 — Remove IPostProcessor infrastructure (depends on T029 for WY consolidation, T033 for mapper wiring)

### Parallel Opportunities

```text
# Phase 1 (after T001):
T002 ∥ T003 ∥ T004 ∥ T006

# Phase 2:
T007 ∥ T008 ∥ T009

# Phase 3 + Phase 4 can run in parallel if staffed:

# Stream A (US1 — document processors):
T010 ∥ T011 ∥ T012 ∥ T013 ∥ T014 → T015 ∥ T016 ∥ T017 ∥ T018 ∥ T019 → T020 → T021 → T022

# Stream B (US2 — mappers, parallel with Stream A):
T023 ∥ T024 ∥ T025 ∥ T026 ∥ T027 → T028 ∥ T029 ∥ T030 ∥ T031 ∥ T032 → T033 ∥ T034 → T035

# After Streams A + B complete:
T036 ∥ T037 ∥ T038 → T039 → T039a → T040 (US3)

# After Stream A completes (independent of US3):
T041 ∥ T042 ∥ T043 → T044 → T045 → T046 → T046a (US4)

T047 → T048 → T049 (US5, after US1+US2+US4)

# Polish:
T050 ∥ T051 ∥ T052 ∥ T053 ∥ T054 ∥ T055 → T056
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 Only)

1. Complete Phase 1: Create abstractions and registries (T001–T006)
2. Complete Phase 2: Test registries (T007–T009)
3. Complete Phase 3: Extract document processors (T010–T022)
4. Complete Phase 4: Extract mappers (T023–T035)
5. **STOP and VALIDATE**: Run full test suite — all 371+ tests pass. `GenericScriptScraper.cs` and mapper classes are clean.
6. Deploy if ready — engine is generic, all behaviour preserved.

### Incremental Delivery

1. Phase 1 + 2 → Infrastructure ready
2. Phase 3 (US1) → Document processors extracted → **MVP milestone 1**
3. Phase 4 (US2) → Mappers extracted → **MVP milestone 2**
4. Phase 5 (US3) → URLs moved to definitions → Engine fully definition-driven
5. Phase 6 (US4) → V1 scrapers deleted → Dead code removed
6. Phase 7 (US5) → Dynamic DI → Zero per-state registration
7. Phase 8 → Validation → Feature complete

### Parallel Team Strategy

With two developers:

1. Both complete Phase 1 + Phase 2 together
2. Developer A: Phase 3 (US1 — document processors)
3. Developer B: Phase 4 (US2 — mappers)
4. Reconvene for Phases 5–8 (sequential, lower risk)

---

## Notes

- All per-state implementations in Phases 3–4 are marked [P] because they touch different files — safe to create in parallel
- WY search mapper (T029) consolidates `WyomingResultPostProcessor.ParseResultBlock` logic — verify the raw text parsing produces identical results
- DE document processor (T019) has different DI needs than other processors (IDocumentTranslator, IMarkdownToPdfConverter) — these are constructor-injected, not from PostFlowDocumentContext
- Phase 5 definition changes (T036–T038) require updating both the variable names AND any `{{variableName}}` references in action scripts within the same JSON files
- Phase 6 file deletions (T041–T043) should be verified by building the project — any remaining references will cause compile errors
- The `postProcessor` field removal from WY definition (T035) must coordinate with `WySearchResultMapper` (T029) being ready to handle parsing
- T048 slug-derived `GetExpectedStatePrefix` handles all current and future states without modification
- No database migrations, no API schema changes, no Helm chart changes
