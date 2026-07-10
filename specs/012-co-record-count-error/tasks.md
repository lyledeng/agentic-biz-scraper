# Tasks: Semantic Error Handling for Colorado Exceeded Record Count

**Input**: Design documents from `specs/012-co-record-count-error/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No setup needed — no new projects, packages, or infrastructure. All required types and infrastructure already exist.

*Phase skipped — existing codebase has all dependencies in place.*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No foundational work needed. The `CheckTextMatchException`, `ExceededRecordCountException`, `GlobalExceptionHandler` mapping, `MapStatus`, and audit trail integration all exist. The only broken link is the exception wrapping in `ScrapingFlowEngine`.

*Phase skipped — all foundational components are already correctly implemented.*

---

## Phase 3: User Story 1 - Semantic Error Response (Priority: P1) 🎯 MVP

**Goal**: When CO SOS reports "Exceeded Record Count", return HTTP 422 with RFC 9457 Problem Details instead of generic 500. No retries.

**Independent Test**: Send `GET /api/v1/business-search?searchTerm=a&state=CO` → verify HTTP 422 with `exceeded-record-count` type, returned within seconds (no retry delay).

### Tests for User Story 1

> **Write these tests FIRST, ensure they FAIL before implementation**

- [X] T001 [P] [US1] Write unit test: `CheckTextMatchException` propagates through `ScrapingFlowEngine.ExecuteAsync` without being wrapped in `InvalidOperationException` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs
- [X] T002 [P] [US1] Write unit test: other exceptions (non-`CheckTextMatchException`) are still wrapped in `InvalidOperationException` by `ScrapingFlowEngine.ExecuteAsync` in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs
- [X] T003 [P] [US1] Write unit test: `GlobalExceptionHandler` maps `ExceededRecordCountException` to HTTP 422 with type `exceeded-record-count` in tests/BizScraper.UnitTests/Middleware/GlobalExceptionHandlerTests.cs

### Implementation for User Story 1

- [X] T004 [US1] Add `catch (CheckTextMatchException)` clause in `ScrapingFlowEngine.ExecuteAsync` to re-throw without wrapping (with best-effort error screenshot) in src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs
- [X] T005 [US1] Verify all new and existing unit tests pass with `dotnet test tests/BizScraper.UnitTests`

**Checkpoint**: At this point, `CheckTextMatchException` propagates as-is → `SearchCoreAsync` catches it → `ExceededRecordCountException` → 422. US1 is fully functional.

---

## Phase 4: User Story 2 - No-Retry Verification (Priority: P2)

**Goal**: Confirm that the Polly retry pipeline does NOT retry `CheckTextMatchException` or `ExceededRecordCountException`, while still retrying transient errors.

**Independent Test**: Verify zero retries when `CheckTextMatchException` is thrown; verify retries still occur for `PlaywrightException`.

### Tests for User Story 2

- [X] T006 [US2] Write unit test: Polly retry pipeline does not retry when `ExceededRecordCountException` is thrown (verify callback invoked exactly once) in tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/ScrapingFlowEngineTests.cs

### Verification for User Story 2

- [X] T007 [US2] Verify existing integration test `ErrorResponseContractTests.Endpoint_ReturnsProblemDetailsFor422502And503` still passes (confirms 422 contract) with `dotnet test tests/BizScraper.IntegrationTests --filter "FullyQualifiedName~ErrorResponseContractTests"`

**Checkpoint**: US2 verifies the no-retry behavior. No production code changes needed — the Polly `ShouldHandle` predicate already excludes both exception types.

---

## Phase 5: User Story 3 - Audit Trail Recording (Priority: P3)

**Goal**: Confirm audit trail records `ExceededRecordCount` status when the error occurs.

**Independent Test**: Trigger the error and verify audit entry has status "ExceededRecordCount".

### Verification for User Story 3

- [X] T008 [US3] Verify `SearchBusinessEntitiesHandler.MapStatus` maps `ExceededRecordCountException` to `"ExceededRecordCount"` — review existing code path and confirm handler catch block writes audit before re-throwing in src/BizScraper.Api/Features/BusinessSearch/Handlers/SearchBusinessEntitiesHandler.cs

**Checkpoint**: US3 is satisfied by existing infrastructure — `MapStatus` and `WriteAuditAsync` already handle this exception type correctly once it becomes reachable (which US1 provides).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Full regression validation

- [X] T009 Run full unit test suite with `dotnet test tests/BizScraper.UnitTests`
- [X] T010 Run full integration test suite with `dotnet test tests/BizScraper.IntegrationTests`
- [X] T011 Run quickstart.md validation steps from specs/012-co-record-count-error/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Skipped
- **Phase 2 (Foundational)**: Skipped
- **Phase 3 (US1)**: No dependencies — starts immediately. Tests written first (T001-T003 in parallel), then implementation (T004), then verification (T005).
- **Phase 4 (US2)**: Depends on T004 (the production fix). T006-T007 can proceed after T004.
- **Phase 5 (US3)**: Depends on T004 (the production fix). T008 is a code review/verification task.
- **Phase 6 (Polish)**: Depends on all previous phases. T009-T010 in parallel, T011 last.

### User Story Dependencies

- **US1 (P1)**: Independent — provides the fix that enables all other stories
- **US2 (P2)**: Depends on US1 completion (T004) for the exception to propagate correctly
- **US3 (P3)**: Depends on US1 completion (T004) for the audit trail path to become reachable

### Parallel Opportunities

```
# Phase 3 - Tests first (all parallel):
T001: Unit test - CheckTextMatchException propagates
T002: Unit test - Other exceptions still wrapped
T003: Unit test - GlobalExceptionHandler maps to 422

# Phase 3 - Implementation (sequential after tests):
T004: The production fix
T005: Verify all unit tests pass

# Phase 4 + Phase 5 (parallel after T004):
T006: No-retry verification test
T007: Integration test verification
T008: Audit trail code review
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Write failing unit tests (T001-T003)
2. Apply the one-line production fix (T004)
3. Verify tests pass (T005)
4. **STOP and VALIDATE**: US1 delivers full user value — 422 response with no retries

### Incremental Delivery

1. US1 → Production fix enables semantic error response (MVP!)
2. US2 → No-retry behavior verified via tests
3. US3 → Audit trail recording confirmed reachable
4. Polish → Full regression suite green

---

## Summary

| Metric | Value |
|--------|-------|
| Total tasks | 11 |
| Tasks per user story | US1: 5, US2: 2, US3: 1, Polish: 3 |
| Production code files modified | 1 (`ScrapingFlowEngine.cs`) |
| New test files | 1 (`ScrapingFlowEngineTests.cs`) |
| Extended test files | 1 (`GlobalExceptionHandlerTests.cs`) |
| Parallel opportunities | T001+T002+T003, T006+T007+T008, T009+T010 |
| MVP scope | US1 only (T001-T005) |
