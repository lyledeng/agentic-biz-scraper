# Tasks: Scraping Diagnostics — Traces & Screenshots

**Input**: Design documents from `/specs/002-scraping-diagnostics/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Tests**: Required — constitution mandates test-first (TDD) discipline.

**Organization**: Tasks grouped by user story for independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Configuration model and appsettings schema shared by all user stories

- [X] T001 Create `TracingMode` enum (`Off`, `OnFailure`, `Always`) and `DiagnosticsOptions` class with `TracingMode` (default `Always`), `ScreenshotsEnabled` (default `true`), and `OutputPath` (default `"diagnostics"`) in `src/CososWebScraping.Api/Common/Configuration/DiagnosticsOptions.cs`
- [X] T002 Add `Diagnostics` property of type `DiagnosticsOptions` to `PlaywrightOptions` in `src/CososWebScraping.Api/Common/Configuration/PlaywrightOptions.cs`
- [X] T003 Add `Playwright:Diagnostics` configuration section with defaults to `src/CososWebScraping.Api/appsettings.json`
- [X] T003a Register `DiagnosticsOptions` with `ValidateDataAnnotations()` and `ValidateOnStart()` in `Program.cs` so invalid configuration values fail fast at startup

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Logging infrastructure and output directory management required by ALL user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004a Update `IBusinessEntityScraper.SearchAsync` signature to accept correlation ID parameter — propagate from handler via `HttpContext.TraceIdentifier` in `src/CososWebScraping.Api/Common/Interfaces/IBusinessEntityScraper.cs` and `src/CososWebScraping.Api/Features/BusinessSearch/Handlers/SearchBusinessEntitiesHandler.cs`
- [X] T005 [P] Add `[LoggerMessage]` diagnostics log methods to `BusinessSearchLogMessages` in `src/CososWebScraping.Api/Features/BusinessSearch/Logging/BusinessSearchLogMessages.cs`: `TraceSaved`, `TraceDiscarded`, `ScreenshotSaved`, `DiagnosticArtifactFailed`
- [X] T006 [P] Write unit tests for `DiagnosticsOptions` defaults, `TracingMode` enum parsing, and output directory creation (verify directory is created when it does not exist) in `tests/CososWebScraping.UnitTests/Common/Configuration/DiagnosticsOptionsTests.cs`
- [X] T007 Add output directory creation logic (ensure directory exists, resolve relative paths against content root) to `PlaywrightBusinessEntityScraper` in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs` — extracted as a private helper method

**Checkpoint**: Configuration model complete, logging ready, output directory guaranteed — user story implementation can begin

---

## Phase 3: User Story 1 — Failure Tracing (Priority: P1) 🎯 MVP

**Goal**: Automatically capture a Playwright trace archive when a scraping request fails, enabling operators to replay the session in a trace viewer.

**Independent Test**: Trigger a scraping failure → confirm a `.zip` trace file is written with the correlation ID in the filename → open in Playwright Trace Viewer.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T008 [P] [US1] Unit test: when tracing mode is `Always` or `OnFailure` and scraping fails, `Tracing.StartAsync` is called before `page.GotoAsync` and `Tracing.StopAsync` is called with a file path in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsTracingTests.cs`
- [X] T009 [P] [US1] Unit test: when tracing mode is `Off`, `Tracing.StartAsync` is never called in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsTracingTests.cs`
- [X] T010 [P] [US1] Unit test: trace filename follows convention `{correlationId}_{timestamp}_trace.zip` in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsTracingTests.cs`

### Implementation for User Story 1

- [X] T011 [US1] Add tracing lifecycle to `SearchCoreAsync` in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`: call `context.Tracing.StartAsync` (with `Screenshots = true`, `Snapshots = true`) before first navigation when tracing mode is `Always` or `OnFailure`
- [X] T012 [US1] Add `finally` block to `SearchCoreAsync` that calls `context.Tracing.StopAsync` with the trace file path when mode is `Always` or (`OnFailure` and failed), or without a path to discard on success in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`
- [X] T013 [US1] Wrap all tracing `StopAsync` calls in try/catch per FR-008 — log warning via `DiagnosticArtifactFailed` and continue in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`
- [X] T014 [US1] Use correlation ID (now available from T004a) in `SearchCoreAsync` to generate filenames with the `{correlationId}_{timestamp}_trace.zip` convention in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`

**Checkpoint**: Failed scraping requests produce a trace `.zip` in the output directory. US1 is fully functional and independently testable.

---

## Phase 4: User Story 2 — Success Tracing (Priority: P2)

**Goal**: When tracing mode is "always", save traces for successful searches too — enabling proactive monitoring.

**Independent Test**: Set tracing mode to `Always` → perform a successful search → confirm trace `.zip` is written. Then set to `OnFailure` → successful search → confirm no trace file.

### Tests for User Story 2 ⚠️

- [X] T015 [P] [US2] Unit test: when tracing mode is `Always` and scraping succeeds, `Tracing.StopAsync` is called with a file path in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsTracingTests.cs`
- [X] T016 [P] [US2] Unit test: when tracing mode is `OnFailure` and scraping succeeds, `Tracing.StopAsync` is called without a path (discard) in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsTracingTests.cs`

### Implementation for User Story 2

- [X] T017 [US2] Update the `finally` block in `SearchCoreAsync` to also save the trace on success when mode is `Always`, and log `TraceSaved` or `TraceDiscarded` accordingly in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`

**Checkpoint**: Tracing now works for both success and failure cases based on the configured mode. US1+US2 both pass.

---

## Phase 5: User Story 3 — Milestone Screenshots (Priority: P2)

**Goal**: Capture full-page PNG screenshots at key scraping milestones (page load, search results, paginated pages, failure) for quick visual triage.

**Independent Test**: Enable screenshots → perform a multi-page search → confirm PNGs for `01-page-load`, `02-search-results`, `03-page-2` milestones exist in the output directory.

### Tests for User Story 3 ⚠️

- [X] T018 [P] [US3] Unit test: when screenshots enabled, `page.ScreenshotAsync` is called after page load, search submit, and each pagination in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsScreenshotTests.cs`
- [X] T019 [P] [US3] Unit test: when screenshots disabled but scraping fails, `page.ScreenshotAsync` is still called for the error screenshot in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsScreenshotTests.cs`
- [X] T020 [P] [US3] Unit test: when screenshots disabled and scraping succeeds, `page.ScreenshotAsync` is never called in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsScreenshotTests.cs`
- [X] T021 [P] [US3] Unit test: screenshot filenames follow convention `{correlationId}_{timestamp}_{milestone}.png` in `tests/CososWebScraping.UnitTests/Infrastructure/Scraping/ScrapingDiagnosticsScreenshotTests.cs`

### Implementation for User Story 3

- [X] T022 [US3] Add screenshot capture helper method to `PlaywrightBusinessEntityScraper` that calls `page.ScreenshotAsync(new PageScreenshotOptions { Path = ..., FullPage = true })`, wraps in try/catch per FR-008, and logs via `ScreenshotSaved` or `DiagnosticArtifactFailed` in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`
- [X] T023 [US3] Insert screenshot calls at milestones in `SearchCoreAsync`: after initial page load (`01-page-load`), after search submit (`02-search-results`), and after each pagination load (`03-page-{N}`) in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`
- [X] T024 [US3] Add error screenshot capture in all catch blocks of `SearchCoreAsync` — always capture `error.png` regardless of `ScreenshotsEnabled` setting in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`

**Checkpoint**: Milestone screenshots captured at every step. Error screenshots always captured on failure. US1+US2+US3 all pass.

---

## Phase 6: User Story 4 — Configuration (Priority: P3)

**Goal**: Operators can control diagnostics behavior via `appsettings.json` without code changes.

**Independent Test**: Change `Playwright:Diagnostics:TracingMode` to `Off`, restart, perform search → no trace or milestone screenshots written. Change to `OnFailure` → only failure produces artifacts.

### Tests for User Story 4 ⚠️

- [X] T025 [P] [US4] Unit test: `DiagnosticsOptions` binds correctly from configuration with custom values (non-default `OutputPath`, `TracingMode = Off`, `ScreenshotsEnabled = false`) in `tests/CososWebScraping.UnitTests/Common/Configuration/DiagnosticsOptionsTests.cs`
- [X] T026 [P] [US4] Unit test: relative `OutputPath` is resolved against content root in `tests/CososWebScraping.UnitTests/Common/Configuration/DiagnosticsOptionsTests.cs`

### Implementation for User Story 4

- [X] T027 [US4] Inject `IWebHostEnvironment` into `PlaywrightBusinessEntityScraper` and resolve relative `OutputPath` against `ContentRootPath` in `src/CososWebScraping.Api/Infrastructure/Scraping/PlaywrightBusinessEntityScraper.cs`
- [X] T028 [US4] Add `appsettings.Development.json` overrides for diagnostics if desired (e.g., different output path for dev) in `src/CososWebScraping.Api/appsettings.Development.json`

**Checkpoint**: All configuration permutations work. US1+US2+US3+US4 all pass.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final integration validation and cleanup

- [X] T029 Verify build succeeds with zero warnings via `dotnet build CososWebScraping.slnx`
- [X] T030 Run all unit tests via `dotnet test` and confirm green
- [X] T031 Manual validation: run the app locally, perform a search, verify trace `.zip` and milestone `.png` files appear in `diagnostics/` directory

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user stories. Includes T004a (correlation ID plumbing) which is a prerequisite for filename generation in all stories.
- **US1 (Phase 3)**: Depends on Phase 2 — core tracing lifecycle (MVP)
- **US2 (Phase 4)**: Depends on Phase 3 — extends tracing logic for success case
- **US3 (Phase 5)**: Depends on Phase 2 — independent of US1/US2 (different artifact type)
- **US4 (Phase 6)**: Depends on Phase 2 — independent of US1/US2/US3 (config plumbing)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Requires Phase 2 only — no dependencies on other stories
- **US2 (P2)**: Requires US1 — extends the same tracing `finally` block
- **US3 (P2)**: Requires Phase 2 only — screenshot logic is independent of tracing
- **US4 (P3)**: Requires Phase 2 only — configuration plumbing is independent

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Implementation follows test specifications
- Story complete and checkpoint verified before moving to next priority

### Parallel Opportunities

- **Phase 1**: T001 and T002 are sequential because they define the same configuration model; T003 and T003a depend on the options types being in place
- **Phase 2**: T005, T006, T007 can all run in parallel (different files)
- **Phase 3+**: US3 and US4 can run in parallel with US1 (different concerns, different files)
- **Within each story**: All tests marked [P] can run in parallel

---

## Parallel Example: User Story 3

```text
# Launch all tests for US3 together:
T018: Unit test — screenshots called at milestones
T019: Unit test — error screenshot always captured
T020: Unit test — no screenshots when disabled + success
T021: Unit test — screenshot filename convention

# Then implement sequentially:
T022: Screenshot capture helper method
T023: Insert milestone screenshot calls
T024: Add error screenshot in catch blocks
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T003a)
2. Complete Phase 2: Foundational (T004a–T007)
3. Complete Phase 3: User Story 1 (T008–T014)
4. **STOP and VALIDATE**: Trigger a failure → confirm trace `.zip` written
5. This is a deployable/demonstrable MVP

### Incremental Delivery

1. Setup + Foundational → Configuration model ready
2. Add US1 (failure tracing) → Test independently → MVP!
3. Add US2 (success tracing) → Test independently → Full tracing
4. Add US3 (milestone screenshots) → Test independently → Full visuals
5. Add US4 (configuration) → Test independently → Production-ready
6. Polish → Clean build, all tests green, manual validation
