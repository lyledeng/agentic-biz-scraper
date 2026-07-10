# Tasks: Per-Definition Browser Mode Configuration

**Input**: Design documents from `specs/030-per-definition-browser-mode/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No project initialization needed — all changes are within existing projects.

*(No setup tasks — existing project structure is used as-is.)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New model types and browser pool infrastructure that ALL user stories depend on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001a Write `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/BrowserConfigTests.cs` — unit tests for `BrowserConfig` parsing: valid mode values, null channel defaults, headless default true, invalid mode rejection, JSON round-trip via source-generated context (Red phase — tests fail until T001/T002)
- [X] T001 Add `BrowserConfig` record to `src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs` — new `sealed record BrowserConfig(string Mode, string? Channel, bool Headless = true)` and add `BrowserConfig? Browser` property to `FlowDefinitionV2` record
- [X] T002 Register `BrowserConfig` type in `src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2JsonContext.cs` — add `BrowserConfig` to the `JsonSerializable` attributes on the source-generated JSON context
- [X] T003a Write `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/BrowserPoolTests.cs` — unit tests for `BrowserPool` lifecycle: lazy creation, usage counting, recycle after threshold, concurrent lock behavior, DisposeAsync cleanup (Red phase — tests fail until T003)
- [X] T003 Create `BrowserPool` class in `src/BizScraper.Api/Infrastructure/Scraping/Engine/BrowserPool.cs` — internal sealed class encapsulating `IPlaywright?`, `IBrowser?`, `SemaphoreSlim` browser lock, and `int` usage count with methods: `EnsureBrowserAsync(PlaywrightOptions, CancellationToken)`, `ForceRecycleAsync(CancellationToken)`, `CanConnectAsync(CancellationToken)`, and `DisposeAsync()`
- [X] T004 Add `ResolveEffectiveMode` method to `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — static method that takes `BrowserConfig?` and `PlaywrightOptions`, returns `"local"` or `"remote"` per the three-step resolution algorithm in data-model.md
- [X] T005 Add `[LoggerMessage]` source-generated log methods for browser mode resolution and pool selection to `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — log effective mode, source (definition override vs. global default), and definition slug at Information level

**Checkpoint**: Foundation types ready — user story phases can begin.

---

## Phase 3: User Story 1 — Override Browser Mode per Definition (Priority: P1) 🎯 MVP

**Goal**: `GenericScriptScraper` uses dual browser pools and routes each definition to the correct pool based on its `browser` config section.

**Independent Test**: Execute a definition with `browser.mode = "local"` and verify local Chrome launches; execute one without `browser` and verify it uses the global default.

### Implementation for User Story 1

- [X] T006a Write `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/BrowserPoolSelectionTests.cs` — unit tests for dual-pool routing: definition with local override routes to local pool, definition with remote override routes to remote pool, definition without override uses global default, remote fallback to local when no BrowserEndpoint, concurrent local+remote requests use independent semaphores (Red phase — tests fail until T006–T011)
- [X] T006 [US1] Refactor `GenericScriptScraper` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — replace single `_browser`, `_playwright`, `_browserLock`, `_usageCount` fields with `_localPool: BrowserPool` and `_remotePool: BrowserPool`; replace the single `_executionSemaphore` with `_executionSemaphoreLocal: SemaphoreSlim(3, 3)` and `_executionSemaphoreRemote: SemaphoreSlim(3, 3)`
- [X] T007 [US1] Update `ExecuteAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — call `ResolveEffectiveMode(definition.Browser, options.Value)` to determine the effective mode; acquire the correct pool and semaphore; pass pool to `EnsureBrowserAsync`; log effective mode and source per FR-014
- [X] T008 [US1] Update `EnsureBrowserAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — change signature to accept `BrowserPool pool` and `BrowserConfig? browserConfig`; use `browserConfig?.Channel` and `browserConfig?.Headless ?? true` for local launches instead of global `PlaywrightOptions.BrowserChannel`; remote mode always uses global `BrowserEndpoint`/`AccessToken`
- [X] T009 [US1] Update `ForceRecycleBrowserAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — accept `BrowserPool pool` parameter; recycle only the specified pool
- [X] T010 [US1] Update `DisposeAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — dispose both `_localPool` and `_remotePool`; dispose both execution semaphores
- [X] T011 [US1] Add definition-level browser config validation in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — when `definition.Browser` is present, validate `Mode` is "local" or "remote"; throw `InvalidOperationException` with clear message if invalid (FR-012)
- [X] T011a [US1] Add error handling for missing browser channel in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — wrap Playwright channel-not-found exceptions during local browser launch with a user-friendly error message including the requested channel name and definition slug; surface error in `FlowExecutionResult` diagnostics (Edge Case 1)

**Checkpoint**: Dual-pool routing is functional. Any definition can override browser mode via JSON. Non-overridden definitions use global default.

---

## Phase 4: User Story 2 — Iowa Runs on Local Chrome (Priority: P1)

**Goal**: Iowa definitions are pre-configured with local Chrome mode, bypassing anti-bot detection.

**Independent Test**: Execute an Iowa business search and verify results are returned without anti-bot blocking.

### Implementation for User Story 2

- [X] T012 [P] [US2] Add `browser` section to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json` — add top-level `"browser": { "mode": "local", "channel": "chrome", "headless": true }` after the `metadata` section
- [X] T013 [P] [US2] Add `browser` section to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json` — add top-level `"browser": { "mode": "local", "channel": "chrome", "headless": true }` after the `metadata` section

**Checkpoint**: Iowa definitions route to local Chrome pool. Iowa searches bypass anti-bot detection.

---

## Phase 5: User Story 3 — Non-Iowa Definitions Default to Remote (Priority: P2)

**Goal**: Definitions without a `browser` section continue using the global default mode (remote when `BrowserEndpoint` is configured).

**Independent Test**: Execute a Colorado or Wyoming search and verify the system uses the remote workspace when `BrowserEndpoint` is configured.

### Implementation for User Story 3

- [X] T014 [US3] Write unit test in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/BrowserConfigTests.cs` asserting all 6 non-Iowa definition JSON files (`us-co-business-search.json`, `us-co-entity-details.json`, `us-wy-business-search.json`, `us-wy-entity-details.json`, `de-de-business-search.json`, `de-de-entity-details.json`) deserialize with `Browser = null` and `ResolveEffectiveMode` returns the global default mode

**Checkpoint**: Non-Iowa definitions work identically to pre-feature behavior. No regressions.

---

## Phase 6: User Story 4 — Operator Adds Browser Override to Any Definition (Priority: P2)

**Goal**: Operators can add or remove a `browser` section in any definition JSON to change its browser mode without code changes.

**Independent Test**: Add a `browser: { "mode": "local" }` block to a Colorado definition, execute it, and verify local mode is used.

### Implementation for User Story 4

- [X] T015 [US4] Add XML doc-comments to `BrowserConfig` record in `src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs` — document each property with `<summary>` explaining purpose and constraints (mode validation, channel/headless ignored for remote)
- [X] T016 [US4] Add XML doc-comments to `BrowserPool` class in `src/BizScraper.Api/Infrastructure/Scraping/Engine/BrowserPool.cs` — document class purpose, lifecycle, and each public method
- [X] T016a [US4] Write acceptance tests in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/BrowserPoolSelectionTests.cs` — verify US4 acceptance scenarios: adding a browser override block to a definition routes to overridden mode; removing the browser override block falls back to global default mode

**Checkpoint**: BrowserConfig and BrowserPool are fully documented and US4 acceptance criteria verified. Any definition JSON can be edited to add/remove browser overrides.

---

## Phase 7: User Story 5 — Health Check Reflects Per-Definition Browser Modes (Priority: P3)

**Goal**: The `/ready` endpoint returns per-mode health breakdown with `200 OK` if at least one mode is healthy.

**Independent Test**: Call `/ready` when both modes are configured and verify the response includes per-mode status for each pool.

### Implementation for User Story 5

- [X] T017a Write/update `tests/BizScraper.UnitTests/Features/HealthCheck/HealthCheckEndpointsTests.cs` — unit tests for per-mode health response: both modes healthy returns 200 Ready, one unhealthy returns 200 Degraded with per-mode breakdown, all unhealthy returns 503 NotReady, `definitionsRequiringLocal`/`definitionsRequiringRemote` arrays populated correctly (Red phase — tests fail until T017–T020)
- [X] T017 [US5] Update `CanConnectAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — change return type to a new `ReadinessHealthResponse` record with per-mode results; probe `_localPool.CanConnectAsync()` and `_remotePool.CanConnectAsync()` independently; include channel/endpoint info in each mode's status
- [X] T018 [US5] Add `ReadinessHealthResponse` and `ModeStatus` records to `src/BizScraper.Api/Features/HealthCheck/HealthModels.cs` — include `Status` (Ready/Degraded/NotReady), `BrowserModes` dictionary mapping mode name to `ModeStatus`, `DefinitionsRequiringLocal`, `DefinitionsRequiringRemote` per the health-check-response-schema.json contract
- [X] T019 [US5] Update `/ready` endpoint in `src/BizScraper.Api/Features/HealthCheck/HealthCheckEndpoints.cs` — call updated `CanConnectAsync`; return `200 OK` with per-mode JSON body if at least one mode healthy; return `503` only if all modes unhealthy; include `definitionsRequiringLocal` / `definitionsRequiringRemote` arrays by scanning loaded definitions
- [X] T020 [US5] Register health response models in `src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2JsonContext.cs` — add `ReadinessHealthResponse` and `ModeStatus` to source-generated JSON context; verify JSON round-tripping works correctly with source generation

**Checkpoint**: `/ready` reports per-mode health. Kubernetes pod stays in ready pool when at least one mode is functional.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Logging, startup observability, and documentation updates.

- [X] T021 Update startup logging in `src/BizScraper.Api/Program.cs` — replace single browser mode log with summary of per-definition browser modes; log count of definitions using local mode, remote mode, and global default; log the definition slugs for each category
- [X] T023 [P] Verify backward compatibility — run existing unit tests in `tests/BizScraper.UnitTests/` to confirm no regressions; all 6 non-Iowa definitions deserialize with `Browser = null` and route to global default

---

## Dependencies

```text
Phase 2 (Foundation): T001a → T001 → T002 (parallel with T003a → T003) → T004, T005 (T004 & T005 parallelizable)
Phase 3 (US1): T006a → T006 → T007 → T008 → T009 → T010 → T011 → T011a
Phase 4 (US2): T012, T013 (parallelizable, depend on Phase 3)
Phase 5 (US3): T014 (depends on Phase 3)
Phase 6 (US4): T015, T016 (parallelizable, depend on Phase 2) → T016a
Phase 7 (US5): T017a → T017 → T018 → T019, T020 (depend on Phase 3)
Phase 8 (Polish): T021, T023 (T021 depends on Phase 3; T023 depends on Phase 7)
```

```text
Story completion order:
  US1 (P1, core dual-pool) → US2 (P1, Iowa config) → US3 (P2, backward compat) → US4 (P2, docs) → US5 (P3, health check)
  
Parallel opportunities per story:
  US1: T006a then T006–T011a are sequential (same file)
  US2: T012 ∥ T013 (different JSON files)
  US4: T015 ∥ T016 (different source files), then T016a
  US5: T017a then T019 ∥ T020 (different files, after T018)
  Polish: T021 ∥ T023 (independent files)
```

## Implementation Strategy

1. **MVP first**: US1 + US2 (Phases 3–4) = functional per-definition browser mode with Iowa on local Chrome. This alone solves the anti-bot problem.
2. **Backward compatibility**: US3 (Phase 5) = verification that non-Iowa definitions continue working.
3. **Operator flexibility**: US4 (Phase 6) = documentation enabling operators to configure any definition.
4. **Observability**: US5 (Phase 7) = health check visibility for Kubernetes operators.
5. **Polish**: Phase 8 = startup logs, JSON context validation, regression check.
