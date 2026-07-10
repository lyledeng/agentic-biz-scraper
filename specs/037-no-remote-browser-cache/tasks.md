# Tasks: Disable Remote Browser Caching

**Input**: Design documents from `/specs/037-no-remote-browser-cache/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md

**Tests**: Included — constitution mandates test-first (TDD).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No new project structure needed. This is a behavioral change in existing files. Setup phase verifies the baseline is clean.

- [X] T001 Verify baseline: run `dotnet build` and `dotnet test` to confirm all existing tests pass before modifications

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Add logging infrastructure and helper method that all user stories depend on.

**⚠️ CRITICAL**: These must be complete before user story implementation begins.

- [X] T002 Add `[LoggerMessage]` source-generated partial methods for remote lifecycle events in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — add `LogRemoteBrowserConnecting(correlationId)`, `LogRemoteBrowserConnected(correlationId, elapsedMs)`, `LogRemoteBrowserDisposed(correlationId)`, `LogRemoteBrowserRetrying(correlationId, exception)`
- [X] T003 Extract `ConnectRemoteBrowserAsync` private helper method in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — encapsulates `Playwright.CreateAsync()` + `Chromium.ConnectAsync()` with 30-second timeout, access token header, and logging; returns `(IPlaywright, IBrowser)` tuple

**Checkpoint**: Foundation ready — logging and helper method available for user story implementation.

---

## Phase 3: User Story 1 — Remote Browser Session Per-Request Lifecycle (Priority: P1) 🎯 MVP

**Goal**: Each remote scrape request creates a fresh `IPlaywright` + `IBrowser`, executes the flow, and disposes both immediately after — eliminating idle Azure Playwright sessions.

**Independent Test**: Execute a remote-mode definition; verify browser is created and disposed within the request boundary.

### Tests for User Story 1

- [X] T004 [US1] Write unit test `RemoteMode_CreatesAndDisposesBrowserPerRequest` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that a remote-mode execution creates IPlaywright+IBrowser, executes the flow, and disposes both after completion
- [X] T005 [US1] Write unit test `RemoteMode_DisposesOnException` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that when the scraping flow throws, IPlaywright+IBrowser are still disposed (guaranteed cleanup per FR-004)
- [X] T006 [US1] Write unit test `RemoteMode_RetriesOnceOnDisconnect` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that a mid-scrape PlaywrightException triggers one reconnect+retry, and if the retry also fails, the error propagates and cleanup occurs

### Implementation for User Story 1

- [X] T007 [US1] Implement `ExecuteRemoteCoreAsync` private method in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — per-request lifecycle: call `ConnectRemoteBrowserAsync` (from T003), create BrowserContext+Page, execute flow, handle post-flow document downloads, dispose BrowserContext, close+dispose IBrowser, dispose IPlaywright in finally block
- [X] T008 [US1] Implement mid-scrape disconnect retry in `ExecuteRemoteCoreAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — catch `PlaywrightException` for "browser has been closed", dispose failed instances, call `ConnectRemoteBrowserAsync` again, retry flow once, propagate on second failure
- [X] T009 [US1] Update `ExecuteAsync` routing in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — when `effectiveMode == "remote"`, call `ExecuteRemoteCoreAsync` instead of `ExecuteCoreAsync`; local and windows modes continue to use `ExecuteCoreAsync` with pooled `_localPool`
- [X] T010 [US1] Set `ConnectAsync` timeout to `30000f` (30 seconds) in `ConnectRemoteBrowserAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — the existing `EnsureBrowserAsync` remote branch is no longer used for remote requests so its timeout is left as-is
- [X] T010a [US1] Write unit test `RemoteMode_ConcurrentRequestsGetIndependentInstances` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that two concurrent remote-mode executions each create their own IPlaywright+IBrowser instances (no shared state), both complete independently, and both dispose their resources

**Checkpoint**: Remote mode now uses per-request lifecycle. Run T004-T006, T010a tests — all should pass.

---

## Phase 4: User Story 2 — Local Browser Pool Unchanged (Priority: P1)

**Goal**: Confirm local browser pooling remains exactly as-is — reused across requests up to `ReuseBrowserForRequests` threshold.

**Independent Test**: Execute a local-mode definition multiple times; verify pool reuse behavior is identical to before.

### Tests for User Story 2

- [X] T011 [US2] Write unit test `LocalMode_ReusesBrowserAcrossRequests` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that local-mode executions reuse the same IBrowser from `_localPool` and do NOT create per-request instances
- [X] T012 [US2] Write unit test `LocalMode_RecyclesAtThreshold` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that after `ReuseBrowserForRequests` local executions, the browser is recycled and a new one launched

### Implementation for User Story 2

- [X] T013 [US2] Verify `ExecuteCoreAsync` local path is untouched in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — confirm no changes to `EnsureBrowserAsync` logic for local mode, pool reuse, or recycling behavior. This is a code-review verification task — no new code written.

**Checkpoint**: Local pool behavior verified unchanged. Run T011-T012 tests — all should pass.

---

## Phase 5: User Story 3 — Readiness Probe Does Not Create Remote Sessions (Priority: P2)

**Goal**: The `/ready` endpoint reports remote as "healthy" when the endpoint is configured, without ever creating an Azure Playwright session.

**Independent Test**: Call `/ready` in remote mode with no prior requests; verify no session created on Azure.

### Tests for User Story 3

- [X] T014 [US3] Write unit test `ProbeRemote_ReportsHealthyWithoutConnecting` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that `ProbePoolAsync` for remote mode returns "healthy" when endpoint is configured, without calling `Playwright.CreateAsync` or `ConnectAsync`
- [X] T015 [US3] Write unit test `ProbeRemote_ReportsNotConfiguredWhenNoEndpoint` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperRemoteLifecycleTests.cs — verify that when `IsRemoteMode` is false, remote probe returns "not-configured"

### Implementation for User Story 3

- [X] T016 [US3] Verify `ProbePoolAsync` remote branch in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — confirm the earlier fix (from conversation session) is correct: reports "healthy" when `!pool.HasBrowser` and endpoint is configured; reports connection state when browser was previously used. No `EnsureBrowserAsync` call for remote.

**Checkpoint**: Readiness probe verified. Run T014-T015 tests — all should pass.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, cleanup, and final validation.

- [X] T017 [P] Update XML doc-comments on `ExecuteAsync`, `ExecuteRemoteCoreAsync`, and `ConnectRemoteBrowserAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
- [X] T018 [P] Run full test suite: `dotnet test` across all test projects to confirm no regressions
- [X] T019 [P] Run quickstart.md validation steps from specs/037-no-remote-browser-cache/quickstart.md
- [X] T020 Update specs/current-specs/ with consolidated specification changes per constitution requirement

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (clean baseline)
- **User Story 1 (Phase 3)**: Depends on Phase 2 (logging + helper method); this is the MVP
- **User Story 2 (Phase 4)**: Depends on Phase 3 (routing change must exist to verify local is untouched)
- **User Story 3 (Phase 5)**: Can run in parallel with Phase 4 after Phase 3
- **Polish (Phase 6)**: Depends on all user stories complete

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Implementation MUST make tests pass (Red → Green → Refactor)
- Story complete before moving to next priority

### Parallel Opportunities

- T004, T005, T006 (US1 tests) can be written in parallel
- T011, T012 (US2 tests) can be written in parallel
- T014, T015 (US3 tests) can be written in parallel
- Phase 4 and Phase 5 can run in parallel after Phase 3
- T017, T018, T019 (Polish tasks) can run in parallel

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Verify baseline
2. Complete Phase 2: Add logging + helper method
3. Complete Phase 3: Per-request remote lifecycle
4. **STOP and VALIDATE**: Test with a real remote-mode request against Azure Playwright
5. Deploy/demo if ready — this alone eliminates the billing problem

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready
2. Phase 3 → Remote per-request lifecycle (MVP — solves the billing problem)
3. Phase 4 → Verify local pool unchanged (confidence)
4. Phase 5 → Verify readiness probe (defense in depth)
5. Phase 6 → Polish and documentation
