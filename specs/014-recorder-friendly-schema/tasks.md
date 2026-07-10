# Tasks: Recorder-Friendly JSON Schema (V2)

**Input**: Design documents from `/specs/014-recorder-friendly-schema/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1–US7) — Setup/Foundational/Polish phases have no story label

---

## Phase 1: Setup

**Purpose**: V2 model records, JSON serialization context, and shared exception types — the foundation everything else depends on

- [x] T001 Define `FlowDefinitionV2`, `FlowMetadata`, `FlowEnvironment`, `ViewportConfig`, `RetryConfig` records in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs
- [x] T002 Define `FlowActionV2`, `ActionTarget`, `SelectorEntry`, `ActionMetadata`, `ConditionV2`, `ScriptDefinition`, `VariableV2`, `OutputDeclarationV2` records in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs
- [x] T003 [P] Define `SchemaVersionProbe` record for two-pass version detection in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs
- [x] T004 [P] Define `SchemaViolation` record for output validation results in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs
- [x] T005 Create `FlowDefinitionV2JsonContext` source-generated serializer context registering all v2 types (FlowDefinitionV2, SchemaVersionProbe, etc.) in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2JsonContext.cs
- [x] T006 [P] Define `TargetResolutionException` custom exception in src/BizScraper.Api/Infrastructure/Scraping/Engine/TargetResolver.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core new components that multiple user stories depend on — must complete before any story work

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T007 Implement `V1Normalizer` static class mapping v1 `FlowDefinition` → `FlowDefinitionV2` — map name/state/endpoint → metadata, flat selector → single-entry ActionTarget (strategy=css, confidence=1.0), assign sequential action id/order, map Condition → ConditionV2 recursively, map Variable → VariableV2 with null defaults, map loop sub-actions recursively in src/BizScraper.Api/Infrastructure/Scraping/Engine/V1Normalizer.cs
- [x] T008 [P] Write unit tests for `V1Normalizer` — verify metadata mapping, selector-to-target conversion, variable enrichment, condition conversion (including nested not), loop sub-action normalization, output mapping in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/V1NormalizerTests.cs
- [x] T009 Implement `TargetResolver` internal sealed class — sort selectors by descending confidence, map strategy to Playwright locator (css→Locator, xpath→Locator("xpath=..."), role→GetByRole, name→Locator), per-selector timeout via `Task.WhenAny` with configurable `selectorTimeoutMs` (default 5000ms), log each selector probe attempt and outcome (strategy, value, success/timeout) for observability, throw `TargetResolutionException` when all selectors exhausted listing attempted selectors in src/BizScraper.Api/Infrastructure/Scraping/Engine/TargetResolver.cs
- [x] T010 [P] Write unit tests for `TargetResolver` — highest-confidence selector used first, fallback on failure, timeout per probe enforced, unknown strategy skipped, all-fail throws with attempted list in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/TargetResolverTests.cs
- [x] T011 Implement `ScriptResolver` internal static class — resolve `scriptRef` from scripts dictionary, fall back to inline `javascript`, throw `InvalidOperationException` when neither set in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScriptResolver.cs
- [x] T012 [P] Write unit tests for `ScriptResolver` — scriptRef resolved correctly, scriptRef missing throws, inline javascript returned, neither set throws in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScriptResolverTests.cs
- [x] T013 Update `FlowDefinitionLoader` with two-pass deserialization — deserialize `SchemaVersionProbe` first, if `SchemaVersion` is Number → v1 path (deserialize FlowDefinition then V1Normalizer), if String → v2 path (deserialize FlowDefinitionV2 directly), store `FlowDefinitionV2` in internal dictionary, update `GetDefinition` return type to `FlowDefinitionV2` in src/BizScraper.Api/Infrastructure/Scraping/Engine/FlowDefinitionLoader.cs
- [x] T014 Add v2-specific validation to `FlowDefinitionLoader` — validate metadata required fields (id, name, state, endpoint non-empty), validate unique action IDs across all actions including loop sub-actions, validate scriptRef references exist in scripts dictionary, validate scriptRef and javascript mutual exclusivity, validate target.selectors non-empty, validate known selector strategies (css/xpath/role/name) in src/BizScraper.Api/Infrastructure/Scraping/Engine/FlowDefinitionLoader.cs
- [x] T015 [P] Write unit tests for `FlowDefinitionLoader` v2 support — v1 detection and normalization, v2 detection and direct loading, unsupported version rejected, duplicate action IDs rejected, missing script reference rejected, empty selectors rejected, unknown strategy rejected in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/FlowDefinitionLoaderTests.cs
- [x] T016 Update `ActionContext` to include `FlowEnvironment?` property and `IReadOnlyDictionary<string, ScriptDefinition>?` scripts property for handler access in src/BizScraper.Api/Infrastructure/Scraping/Engine/ActionContext.cs
- [x] T017 Update `EngineLogging` source-generated `[LoggerMessage]` methods to include optional action ID parameter alongside action index in log templates in src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs

**Checkpoint**: Foundation ready — all new types, resolver services, loader v2 support, and context updates in place

---

## Phase 3: User Story 5 — Backward Compatibility with V1 Definitions (Priority: P1)

**Goal**: Existing v1 definitions load via V1Normalizer and produce identical results through the single v2 execution path

**Independent Test**: Run full test suite with existing v1 JSON files loaded through the v2 engine — zero regressions

- [x] T018 [US5] Update `IActionHandler` interface to accept `FlowActionV2` parameter type instead of `FlowAction` in src/BizScraper.Api/Infrastructure/Scraping/Engine/IActionHandler.cs
- [x] T019 [US5] Update `ScrapingFlowEngine` to accept `FlowDefinitionV2` — update `ExecuteAsync` and `ExecuteActionsAsync` signatures, iterate `FlowActionV2` list, pass `FlowEnvironment` and scripts via `ActionContext`, pass action `Id` to lifecycle log methods (start/complete per action) in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T020 [US5] Update `ConditionEvaluator` to accept `ConditionV2` — support both `Target` (via TargetResolver) and legacy `Selector` for element-exists/element-visible conditions, inject `TargetResolver` via constructor in src/BizScraper.Api/Infrastructure/Scraping/Engine/ConditionEvaluator.cs
- [x] T021 [P] [US5] Update `ClickActionHandler` to accept `FlowActionV2` — use `TargetResolver` when `action.Target` is set, fall back to `action.Selector` for v1-normalized actions in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ClickActionHandler.cs
- [x] T022 [P] [US5] Update `FillActionHandler` to accept `FlowActionV2` — use `TargetResolver` when `action.Target` is set, fall back to `action.Selector` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/FillActionHandler.cs
- [x] T023 [P] [US5] Update `ExtractActionHandler` to accept `FlowActionV2` — use `ScriptResolver` to resolve javascript from `scriptRef` or inline `javascript`, pass scripts dict from `ActionContext` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ExtractActionHandler.cs
- [x] T024 [P] [US5] Update `NavigateActionHandler` to accept `FlowActionV2` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/NavigateActionHandler.cs
- [x] T025 [P] [US5] Update `CheckTextActionHandler` to accept `FlowActionV2` — use `TargetResolver` for `action.Target` when evaluating text on elements in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CheckTextActionHandler.cs
- [x] T026 [P] [US5] Update `WaitForLoadActionHandler` to accept `FlowActionV2` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/WaitForLoadActionHandler.cs
- [x] T027 [P] [US5] Update `WaitForConditionActionHandler` to accept `FlowActionV2` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/WaitForConditionActionHandler.cs
- [x] T028 [P] [US5] Update `ScreenshotActionHandler` to accept `FlowActionV2` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ScreenshotActionHandler.cs
- [x] T029 [P] [US5] Update `DownloadActionHandler` to accept `FlowActionV2` — use `TargetResolver` for link element when `action.Target` is present in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/DownloadActionHandler.cs
- [x] T030 [P] [US5] Update `CallServiceActionHandler` to accept `FlowActionV2` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CallServiceActionHandler.cs
- [x] T031 [US5] Update `LoopActionHandler` to accept `FlowActionV2` — use `ConditionV2` for `TerminateWhen`, pass v2 sub-actions to inner engine, ensure scripts dict available for nested scriptRef resolution in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/LoopActionHandler.cs
- [x] T032 [US5] Update all callers of `FlowDefinitionLoader.GetDefinition` (`JsonDrivenSearchScraper`, `JsonDrivenEntityDetailScraper`, other handlers) to work with `FlowDefinitionV2` return type — update DI registrations for `TargetResolver` and `ConditionEvaluator` in src/BizScraper.Api/Infrastructure/Scraping/Engine/
- [x] T033 [US5] Fix all compilation errors from v1→v2 type changes across existing unit test files in tests/BizScraper.UnitTests/
- [x] T034 [US5] Verify all 5 existing v1 JSON definitions load and execute correctly through V1Normalizer → v2 engine path — run full test suite and confirm zero regressions

**Checkpoint**: All existing v1 definitions work identically through the v2 engine — zero regressions (SC-001)

---

## Phase 4: User Story 1 — Engine Executes V2 Definitions (Priority: P1) 🎯 MVP

**Goal**: Native v2 definitions with metadata, environment, viewport, retries, and default variable values load and execute correctly

**Independent Test**: Create a v2 Colorado business search definition and verify identical results to v1

- [x] T035 [US1] Add viewport configuration to `ScrapingFlowEngine.ExecuteAsync` — call `page.SetViewportSizeAsync(width, height)` when `definition.Environment?.Viewport` is set, before executing any actions in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T036 [US1] Add per-action retry logic to `ScrapingFlowEngine.ExecuteActionsAsync` — when `definition.Environment?.Retries` is configured, wrap handler execution in retry loop with `delayMs` initial delay multiplied by `backoffMultiplier`, skip retry for `OperationCanceledException` and `CheckTextMatchException`, log each retry attempt in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T037 [US1] Add variable initialization from `VariableV2.DefaultValue` — during engine startup, apply `JsonElement` default values for config/runtime variables not provided at runtime, honor `Required` constraint (default satisfies required) in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T038 [US1] Log metadata fields (id, name, author) in flow lifecycle log messages — add `[LoggerMessage]` methods for flow start/complete with metadata context in src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs
- [x] T039 [P] [US1] Write unit tests for viewport configuration — verify `SetViewportSizeAsync` called with correct dimensions when environment.viewport is set, not called when null in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs
- [x] T040 [P] [US1] Write unit tests for per-action retry logic — retry on transient failure, no retry when retries config is null, no retry on `CheckTextMatchException`, backoff delay multiplied correctly, max attempts honored in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs
- [x] T041 [P] [US1] Write unit tests for default value initialization — config variable uses default when not provided at runtime, required variable with default satisfies requirement, extracted variable default ignored in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs

**Checkpoint**: V2 definitions with environment and viewport execute correctly (US1 complete, FR-027–FR-029)

---

## Phase 5: User Story 2 — Multi-Selector Resilient Targeting (Priority: P1)

**Goal**: Actions and conditions use multi-selector targets with confidence-ranked fallback

**Independent Test**: V2 definition with multi-selector target — highest confidence selector matches, fallback works when primary fails

- [x] T042 [US2] Add multi-selector success logging to `ClickActionHandler` — log which selector strategy and value succeeded after `TargetResolver` resolution (TargetResolver integration already done in T021) in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ClickActionHandler.cs
- [x] T043 [P] [US2] Add multi-selector success logging to `FillActionHandler` — log which selector strategy succeeded (TargetResolver integration already done in T022) in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/FillActionHandler.cs
- [x] T044 [US2] Add multi-selector target support to `ConditionEvaluator` logging — log which selector resolved for `element-exists`/`element-visible` conditions with target (TargetResolver injection already done in T020) in src/BizScraper.Api/Infrastructure/Scraping/Engine/ConditionEvaluator.cs
- [x] T045 [P] [US2] Write unit tests for `ClickActionHandler` with multi-selector target — first selector matches, fallback to second on failure, all-fail throws `TargetResolutionException` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/ClickActionHandlerTests.cs
- [x] T046 [P] [US2] Write unit tests for `FillActionHandler` with multi-selector target in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/FillActionHandlerTests.cs
- [x] T047 [P] [US2] Write unit tests for `ConditionEvaluator` with multi-selector target — element-exists with target, element-visible with target, terminateWhen with target in loop in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ConditionEvaluatorTests.cs

**Checkpoint**: Multi-selector targeting works in actions and conditions (US2 complete, SC-003)

---

## Phase 6: User Story 3 — Named Reusable Scripts (Priority: P2)

**Goal**: Extract actions resolve scripts from the `scripts` section via `scriptRef`

**Independent Test**: V2 definition with scripts section — two named scripts referenced from multiple extract actions execute correctly

- [x] T048 [US3] Verify `scriptRef` resolution in `ExtractActionHandler` works end-to-end — ScriptResolver integration already done in T023; this task adds logging of which script name was resolved, verifies loop sub-action scriptRef resolution works identically to top-level, and confirms the scripts dict from `ActionContext` is correctly threaded through in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ExtractActionHandler.cs
- [x] T049 [P] [US3] Write unit tests for `ExtractActionHandler` with scriptRef — scriptRef resolved from scripts dict, inline javascript still works, scriptRef in loop sub-actions resolves correctly, both scriptRef and javascript throws at load time in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/ExtractActionHandlerTests.cs

**Checkpoint**: Script references work — definitions can use centralized scripts (US3 complete, SC-004)

---

## Phase 7: User Story 4 — Enriched Variable Definitions (Priority: P2)

**Goal**: Variables support defaultValue, sensitive markers, and descriptions

**Independent Test**: V2 definition with sensitive variable — value redacted in logs, default values applied

- [x] T050 [US4] Add sensitive variable redaction to `VariableSubstitution` — add `ResolveSafe` method that returns `***` for sensitive variable values (keyed by a `sensitiveNames` set), use in all logging contexts in src/BizScraper.Api/Infrastructure/Scraping/Engine/VariableSubstitution.cs
- [x] T051 [US4] Update engine logging to use `ResolveSafe` for all variable value log output — ensure sensitive variables never appear in plain text in diagnostic traces or log messages in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T052 [P] [US4] Write unit tests for sensitive variable redaction — sensitive=true variable value redacted as `***`, sensitive=false shown in full, null sensitive treated as false, ResolveSafe with no sensitive names returns original in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/VariableSubstitutionTests.cs

**Checkpoint**: Variables enriched with defaults/sensitivity/descriptions (US4 complete, SC-005)

---

## Phase 8: User Story 7 — Action Identifiers and Ordering (Priority: P3)

**Goal**: Actions have unique IDs in logs and are sorted by order field

**Independent Test**: V2 definition with out-of-order actions — engine sorts by order, error includes action ID

- [x] T053 [US7] Add action ordering sort in `ScrapingFlowEngine` — sort `FlowActionV2` list by `Order` when present (stable sort), preserve array order when `Order` is null, handle mixed ordered/unordered actions in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T054 [US7] Update error and failure messages in engine to include action ID — `InvalidOperationException`, handler failure catch blocks, `TargetResolutionException` context; distinct from T019 which adds action ID to lifecycle (start/complete) logs only in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T055 [P] [US7] Write unit tests for action ordering — sorted by order field, preserved array-order when no order field, mixed ordered/unordered stable sort in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs
- [x] T056 [P] [US7] Write unit tests for action ID in error messages — failed action includes ID in error, action without ID falls back to index in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs

**Checkpoint**: Action IDs in all logs/errors, ordering works (US7 complete, SC-006)

---

## Phase 9: User Story 6 — Output Schema Validation (Priority: P3)

**Goal**: Output validated against embedded JSON Schema — advisory warnings only

**Independent Test**: V2 definition with output.schema — conforming output passes silently, non-conforming output logs warning

- [x] T057 [US6] Implement `OutputSchemaValidator` internal sealed class — walk schema `JsonElement` tree against output `JsonElement`, check type assertions (string/number/boolean/array/object), required field presence, array item types, return list of `SchemaViolation` records, skip when schema is null in src/BizScraper.Api/Infrastructure/Scraping/Engine/OutputSchemaValidator.cs
- [x] T058 [US6] Add source-generated `[LoggerMessage]` methods for schema violation warnings — include violation path, expected type, actual type in src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs
- [x] T059 [US6] Integrate `OutputSchemaValidator` into `ScrapingFlowEngine.ExecuteAsync` — call after flow completes when `definition.Output.Schema` is not null, log each violation as warning, never block execution or modify result in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [x] T060 [P] [US6] Write unit tests for `OutputSchemaValidator` — valid output passes (empty violations), missing required field reported, wrong property type reported, array item type mismatch reported, null schema returns empty violations in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/OutputSchemaValidatorTests.cs
- [x] T061 [P] [US6] Write unit tests for engine schema validation integration — validation runs after execution completes, violation logged as warning not error, execution result not modified by validation failures in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs

**Checkpoint**: Output schema validation operational — advisory warnings logged (US6 complete, FR-040–FR-041)

---

## Phase 10: Compound Conditions & Error Messages (Cross-Cutting)

**Purpose**: `and`/`or` compound conditions in ConditionEvaluator, `errorMessage` on check-text actions

- [x] T062 Add `and` compound condition type to `ConditionEvaluator` — evaluate all conditions in `ConditionV2.Conditions` array, return true only when all inner conditions are true in src/BizScraper.Api/Infrastructure/Scraping/Engine/ConditionEvaluator.cs
- [x] T063 Add `or` compound condition type to `ConditionEvaluator` — evaluate conditions sequentially, short-circuit return true on first true condition in src/BizScraper.Api/Infrastructure/Scraping/Engine/ConditionEvaluator.cs
- [x] T064 [P] Write unit tests for compound conditions — and (all true passes, one false fails), or (first true short-circuits, all false fails), nested and/or/not combinations in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ConditionEvaluatorTests.cs
- [x] T065 Add `errorMessage` support to `CheckTextActionHandler` — resolve `${variable}` placeholders in `action.ErrorMessage` using `VariableSubstitution.Resolve`, include resolved message in `CheckTextMatchException` constructor when `onMatch` is `"throw"` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CheckTextActionHandler.cs
- [x] T066 Update `CheckTextMatchException` to carry `ErrorMessage` property alongside existing `ErrorType` — populated from resolved errorMessage or null when not set in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CheckTextActionHandler.cs
- [x] T067 [P] Write unit tests for errorMessage — variable interpolation in errorMessage, null errorMessage uses default behavior, resolved errorMessage carried in exception, sensitive variable redaction in errorMessage in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/CheckTextActionHandlerTests.cs

**Checkpoint**: Compound conditions and error messages operational (FR-033–FR-036)

---

## Phase 11: V1-to-V2 File Migration

**Purpose**: Mechanically migrate all 5 v1 JSON files to v2 on disk, verify functional equivalence, remove v1 runtime support

- [x] T068 Migrate `co-business-search.json` from v1 to v2 format using V1Normalizer mapping rules — add metadata section, convert flat selectors to targets, assign action IDs/order, add schemaVersion "1.0.0" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-business-search.json
- [x] T069 [P] Migrate `co-entity-details.json` from v1 to v2 format in src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-entity-details.json
- [x] T070 [P] Migrate `wy-business-search.json` from v1 to v2 format in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-business-search.json
- [x] T071 [P] Migrate `wy-entity-details.json` from v1 to v2 format in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json
- [x] T072 [P] Migrate `de-business-search.json` from v1 to v2 format in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json
- [x] T073 Run full test suite with migrated v2 files — verify all 5 definitions produce identical results to original v1 files, confirm zero regressions
- [x] T074 Remove v1 runtime support — delete `V1Normalizer.cs`, remove v1 `FlowDefinition`/`FlowAction`/`Condition`/`Variable` records from FlowDefinition.cs, remove `FlowDefinitionJsonContext` (v1), remove v1 detection branch in `FlowDefinitionLoader`, update `SupportedSchemaVersion` constant in src/BizScraper.Api/Infrastructure/Scraping/Engine/
- [x] T075 Remove v1 normalizer unit tests — delete `V1NormalizerTests.cs`, update `FlowDefinitionLoaderTests.cs` to only test v2 path in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/

**Checkpoint**: All definitions are v2 on disk, v1 runtime support removed (FR-037–FR-039, SC-001, SC-002)

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, integration verification, and documentation

- [x] T076 Verify `FlowDefinitionV2JsonContext` includes all required type registrations — compile and fix any missing types or serialization issues in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2JsonContext.cs
- [x] T077 [P] Ensure all source-generated `[LoggerMessage]` methods compile without warnings in src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs
- [x] T078 [P] Run full solution build — `dotnet build` passes with zero warnings in solution root
- [x] T079 Run full unit test suite — all tests pass across tests/BizScraper.UnitTests/
- [x] T080 [P] Update `appsettings.json` to add `SelectorTimeoutMs` default (5000) under scraping configuration section if environment-level default needs external override in src/BizScraper.Api/appsettings.json

**Checkpoint**: Feature complete — all tasks done, all tests passing, zero warnings

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1 (Setup: T001–T006)
  └── Phase 2 (Foundation: T007–T017)
        └── Phase 3 (US5 Backward Compat: T018–T034)
              ├── Phase 4 (US1 V2 Engine: T035–T041)
              ├── Phase 5 (US2 Multi-Selector: T042–T047)
              ├── Phase 6 (US3 Scripts: T048–T049)
              ├── Phase 7 (US4 Variables: T050–T052)
              ├── Phase 8 (US7 Action IDs: T053–T056)
              ├── Phase 9 (US6 Schema Validation: T057–T061)
              └── Phase 10 (Compound + Errors: T062–T067)
                    │
                    └── Phase 11 (Migration: T068–T075) [after Phases 3–10]
                          └── Phase 12 (Polish: T076–T080)
```

### User Story Dependencies

- **US5 (P1)**: First — provides the v2 execution path all other stories build on
- **US1 (P1)**: After US5 — adds v2-native features (viewport, retries, defaults)
- **US2 (P1)**: After US5 — multi-selector targeting integrated into handlers already updated in US5
- **US3 (P2)**: After US5 — ScriptResolver integrated into ExtractActionHandler updated in US5
- **US4 (P2)**: After US5 — sensitive redaction applies to engine logging updated in US5
- **US7 (P3)**: After US5 — action ordering/IDs in engine updated in US5
- **US6 (P3)**: After US5 — OutputSchemaValidator integrated into engine updated in US5
- **Phases 4–10**: Can proceed in parallel after Phase 3 (US5) completes

### Within-Phase Parallelism

- **Phase 2**: T007, T009, T011 (implementations) are independent; T008, T010, T012, T015 (tests) are independent
- **Phase 3**: T021–T030 (action handler updates) are all independent — different files
- **Phase 11**: T068–T072 (JSON file migrations) are all independent

## Parallel Execution Examples

### Phase 2 (Foundation) — Implementation batch
```
Parallel: T007 (V1Normalizer) | T009 (TargetResolver) | T011 (ScriptResolver)
Parallel: T008 (V1Normalizer tests) | T010 (TargetResolver tests) | T012 (ScriptResolver tests)
Sequential: T013 → T014 (FlowDefinitionLoader two-pass + validation)
Parallel: T015 (Loader tests) | T016 (ActionContext) | T017 (EngineLogging)
```

### Phase 3 (US5) — Handler updates batch
```
Sequential: T018 (IActionHandler interface) → T019 (Engine)
Sequential: T020 (ConditionEvaluator)
Parallel: T021–T030 (all 10 action handlers — different files)
Sequential: T031 (LoopActionHandler — depends on engine v2)
Sequential: T032 → T033 → T034 (callers, test fixes, verification)
```

### Phases 4–10 — After US5 completes
```
Parallel: Phase 4 (US1) | Phase 5 (US2) | Phase 6 (US3) | Phase 7 (US4) | Phase 8 (US7) | Phase 9 (US6) | Phase 10 (Compound)
```

---

## Implementation Strategy

### MVP Scope (Phases 1–5)

Delivers the core v2 engine with backward compatibility, viewport/retries, and multi-selector targeting. This is the minimum viable increment.

### Incremental Delivery

1. **Increment 1** (Phases 1–3): V2 model + backward-compatible engine — zero regression risk, single v2 execution path
2. **Increment 2** (Phases 4–5): V2-native features — viewport, retries, multi-selector targeting
3. **Increment 3** (Phases 6–7): Scripts + enriched variables — definition authoring improvements
4. **Increment 4** (Phases 8–10): Action IDs, output validation, compound conditions, error messages
5. **Increment 5** (Phase 11): V1 file migration + v1 runtime removal — final cleanup
6. **Increment 6** (Phase 12): Polish and verification
