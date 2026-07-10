# Tasks: Show No Records Found in Both UI and API

**Input**: Design documents from `/specs/027-no-records-found/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Included — spec requires TDD per constitution (Test-First, Principle I).

**Organization**: Tasks grouped by user story. US2 (API) is Phase 3 because it is a prerequisite for US1 (UI), which consumes the API response.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Exact file paths included in descriptions

---

## Phase 1: Setup

**Purpose**: No new projects or infrastructure. This feature modifies existing files only.

- [X] T001 Verify feature branch `027-no-records-found` is checked out and builds cleanly

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared model changes that both user stories depend on.

**⚠️ CRITICAL**: US1 and US2 implementation tasks depend on these completing first.

- [X] T002 Add `ResultCount` property (int?) with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` and `[JsonPropertyName("resultCount")]` to `ExecuteScriptResponse` in `src/BizScraper.Api/Features/ExecuteScript/Models/ExecuteScriptResponse.cs`
- [X] T003 [P] Add `resultCount?: number` to `ExecuteScriptResponse` interface in `src/test-ui/src/app/shared/models/execute-script.model.ts`

**Checkpoint**: Both API and UI response models are updated. No behaviour change yet until handler and component are wired.

---

## Phase 3: User Story 2 — No Records Indicator in API Response (Priority: P2 but implemented first as prerequisite)

**Goal**: API business-search responses include `resultCount` field; non-search responses omit it.

**Independent Test**: `POST /api/v2/execute-script` with a nonexistent search term returns `resultCount: 0` and `data: []`. Entity-details response has no `resultCount` key.

### Tests for User Story 2

> **Write these tests FIRST, ensure they FAIL before implementation**

- [X] T004 [US2] Write unit test `HandleAsync_BusinessSearch_SetsResultCount` in `tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerTests.cs` — mock scraper to return `FlowExecutionResult` with a JSON array of 2 search results for `us-co-business-search`, assert `response.ResultCount == 2`. Use `[Theory]` with `[InlineData]` for all 4 slugs (`us-co-business-search`, `us-wy-business-search`, `us-ia-business-search`, `de-de-business-search`) to satisfy SC-004
- [X] T005 [P] [US2] Write unit test `HandleAsync_BusinessSearch_ZeroResults_SetsResultCountZero` in `tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerTests.cs` — mock scraper to return empty JSON array, assert `response.ResultCount == 0`. Use `[Theory]` with all 4 search slugs
- [X] T006 [P] [US2] Write unit test `HandleAsync_EntityDetails_ResultCountIsNull` in `tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerTests.cs` — mock scraper for `us-co-entity-details`, assert `response.ResultCount is null`

### Implementation for User Story 2

- [X] T007 [US2] Set `ResultCount` from `UnifiedSearchResult[].Length` after `ApplyPostProcessingMapper` in `src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs` — add `if (data is UnifiedSearchResult[] arr)` check before building the response, set `ResultCount = arr.Length`
- [X] T008 [US2] Run US2 tests and verify all 3 pass (`dotnet test --filter "FullyQualifiedName~ExecuteScriptHandlerTests"`)

**Checkpoint**: API returns `resultCount` for all business-search slugs (CO, WY, IA, DE) and omits it for entity-details. US2 acceptance scenarios 1-3 are satisfied.

---

## Phase 4: User Story 1 — No Records Found Message in UI (Priority: P1)

**Goal**: UI displays distinct states for initial prompt, no-records-found, and truncated-empty.

**Independent Test**: Search for "ZZZZNONEXISTENT99999" in any state → UI shows informational notification with `No records found for "ZZZZNONEXISTENT99999" in {stateName}`.

### Tests for User Story 1

> **Write these tests FIRST, ensure they FAIL before implementation**

- [X] T009 [US1] Write Angular test `should show initial prompt when no search performed` in `src/test-ui/src/app/features/search/search.component.spec.ts` — verify the empty-state prompt text is rendered on initial load
- [X] T010 [P] [US1] Write Angular test `should show no-records-found notification after empty search` in `src/test-ui/src/app/features/search/search.component.spec.ts` — mock service to return `{ data: [], truncated: false, resultCount: 0 }`, trigger `onSearch`, verify notification-pds3 with type="information" renders with expected message. Include a sub-case for `{ data: null, truncated: false }` to verify null data is treated as empty (spec edge case)
- [X] T011 [P] [US1] Write Angular test `should show truncation warning when truncated with zero results` in `src/test-ui/src/app/features/search/search.component.spec.ts` — mock service to return `{ data: [], truncated: true, resultCount: 0 }`, trigger `onSearch`, verify warning message renders instead of no-records notification
- [X] T012 [P] [US1] Write Angular test `should replace no-records with results table on successful search` in `src/test-ui/src/app/features/search/search.component.spec.ts` — perform empty search then a populated search, verify results table appears and notification disappears

### Implementation for User Story 1

- [X] T013 [US1] Add `hasSearched` signal, `lastSearchTerm`, and `lastSearchState` tracking to `src/test-ui/src/app/features/search/search.component.ts` — set `hasSearched.set(true)` and capture term/state in `onSearch()`, handle `truncated + empty` in the subscribe callback by setting `warning()` signal
- [X] T014 [US1] Replace the single empty-state `@if` block in `src/test-ui/src/app/features/search/search.component.html` with two distinct blocks: (a) initial prompt when `!hasSearched()`, (b) `<notification-pds3 type="information">` with no-records message when `hasSearched() && results().length === 0 && !error() && !warning()`
- [X] T015 [US1] Run US1 Angular tests and verify all 4 pass (`npx ng test --include="**/search.component.spec.ts" --watch=false`)

**Checkpoint**: UI correctly distinguishes initial prompt, no-records-found, truncated-empty, and results states. US1 acceptance scenarios 1-4 are satisfied.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Documentation updates and final validation.

- [X] T016 [P] Update `ExecuteScriptResponse` table in `specs/current-specs/data-model.md` to include `ResultCount` field
- [X] T017 [P] Update response example in `specs/current-specs/contracts/execute-script.md` to include `resultCount` field and serialization note
- [X] T018 [P] Update Business Search section (§2) in `specs/current-specs/spec.md` to document `resultCount` field on search responses and the four-state UI behaviour (initial, no-records, results, truncated-empty)
- [X] T019 Run full quickstart.md validation from `specs/027-no-records-found/quickstart.md` — verify all API curl examples and UI manual test steps

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — verify branch and build
- **Foundational (Phase 2)**: Depends on Setup — model changes that BLOCK both stories
- **User Story 2 (Phase 3)**: Depends on Phase 2 — API handler changes
- **User Story 1 (Phase 4)**: Depends on Phase 2 (model) and Phase 3 (API response shape used by UI subscribe callback for `truncated` detection)
- **Polish (Phase 5)**: Depends on Phases 3 and 4 completion

### User Story Dependencies

- **User Story 2 (API)**: Can start after Foundational (Phase 2) — no dependency on US1
- **User Story 1 (UI)**: Depends on Phase 2 (TypeScript model) and benefits from US2 being complete (so the API returns `truncated` + `resultCount` correctly for the `truncated + empty` edge case detection)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Model changes before handler/component logic
- Handler/component logic before template changes
- Verify tests pass after implementation

### Parallel Opportunities

- T002 and T003 (Phase 2 model changes) can run in parallel — different languages/files
- T005 and T006 (US2 tests) can run in parallel after T004 — different test scenarios
- T010, T011, T012 (US1 tests) can run in parallel after T009 — different test scenarios
- T016, T017, and T018 (Phase 5 docs) can run in parallel — different files

---

## Parallel Example: User Story 2

```text
# After Phase 2 completes, launch tests in parallel:
T005: "Write test HandleAsync_BusinessSearch_ZeroResults_SetsResultCountZero"
T006: "Write test HandleAsync_EntityDetails_ResultCountIsNull"

# T004 should come first (it establishes the test pattern for the other two)
```

## Parallel Example: User Story 1

```text
# After T009 (first test establishes pattern), launch remaining tests in parallel:
T010: "Write test should show no-records-found notification after empty search"
T011: "Write test should show truncation warning when truncated with zero results"
T012: "Write test should replace no-records with results table on successful search"
```

---

## Implementation Strategy

### MVP First (User Story 2 Only)

1. Complete Phase 1: Setup (verify build)
2. Complete Phase 2: Foundational (model changes)
3. Complete Phase 3: User Story 2 (API resultCount)
4. **STOP and VALIDATE**: API correctly returns resultCount for searches, omits for details
5. Deploy API — backward-compatible, additive change

### Full Delivery

1. Complete Phases 1–3 (MVP — API change)
2. Complete Phase 4: User Story 1 (UI states)
3. Complete Phase 5: Polish (docs + validation)
4. All acceptance scenarios verified

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable after its phase checkpoint
- Commit after each task or logical group
- Stop at any checkpoint to validate the story independently
