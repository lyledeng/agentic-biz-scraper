# Tasks: Comprehensive Action Logging for Scraping Engine

**Input**: Design documents from `/specs/026-action-logging/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No project initialization needed — this feature modifies existing files only. This phase prepares the shared infrastructure changes that all user stories depend on.

- [x] T001 Add `GetLogDetails` and `GetCompletionDetails` methods to `IActionHandler` interface in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/IActionHandler.cs — `GetLogDetails(ActionContext, FlowActionV2)` returns `string?`; `GetCompletionDetails(ActionContext, FlowActionV2)` has default implementation returning `null`
- [x] T002 Add `SensitiveVariableNames` property (`IReadOnlySet<string>`), `LoopDepth` (`int`, default 0), and `LoopIteration` (`int`, default 0) to `ActionContext` in src/BizScraper.Api/Infrastructure/Scraping/Engine/ActionContext.cs — `LoopDepth` and `LoopIteration` enable nested loop sub-actions to indicate their loop context in log entries

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Modify the engine core and logging methods that ALL user story handler implementations depend on.

**⚠️ CRITICAL**: No handler work (Phase 3+) can begin until this phase is complete.

### Tests for Phase 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T003 [P] Write unit tests for enriched `EngineLogging` methods in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/EngineLoggingTests.cs — verify `ActionStarted`, `ActionCompleted`, `ActionSkipped` emit at `Information` level; verify `ActionFailed` emits at `Error` level; verify `RetryAttempt` emits at `Warning` level; verify all methods include `correlationId` parameter; verify `ActionCompleted` includes `elapsedMs`; tests MUST FAIL before T004–T005 implementation
- [x] T004 [P] Write unit tests for `ScrapingFlowEngine` sensitive variable set building in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs — verify `context.SensitiveVariableNames` is populated from `definition.Variables` where `Sensitive == true`; verify `null` and `false` Sensitive values are excluded; tests MUST FAIL before T006 implementation
- [x] T005 Write unit test for null action ID/description fallback in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs — verify that when `action.Id` is null, log receives `(no id)` fallback; verify that when `action.Description` is null, log receives `(no description)` fallback; test MUST FAIL before T007 implementation

### Implementation for Phase 2

- [x] T006 Enrich and promote existing `[LoggerMessage]` methods in src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs — promote `ActionStarted`, `ActionCompleted`, `ActionSkipped` from `Debug` to `Information`; add `correlationId`, `details` parameters to `ActionStarted` and `ActionFailed`; add `correlationId`, `elapsedMs`, `details` to `ActionCompleted`; add `correlationId`, `condition` to `ActionSkipped`; update message templates per data-model.md
- [x] T007 Add source-generated `RetryAttempt` method at `Warning` level to src/BizScraper.Api/Infrastructure/Scraping/Engine/EngineLogging.cs with parameters: `actionType`, `actionId`, `attempt`, `maxAttempts`, `delayMs`
- [x] T008 Update `ScrapingFlowEngine.ExecuteAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs — build `HashSet<string>` from `definition.Variables` where `Sensitive == true` and assign to `context.SensitiveVariableNames` before `ExecuteActionsAsync`
- [x] T009 Update `ScrapingFlowEngine.ExecuteActionsAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs — call `handler.GetLogDetails()` before execution and pass result to enriched `ActionStarted`; wrap `ExecuteWithRetryAsync` with `Stopwatch.StartNew()`; call `handler.GetCompletionDetails()` after execution and pass elapsed ms + details to enriched `ActionCompleted`; pass `context.CorrelationId` to all lifecycle log calls; pass condition string to enriched `ActionSkipped`; use `action.Id ?? "(no id)"` and `action.Description ?? "(no description)"` fallbacks for null fields
- [x] T010 Update `ScrapingFlowEngine.ExecuteWithRetryAsync` in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs — replace `logger.LogDebug(...)` interpolated string with source-generated `logger.RetryAttempt(action.Type, action.Id, attempt + 1, retries.MaxAttempts, delayMs)`

**Checkpoint**: Engine framework ready — enriched lifecycle logging operational with placeholder details (null) from all handlers.

---

## Phase 3: User Story 1 — Action-Level Observability for Debugging (Priority: P1) 🎯 MVP

**Goal**: Every action type emits structured log entries with action-specific details (FR-001, FR-002, FR-008).

**Independent Test**: Run any scraping definition and verify every action produces a log entry with type-specific details and elapsed time visible in console output.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T011 [P] [US1] Write unit tests for `GetLogDetails` on all 11 handlers in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/ — one test class per handler verifying the detail string format matches the data-model contract (e.g., `NavigateActionHandlerTests.cs` asserts `url=..., waitUntil=...`); tests MUST FAIL before T013–T023 implementation
- [x] T012 [P] [US1] Write unit tests for `GetCompletionDetails` on handlers that override the default in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/ — verify `ExtractActionHandler` returns `resultLength={n}`, `CheckTextActionHandler` returns `matched={true/false}`, `DownloadActionHandler` returns `destination={path}`, `WaitForConditionActionHandler` returns `outcome=met`; tests MUST FAIL before implementation

### Implementation for User Story 1

- [x] T013 [P] [US1] Implement `GetLogDetails` in `NavigateActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/NavigateActionHandler.cs — return `url={resolved_url}, waitUntil={state}` using `VariableSubstitution.Resolve` for the URL
- [x] T014 [P] [US1] Implement `GetLogDetails` in `FillActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/FillActionHandler.cs — return `target={description}, value={resolved_value}` using `VariableSubstitution.Resolve` for non-sensitive values (sensitive handling deferred to US3)
- [x] T015 [P] [US1] Implement `GetLogDetails` in `ClickActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ClickActionHandler.cs — return `target={description}, waitAfter={state}`
- [x] T016 [P] [US1] Implement `GetLogDetails` in `ExtractActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ExtractActionHandler.cs — return `script={ref}, storeAs={variable}, postProcessor={name|none}`; implement `GetCompletionDetails` returning `resultLength={n}` from stored variable character count
- [x] T017 [P] [US1] Implement `GetLogDetails` in `CheckTextActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CheckTextActionHandler.cs — return `pattern={pattern}, onMatch={behavior}`; implement `GetCompletionDetails` returning `matched={true/false}` from `context.Variables` check-text result
- [x] T018 [P] [US1] Implement `GetLogDetails` in `DownloadActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/DownloadActionHandler.cs — return `selector={description}, filenamePattern={pattern}`; implement `GetCompletionDetails` returning `destination={storagePath}` with the blob storage destination path after upload
- [x] T019 [P] [US1] Implement `GetLogDetails` in `LoopActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/LoopActionHandler.cs — return `maxIterations={n}, aggregateInto={var}`; add per-iteration `[LoggerMessage]` logging inside `ExecuteAsync` emitting iteration number and aggregation status; log early termination with reason when loop exits before max iterations; set `context.LoopDepth` and `context.LoopIteration` before executing sub-actions so nested log entries indicate loop context
- [x] T020 [P] [US1] Implement `GetLogDetails` in `ScreenshotActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/ScreenshotActionHandler.cs — return `milestone={name}, force={flag}`
- [x] T021 [P] [US1] Implement `GetLogDetails` in `WaitForLoadActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/WaitForLoadActionHandler.cs — return `state={loadState}`
- [x] T022 [P] [US1] Implement `GetLogDetails` in `WaitForConditionActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/WaitForConditionActionHandler.cs — return `condition={summary}, timeoutMs={ms}`; implement `GetCompletionDetails` returning `outcome=met`
- [x] T023 [P] [US1] Implement `GetLogDetails` in `CallServiceActionHandler` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CallServiceActionHandler.cs — return `service={name}, input={var}, output={var}`

**Checkpoint**: All 11 handlers produce detail strings. Running any scraping definition emits Information-level log entries with action-specific details and elapsed time. SC-001, SC-002, SC-005, SC-006 satisfied.

---

## Phase 4: User Story 2 — Consistent Log Correlation Across Actions (Priority: P2)

**Goal**: Every action log entry includes the correlation ID for request-level filtering (FR-003).

**Independent Test**: Execute two concurrent scraping requests and filter log output by correlation ID — entries for each request should be fully isolated.

### Tests for User Story 2

> **NOTE: Write this test FIRST, ensure it FAILS before verification**

- [x] T024 [US2] Write unit test verifying correlation ID isolation — create test in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/CorrelationIdPropagationTests.cs that executes two flows with different correlation IDs and asserts each log entry contains only its own correlation ID; verify nested loop actions inherit the parent correlation ID

**Checkpoint**: Correlation ID appears in every action log entry. Concurrent request logs can be isolated by filtering. SC-004 satisfied.

---

## Phase 5: User Story 3 — Sensitive Data Protection in Logs (Priority: P2)

**Goal**: Sensitive variable values are redacted in log output; non-sensitive values are logged in full (FR-004).

**Independent Test**: Run a scraping flow with a `fill` action using a `sensitive: true` variable and verify the log shows character count instead of the value.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T025 [P] [US3] Write unit test for sensitive value redaction in `FillActionHandler.GetLogDetails` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/FillActionHandlerTests.cs — verify `sensitive: true` variable produces `value=[N chars]`; verify `sensitive: false` variable produces full value; test MUST FAIL before T027
- [x] T026 [P] [US3] Write unit test for `CallServiceActionHandler.GetLogDetails` sensitive handling in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/Actions/CallServiceActionHandlerTests.cs — verify output variable names are logged but resolved values are not; test MUST FAIL before T028

### Implementation for User Story 3

- [x] T027 [US3] Update `FillActionHandler.GetLogDetails` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/FillActionHandler.cs — use `VariableSubstitution.ResolveSafe(action.Value, context.Variables, context.SensitiveVariableNames)` to resolve the value; when result is `***` (sensitive), substitute with `[N chars]` showing the actual character count of the resolved value; when non-sensitive, show the full resolved value
- [x] T028 [US3] Update `CallServiceActionHandler.GetLogDetails` in src/BizScraper.Api/Infrastructure/Scraping/Engine/Actions/CallServiceActionHandler.cs — ensure output variable values from sensitive services (e.g., captcha-solver) are not logged in the detail string; log only variable names, not resolved values

**Checkpoint**: Sensitive values never appear in plaintext in logs. Non-sensitive values (search terms) displayed in full. SC-003 satisfied.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup across all stories.

- [x] T029 [P] Verify project compiles with zero warnings by running `dotnet build src/BizScraper.Api/` — all new `[LoggerMessage]` methods must source-generate without errors
- [x] T030 Run quickstart.md validation — execute a scraping flow via the API and verify log output matches the examples in specs/026-action-logging/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all handler work
- **US1 (Phase 3)**: Depends on Phase 2 — tests (T011–T012) first, then all 11 handler tasks [P] in parallel
- **US2 (Phase 4)**: Depends on Phase 2 — test (T024) then verification
- **US3 (Phase 5)**: Depends on Phase 3 T014 (FillActionHandler initial implementation) — tests (T025–T026) first, then T027–T028
- **Polish (Phase 6)**: Depends on all previous phases

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependency on other stories
- **US2 (P2)**: Can start after Foundational — independent of US1 (correlation ID is an engine concern from T009)
- **US3 (P2)**: Depends on US1 T014 (FillActionHandler basic implementation) — adds sensitive redaction on top

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution Principle I)
- All US1 handler tasks are [P] — they modify different files and can run in parallel
- US2 is a single test/verification task
- US3 refines 2 specific handlers from US1

### Parallel Opportunities

```text
# Phase 2 tests can run in parallel:
T003, T004 (parallel — different test files)

# After Phase 2 implementation completes, launch US1 tests:
T011, T012 (parallel — different test files)

# Then launch all US1 handler tasks simultaneously:
T013, T014, T015, T016, T017, T018, T019, T020, T021, T022, T023

# US2 (T024) can run in parallel with any US1 task

# After T014 completes, US3 tests then implementation:
T025, T026 (parallel — different test files)
T027, T028 (parallel — different handler files)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Tests first (T003–T005), then implementation (T006–T010)
3. Complete Phase 3: Tests first (T011–T012), then handlers (T013–T023) — all [P] in parallel
4. **STOP and VALIDATE**: Run a scraping flow, verify all 11 action types emit enriched log entries
5. Deploy/demo if ready — MVP delivers full observability

### Incremental Delivery

1. Setup + Foundational → Engine framework ready
2. Add US1 → All actions logged with details and elapsed time → Deploy (MVP!)
3. Add US2 → Correlation ID verified for concurrent isolation → Deploy
4. Add US3 → Sensitive redaction active → Deploy
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (2 setup + 3 test + 5 impl tasks)
2. Once Foundational is done:
   - Developer A: US1 tests + handlers (navigate, fill, click, extract, check-text, download)
   - Developer B: US1 handlers (loop, screenshot, wait-for-load, wait-for-condition, call-service)
   - Developer C: US2 correlation test
3. After US1 fill handler done: Developer C picks up US3 (tests then implementation)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Total tasks: 30
- Tasks per phase: Setup=2, Foundational=8 (3 test + 5 impl), US1=13 (2 test + 11 impl), US2=1 (test), US3=4 (2 test + 2 impl), Polish=2
- Test tasks precede implementation per constitution Principle I (Test-First)
- Parallel opportunities: 11 tasks in US1 implementation, 2 tasks in US3
- Commit after each task or logical group
