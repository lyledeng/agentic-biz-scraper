# Tasks: Playwright Local Fallback

**Input**: Design documents from `/specs/021-playwright-local-fallback/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Constitution §I (Test-First) mandates tests for all new public methods. Test tasks included for new API surface.

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)

---

## Phase 1: Setup

**Purpose**: Configuration defaults and shared infrastructure extraction before any user story work.

- [X] T001 Change `Playwright:BrowserEndpoint` from hardcoded `wss://...` to `""` in src/BizScraper.Api/appsettings.json per FR-001
- [X] T002 [P] Change `ExecuteScript:ConcurrencyLimits:Default` from `1` to `3` in src/BizScraper.Api/appsettings.json per FR-010
- [X] T003 [P] Add computed property `IsRemoteMode => !string.IsNullOrWhiteSpace(BrowserEndpoint)` to src/BizScraper.Api/Common/Configuration/PlaywrightOptions.cs per data-model.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extract diagnostic helpers from `PlaywrightBrowserManager` before it can be deleted. All user stories depend on this phase.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T004 Create `DiagnosticsPathHelper` static class in src/BizScraper.Api/Infrastructure/Scraping/DiagnosticsPathHelper.cs — extract `BuildDiagnosticArtifactPath`, `BuildDiagnosticArtifactBlobPath`, `ResolveDiagnosticsOutputPath`, `SanitizeFileNameSegment` from `PlaywrightBrowserManager` per research.md §R-002
- [X] T005 Update tests/BizScraper.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsTracingTests.cs — delete orchestration tests (`ExecuteWithDiagnosticsAsync_*` tests that instantiate `PlaywrightBrowserManager`); migrate `BuildDiagnosticArtifactPath` call to `DiagnosticsPathHelper.BuildDiagnosticArtifactPath`; remove `CreateScraper` helper factory
- [X] T006 [P] Update tests/BizScraper.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsScreenshotTests.cs — delete orchestration tests (`ExecuteWithDiagnosticsAsync_*` and `CaptureScreenshotAsync` tests that instantiate `PlaywrightBrowserManager`); migrate `BuildDiagnosticArtifactPath` call to `DiagnosticsPathHelper.BuildDiagnosticArtifactPath`; remove `CreateManager` helper factory
- [X] T007 [P] Update tests/BizScraper.UnitTests/Common/Configuration/DiagnosticsOptionsTests.cs to call `DiagnosticsPathHelper.ResolveDiagnosticsOutputPath` instead of `PlaywrightBrowserManager.ResolveDiagnosticsOutputPath` and remove `PlaywrightBrowserManager` test helper factory
- [X] T008 Delete src/BizScraper.Api/Infrastructure/Scraping/PlaywrightEntityDetailScraper.cs — orphaned v1 scraper superseded by `co-entity-details.json` flow (spec 006)
- [X] T009 Delete src/BizScraper.Api/Infrastructure/Scraping/PlaywrightBrowserManager.cs — superseded by `GenericScriptScraper` per FR-007
- [X] T010 Remove `builder.Services.AddSingleton<PlaywrightBrowserManager>()` registration from src/BizScraper.Api/Program.cs (line 86); also remove any remaining `PlaywrightEntityDetailScraper` or `IEntityDetailScraper` DI registrations that reference deleted types
- [X] T011 Add startup validation for `PlaywrightOptions` in src/BizScraper.Api/Program.cs — when `BrowserEndpoint` is non-empty, validate it starts with `wss://` or `ws://`; fail-fast on invalid values per research.md §R-005
- [X] T012 Verify solution compiles with zero errors after `PlaywrightBrowserManager` and `PlaywrightEntityDetailScraper` removal

**Checkpoint**: Legacy browser manager removed. `DiagnosticsPathHelper` preserves test-covered static logic. Solution compiles clean.

---

## Phase 3: User Story 1 — Configure Browser Mode via Configuration (Priority: P1) 🎯 MVP

**Goal**: Operators can switch between Remote and Local browser modes with a single configuration change.

**Independent Test**: Set `Playwright:BrowserEndpoint` to empty → system launches local Chromium. Set to `wss://...` → system connects to remote workspace.

### Implementation for User Story 1

- [X] T013a [P] [US1] Write unit test for `PlaywrightOptions.IsRemoteMode` in tests/BizScraper.UnitTests/Common/Configuration/PlaywrightOptionsTests.cs — assert `IsRemoteMode` returns false when `BrowserEndpoint` is empty/null, true when populated with `wss://...`
- [X] T013b [P] [US1] Write unit test for startup validation in tests/BizScraper.UnitTests/Common/Configuration/PlaywrightOptionsTests.cs — assert validation fails when `BrowserEndpoint` is non-empty but does not start with `wss://` or `ws://`
- [X] T013c [P] [US1] Write unit tests for `GenericScriptScraper.CanConnectAsync` in tests/BizScraper.UnitTests/Infrastructure/Scraping/GenericScriptScraperTests.cs — test returns true when browser connected, false when `PlaywrightException` thrown (use NSubstitute browser factory override)
- [X] T013 [US1] Add `CanConnectAsync(CancellationToken)` method to src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — call `EnsureBrowserAsync`, return `_browser!.IsConnected`, catch `PlaywrightException`/`InvalidOperationException` and return false per research.md §R-003
- [X] T014 [P] [US1] Add `IsRemoteMode` property to src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — delegates to `options.Value.IsRemoteMode`
- [X] T015 [US1] Add headless option to `LaunchAsync` in `EnsureBrowserAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs — pass `new BrowserTypeLaunchOptions { Headless = true }` for local mode per FR-003

**Checkpoint**: Application starts in Local mode by default. Script execution works with local Chromium. Remote mode works when `BrowserEndpoint` is configured.

---

## Phase 4: User Story 2 — Health Check Reflects Active Browser Mode (Priority: P2)

**Goal**: `/ready` endpoint validates connectivity for the active browser mode and returns the mode in the response.

**Independent Test**: Start in Local mode → `GET /ready` returns `{"status":"Ready","browserMode":"Local"}`. Start in Remote mode with invalid endpoint → returns 503.

### Implementation for User Story 2

- [X] T016 [US2] Refactor `/ready` endpoint in src/BizScraper.Api/Features/HealthCheck/HealthCheckEndpoints.cs — inject `GenericScriptScraper` and `IOptions<PlaywrightOptions>` instead of `PlaywrightBrowserManager`; call `scraper.CanConnectAsync(ct)` per research.md §R-003 and health-check-contract.md
- [X] T017 [US2] Add `browserMode` field to `/ready` 200 OK response — include `opts.Value.IsRemoteMode ? "Remote" : "Local"` per FR-009 and health-check-contract.md
- [X] T018 [US2] Update `/ready` 503 detail message to include active browser mode — `$"Browser connectivity check failed (mode: {mode})."` per health-check-contract.md

**Checkpoint**: Readiness probe correctly validates local or remote browser. Kubernetes probes work in both modes.

---

## Phase 5: User Story 3 — Consistent Browser Lifecycle Across Both Modes (Priority: P2)

**Goal**: Browser pooling, recycling, and diagnostics work identically in both modes with no separate code paths.

**Independent Test**: Run the same script in Local and Remote modes; verify identical screenshot capture, browser recycling at 25 uses, and crash recovery.

### Implementation for User Story 3

- [X] T019 [US3] Verify `IsConnected` check for local browsers — start app in Local mode, execute `co-business-search` definition, confirm screenshots appear in `diagnostics/` folder. **Pass criteria**: script completes with 200 OK and screenshot artifacts are written identically to Remote mode.
- [X] T020 [US3] Verify browser recycling — set `ReuseBrowserForRequests=2` in appsettings, send 3 sequential requests in Local mode, check logs for "Recycling browser after 2 uses". **Pass criteria**: log message appears after 2nd request; 3rd request succeeds with a fresh browser.
- [X] T020a [US3] Verify concurrency=3 — send 3 simultaneous requests via `curl` in Local mode. **Pass criteria**: all 3 return 200 OK without `503 Service Unavailable`; FR-010 validated.

**Checkpoint**: Both modes share identical lifecycle. No separate code paths for local vs. remote.

---

## Phase 6: User Story 4 — Observability of Active Browser Mode (Priority: P3)

**Goal**: Active browser mode is logged at startup and visible in health check responses.

**Independent Test**: Check startup logs for mode indication; verify `/ready` response includes `browserMode`.

### Implementation for User Story 4

- [X] T021 [US4] Add startup log message in src/BizScraper.Api/Program.cs after service build — log `"Browser mode: Local (no BrowserEndpoint configured)"` or `"Browser mode: Remote (endpoint: {endpoint})"` at Information level per FR-008 and startup log contract
- [X] T022 [P] [US4] Add source-generated log method for browser mode startup message in appropriate logging class or inline in src/BizScraper.Api/Program.cs

**Checkpoint**: Operators can identify active mode from pod startup logs and `/ready` response without SSH access.

---

## Phase 7: Deployment Configuration

**Goal**: Helm charts support browser mode configuration via values override.

### Implementation

- [X] T023 [P] Add `playwright.browserEndpoint: ""` to deploy/helm/bizscraper/values.yaml per research.md §R-006
- [X] T024 Add `Playwright__BrowserEndpoint` environment variable to deploy/helm/bizscraper/templates/deployment.yaml sourced from `{{ .Values.playwright.browserEndpoint }}` per research.md §R-006

**Checkpoint**: Helm deployments default to Local mode. Staging/production override to Remote via values.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, validation, and documentation.

- [X] T025 Run quickstart.md validation — start application in Local mode, verify `/ready` returns `{"status":"Ready","browserMode":"Local"}`; execute at least one script definition (e.g., `co-business-search`) in Local mode to confirm end-to-end execution per SC-002; start in Remote mode, verify mode switches
- [X] T026 [P] Verify all existing unit tests pass after refactoring (`dotnet test`)
- [X] T027 [P] Verify Swagger/OpenAPI documentation reflects updated `/ready` response schema at /swagger endpoint
- [X] T028 Remove any remaining `using` references to `PlaywrightBrowserManager` or `PlaywrightEntityDetailScraper` across the solution

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on T003 from Setup (IsRemoteMode property) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 completion (PlaywrightBrowserManager removed, solution compiles)
- **US2 (Phase 4)**: Depends on T013 from US1 (CanConnectAsync exists on GenericScriptScraper)
- **US3 (Phase 5)**: Depends on Phase 2 completion — can run in parallel with US1/US2 (verification only)
- **US4 (Phase 6)**: Depends on T014 from US1 (IsRemoteMode property on GenericScriptScraper)
- **Deployment (Phase 7)**: No code dependencies — can run in parallel with any phase
- **Polish (Phase 8)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependencies on other stories
- **US2 (P2)**: Depends on US1 (T013 — CanConnectAsync method)
- **US3 (P2)**: Can start after Foundational — independent verification tasks
- **US4 (P3)**: Depends on US1 (T014 — IsRemoteMode property)

### Within Each User Story

- Core implementation before integration
- Story complete and independently testable before moving to next priority

### Parallel Opportunities

- **Phase 1**: T001, T002, T003 all touch different config/files — T002 and T003 are parallelizable
- **Phase 2**: T005, T006, T007 all migrate different test files — parallelizable after T004
- **Phase 3+**: US3 (verification) and US4 (logging) can run in parallel with US2 (health check)
- **Phase 7**: Entirely independent of application code — parallelizable with any phase

---

## Parallel Example: Foundational Phase

```
# After T004 (DiagnosticsPathHelper extraction), launch in parallel:
T005: Update ScrapingDiagnosticsTracingTests.cs
T006: Update ScrapingDiagnosticsScreenshotTests.cs
T007: Update DiagnosticsOptionsTests.cs
T008: Delete PlaywrightEntityDetailScraper.cs

# After all test migrations + T009 (delete PlaywrightBrowserManager):
T010: Remove DI registration from Program.cs
T011: Add startup validation
T012: Verify clean compile
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (config defaults)
2. Complete Phase 2: Foundational (extract helpers, remove legacy code)
3. Complete Phase 3: User Story 1 (browser mode via config)
4. **STOP and VALIDATE**: Launch app in Local mode, execute a script, verify it works
5. Deploy/demo if ready — operators can now use local Chromium

### Incremental Delivery

1. Setup + Foundational → Legacy code removed, defaults changed
2. Add US1 → Browser mode configurable → Deploy (MVP!)
3. Add US2 → Health check aware of mode → Deploy
4. Add US3 → Verify lifecycle parity → Deploy
5. Add US4 → Startup logging → Deploy
6. Deployment config → Helm values updated → Deploy
7. Polish → Full validation → Final deploy
