# Tasks: Fix Iowa Search and Test UI

**Input**: Design documents from `/specs/024-ia-search-testui-fix/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Required by Constitution Principle I (Test-First, NON-NEGOTIABLE). Test tasks precede implementation per Red-Green-Refactor. Unit tests for model extensions and mapper update; integration tests deferred to quickstart validation (T017).

**Pre-Existing Coverage**: FR-009 (test UI Iowa invocation) is covered by existing infrastructure — IA is already wired into `StateCode`, `STATE_SLUG_MAP`, and `STATE_DETAIL_SLUG` (confirmed in research.md §7). FR-012 (TLS tolerance) is already implemented globally — `IgnoreHTTPSErrors = true` is set in `GenericScriptScraper.cs`, `JsonDrivenSearchScraper.cs`, and `JsonDrivenEntityDetailScraper.cs`. No tasks needed for either.

**Organization**: Tasks grouped by user story from spec.md (3 P1 stories). All stories are P1 but ordered by dependency: US1 (search) → US2 (details) → US3 (test UI).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Exact file paths included in all descriptions

---

## Phase 1: Setup

**Purpose**: Deploy v2 definition files that replace the non-functional stubs — the root cause of the entire issue.

- [X] T001 Replace stub definition with complete v2 search definition by copying `specs/024-ia-search-testui-fix/contracts/us-ia-business-search.json` to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json`
- [X] T002 [P] Replace stub definition with complete v2 entity details definition by copying `specs/024-ia-search-testui-fix/contracts/us-ia-entity-details.json` to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json`

---

## Phase 2: Foundational (Model Extensions)

**Purpose**: Extend C# models with Iowa-specific fields that US2 (details mapping) and US3 (test UI) both depend on.

**⚠️ CRITICAL**: US2 and US3 cannot begin until these model changes are in place.

### Tests for Phase 2 (RED — must fail before implementation)

- [X] T003 [P] Write unit test for `IowaNameEntry` serialization round-trip in `tests/BizScraper.UnitTests/Common/Models/IowaNameEntryTests.cs`: verify JSON serialization via `AppJsonSerializerContext` produces expected camelCase output and deserializes back with all fields (`name`, `type`, `status`, `modified`) intact. Test MUST fail (class does not exist yet).
- [X] T004 [P] Write unit test for `DetailSection` Iowa field serialization in `tests/BizScraper.UnitTests/Common/Models/DetailSectionIowaFieldsTests.cs`: verify that `ChapterCode`, `CertificateNote`, and `IowaNames` serialize as `null` when unset and serialize correctly when populated. Test MUST fail (properties do not exist yet).

### Implementation for Phase 2 (GREEN — make tests pass)

- [X] T005 Create `IowaNameEntry` record in `src/BizScraper.Api/Common/Models/IowaNameEntry.cs` with properties: Name (required string), Type (required string), Status (required string), Modified (bool)
- [X] T006 [P] Add nullable Iowa-specific properties to `DetailSection` in `src/BizScraper.Api/Common/Models/DetailSection.cs`: `ChapterCode` (string?), `CertificateNote` (string?), `IowaNames` (IReadOnlyList<IowaNameEntry>?)
- [X] T007 Register `IowaNameEntry` and `IReadOnlyList<IowaNameEntry>` in `src/BizScraper.Api/Common/Serialization/AppJsonSerializerContext.cs` with `[JsonSerializable]` attributes

**Checkpoint**: T003 and T004 pass (GREEN). C# models extended — API can serialize Iowa-specific fields. US1 is independently testable at this point (search works with deployed definitions).

---

## Phase 3: User Story 1 — Iowa Business Search Returns Results (Priority: P1) 🎯 MVP

**Goal**: Iowa business search navigates the SOS site, extracts paginated results, and returns normalized response via `POST /api/v2/execute-script`.

**Independent Test**: `POST /api/v2/execute-script` with `{definition: "us-ia-business-search", parameters: {searchTerm: "Acme"}}` returns non-empty results with identifier, name, status, entityType, uniqueKey, state fields.

### Implementation for User Story 1

No additional code changes needed. The search definition (T001) handles navigation, form fill, extraction, and pagination. The existing `SearchResultMapper.MapIaResults` correctly maps the extracted output to the normalized schema. The existing `UniqueKeyEncoder` correctly encodes Iowa keys with `US-IA` prefix.

**Checkpoint**: Iowa search is fully functional after T001 + T002. Verify with quickstart.md search request.

---

## Phase 4: User Story 2 — Iowa Entity Details Drill-Down Returns Full Profile (Priority: P1)

**Goal**: Iowa entity detail drill-down accepts a uniqueKey, navigates to the entity summary page, extracts the full profile with Iowa-specific fields, and returns it in the unified detail format.

**Independent Test**: Execute Iowa search, take a `uniqueKey` from results, invoke `POST /api/v2/execute-script` with `{definition: "us-ia-entity-details", parameters: {uniqueKey: "<key>"}}` — verify full profile with `chapterCode`, `certificateNote`, `iowaNames`, registered agent, and principal office.

### Tests for User Story 2 (RED — must fail before implementation)

- [X] T008 [US2] Write unit test for `MapIaDetails` Iowa field mapping in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperIaTests.cs`: given a mock extraction output with `chapterCode`, `certificateNote`, and `names[]` array, verify `MapIaDetails` returns `Details.ChapterCode` populated (not `Details.Jurisdiction`), `Details.CertificateNote` populated (not `Certificate.Error`), `Details.IowaNames` as `IowaNameEntry[]` (not `PartyEntry[]`), `Certificate.Available = false`, and `Parties` is empty. Test MUST fail (mapper still uses old field mappings).

### Implementation for User Story 2 (GREEN — make test pass)

- [X] T009 [US2] Update `EntityDetailMapper.MapIaDetails` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs` to populate `Details.ChapterCode` from `output.chapterCode` (instead of `Details.Jurisdiction`), `Details.CertificateNote` from `output.certificateNote` (instead of `Certificate.Error`), and `Details.IowaNames` from `output.names[]` as `IowaNameEntry[]` (instead of `PartyEntry[]`). Set `Certificate.Available = false`, `Parties = empty list`, `Jurisdiction = null`.

**Checkpoint**: T008 passes (GREEN). Iowa entity details returns full profile with explicit Iowa fields. Verify with quickstart.md entity details request.

---

## Phase 5: User Story 3 — Test UI Supports Iowa Search and Drill-Down (Priority: P1)

**Goal**: Test UI correctly executes Iowa searches, displays results in the grid, and drills down to entity details showing Iowa-specific fields (chapterCode, certificateNote, iowaNames).

**Independent Test**: In the test UI, select Iowa (IA), search for "Farm", verify results grid populates, click a result, verify detail view shows standard fields plus Chapter/Code row, Certificate Note banner, and Iowa Names table.

### Implementation for User Story 3

- [X] T010 [US3] Add Iowa fields to `DetailSection` interface and create `IowaNameEntry` interface in `src/test-ui/src/app/shared/models/unified-entity.model.ts`: add `chapterCode?: string`, `certificateNote?: string`, `iowaNames?: IowaNameEntry[]` to `DetailSection`; add new `IowaNameEntry` interface with `name: string`, `type: string`, `status: string`, `modified: boolean`
- [X] T011 [US3] Update entity-details component template and TypeScript in `src/test-ui/src/app/features/entity-details/entity-details.component.html` and `.ts` to render Iowa-specific sections: (1) "Chapter/Code" row when `detail.details.chapterCode` is non-null, (2) informational notice banner (warn style) when `detail.details.certificateNote` is non-null, (3) Iowa Names table with Name, Type, Status, Modified columns when `detail.details.iowaNames` is non-null and non-empty. Include any helper logic needed in the component TypeScript.
- [X] T012 [US3] Add 503 Retry-After error handling to `src/test-ui/src/app/features/entity-details/entity-details.component.ts`: in the `error` callback, check `err.status === 503` and extract `Retry-After` header to display retry guidance (matching the existing pattern in `search.component.ts` lines 67–72). Currently the component only shows `err.message || 'Failed to load entity details'`.

**Checkpoint**: Test UI renders Iowa search results and entity details with all Iowa-specific fields. 503 errors show retry guidance. Full end-to-end verification per quickstart.md test UI section.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Update global specification snapshot and verify no regressions.

- [X] T013 [P] Update `specs/current-specs/data-model.md` to add `ChapterCode`, `CertificateNote`, `IowaNames` to the EntityDetailResult table and add new `IowaNameEntry` model table
- [X] T014 [P] Update `specs/current-specs/spec.md` to reflect Iowa definitions are complete (not stubs) and note explicit Iowa model fields on the unified entity detail response
- [X] T015 Run regression verification per quickstart.md: execute CO and WY searches to confirm zero regression, verify new nullable Iowa fields are `null` for non-Iowa entities
- [X] T016 Run quickstart.md full validation: Iowa search, Iowa entity details, test UI walkthrough, error scenarios

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: No dependency on Phase 1 — can run in parallel with Setup. Tests (T003, T004) MUST be written and fail before implementation (T005–T007).
- **US1 (Phase 3)**: Depends on Phase 1 (T001 deploys the search definition). No code changes required — search works once definition is deployed.
- **US2 (Phase 4)**: Depends on Phase 1 (T002 deploys details definition) + Phase 2 (T005–T007 provide IowaNameEntry model for mapper). Test (T008) MUST be written and fail before implementation (T009).
- **US3 (Phase 5)**: Depends on Phase 2 (model extensions inform TS model shape). Can start T010 in parallel with US2.
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (Search)**: Independent after T001. No mapper or model changes needed.
- **US2 (Details)**: Requires T002 (definition) + T005/T006/T007 (models) before T008 (test) → T009 (mapper update)
- **US3 (Test UI)**: Requires T006 (C# model shape) to inform T010 (TS model). T011/T012 depend on T010.

### Within Each User Story

- Tests MUST be written FIRST and FAIL (RED)
- Implementation makes tests pass (GREEN)
- Models before services/mappers
- Mappers before UI
- TS models before component template changes

### Parallel Opportunities

- T001 and T002 can run in parallel (different definition files)
- T003 and T004 can run in parallel (different test files)
- T005 and T006 can run in parallel (different C# files)
- T013 and T014 can run in parallel (different spec files)
- T010 can start as soon as T006 is done (doesn't need to wait for T009)

---

## Parallel Example: Phase 1 + Phase 2

```text
# Phase 1 (both in parallel):
T001: Copy search definition          → us-ia-business-search.json
T002: Copy details definition         → us-ia-entity-details.json

# Phase 2 Tests (RED — both in parallel):
T003: Unit test IowaNameEntry         → tests/.../IowaNameEntryTests.cs (FAIL)
T004: Unit test DetailSection Iowa    → tests/.../DetailSectionIowaFieldsTests.cs (FAIL)

# Phase 2 Implementation (GREEN — T005+T006 in parallel, T007 after both):
T005: Create IowaNameEntry.cs         → Common/Models/IowaNameEntry.cs
T006: Extend DetailSection.cs         → Common/Models/DetailSection.cs
T007: Register in serializer context  → Common/Serialization/AppJsonSerializerContext.cs (after T005)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Deploy definitions (T001, T002)
2. Complete Phase 2: Tests RED (T003, T004) → Implementation GREEN (T005, T006, T007)
3. **STOP and VALIDATE**: Iowa search works end-to-end (US1 complete)
4. Proceed to US2 (details mapping) → US3 (test UI) → Polish

### Incremental Delivery

1. T001 + T002 → Definitions deployed (search returns raw data immediately)
2. T003–T007 → Models extended with TDD (API serializes Iowa fields)
3. T008 → T009 → Details mapper updated with TDD (US2 complete — full detail profiles)
4. T010–T012 → Test UI updated (US3 complete — visual verification + 503 handling)
5. T013–T016 → Polish and regression (feature complete)

Each increment adds value without breaking previous states.
