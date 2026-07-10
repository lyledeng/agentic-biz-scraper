# Tasks: WY Filing History Document Download Fix

**Input**: Design documents from `/specs/029-wy-history-doc-fix/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Not explicitly requested in spec. Unit tests included for the core upload logic only (constitution requires test coverage for new code).

**Organization**: US2 (error handling) and US3 (cloud storage) are inherently fulfilled by the US1 implementation — the upload step includes per-document error isolation and blob storage in a single code path. They share Phase 3 rather than separate phases.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Add constructor dependencies to enable blob upload in the scraper

- [X] T001 Add `IHttpContextAccessor` and `IConfiguration` constructor parameters to `GenericScriptScraper` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` (adapted from plan — `JsonDrivenEntityDetailScraper` is dead code; the v2 execute-script path routes through `GenericScriptScraper`)
- [X] T002 DI auto-resolves new parameters since `GenericScriptScraper` is registered as `AddSingleton<GenericScriptScraper>()` (no factory update needed)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: No foundational tasks needed — all required infrastructure (IBlobStorageClient, document proxy endpoint, EntityDetailMapper) already exists and requires no changes.

**Checkpoint**: Setup complete — user story implementation can begin.

---

## Phase 3: User Story 1 — View and Download WY Filing History Documents (Priority: P1) 🎯 MVP

**Goal**: After scraping, each successfully downloaded history document is uploaded to Azure Blob Storage and its `StorageUrl` replaced with a proxy URL so users can access it.

**Independent Test**: Execute `us-wy-entity-details` for a WY entity with filing history; verify `historyDocuments[].storageUrl` contains `/api/v1/documents/...` proxy URLs; fetch one via the document proxy endpoint.

**Covers**: US1 (core download fix), US2 (upload failure error handling), US3 (cloud storage)

### Unit Tests (TDD — Written First)

- [X] T003 [P] [US1] Write unit test: successful download + upload replaces `StorageUrl` with proxy URL in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/HistoryDocumentUploadTests.cs`
- [X] T004 [P] [US1] Write unit test: blob upload failure sets `Error` and clears `StorageUrl` (document-level isolation) in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/HistoryDocumentUploadTests.cs`
- [X] T005 [P] [US1] Write unit test: documents with no `StorageUrl` or `FileName` are passed through unchanged in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/HistoryDocumentUploadTests.cs`

### Implementation (Makes Tests Pass)

- [X] T006 [US1] Add `ProcessHistoryDocumentsAsync` static method with `<summary>` doc-comment to `GenericScriptScraper` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — downloads history docs via browser context, uploads to blob, builds proxy URL, replaces `storageUrl` in composed output (pattern from `DownloadActionHandler`)
- [X] T007 [US1] Integrate history document processing into `GenericScriptScraper.ExecuteAsync()` — after flow engine returns, detect `historyDocuments` in composed output, call `ProcessHistoryDocumentsAsync` while browser context is alive (FR-001, FR-002, FR-004, FR-005, FR-007, FR-008, FR-009)
- [X] T008 [US1] Structured log messages already exist in `EntityDetailsLogMessages.cs` (EventIds 2008–2011); `ProcessHistoryDocumentsAsync` uses standard `ILogger.Log*` methods for download/upload progress

**Checkpoint**: API returns proxy URLs for WY filing history documents. Users can click and view PDFs via the existing document proxy and test-ui viewer.

---

## Phase 4: UI Polish — Upgrade Document Buttons to Jumpstart PDS3

**Goal**: Replace custom `doc-open-btn` buttons with Jumpstart `button-field-pds3 type="secondary" size="small"` with `wk-icon-file-pdf` icon so document actions stand out visually.

**Independent Test**: Open entity details in the test-ui; verify all document buttons (certificate, filing history, DE documents) use Jumpstart secondary button styling with PDF icon.

### Implementation

- [X] T009 [P] Jumpstart `JumpstartComponentsModule` already imported (includes `button-field-pds3` and `icon-pds3`) in `entity-details.component.ts`
- [X] T010 Replace all `<button type="button" class="doc-open-btn ...">` elements with `<button-field-pds3 type="secondary" size="small">` + `<icon-pds3 name="wk-icon-file-pdf">` in `entity-details.component.html` for certificate and document sections
- [X] T011 [P] Remove custom `.doc-open-btn`, `.doc-open-btn:hover`, `.doc-open-btn--active`, `.doc-open-btn--compact` CSS rules from `entity-details.component.css`
- [X] T012 Update test selectors from `button.doc-open-btn` to `button-field-pds3 button` in `entity-details.component.spec.ts`

**Checkpoint**: All document buttons across the entity details page use Jumpstart PDS3 styling with PDF icon.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation

- [X] T013 [P] Verify build passes with zero warnings (`dotnet build` with `TreatWarningsAsErrors`) — 0 warnings, 0 errors
- [X] T014 [P] Verify all existing unit tests pass (`dotnet test`) — 206 passed, 0 failed
- [X] T015 [P] Verify test-ui builds with zero errors (`npx ng build`) — exit code 0
- [ ] T016 Run quickstart.md validation — execute the manual test steps from `specs/029-wy-history-doc-fix/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Story 1 (Phase 3)**: Depends on Phase 1 (T001, T002)
- **UI Polish (Phase 4)**: No dependency on Phase 3 — can run in parallel
- **Polish (Phase 5)**: Depends on Phase 3 and Phase 4

### Within Phase 3

- T003, T004, T005 (unit tests) can run in parallel — written first per TDD (Constitution I)
- T006 (private method) → T007 (integrate into loop) → T008 (logging) — makes tests pass
- T006 depends on T003, T004, T005 (tests written first)

### Within Phase 4

- T009 (imports) → T010 (template) → T012 (test selectors)
- T011 (CSS removal) can run in parallel with T010

### Parallel Opportunities

```
Phase 1:  T001 → T002
                    ↓
Phase 3:  T003 ──┐
          T004 ──┼── (parallel, TDD tests first)
          T005 ──┘
                 ↓
          T006 → T007 → T008
                    ↓                    ↓ (parallel track)
Phase 4:  T009 → T010 → T012      Phase 4 (parallel with Phase 3)
          T011 ──┘ (parallel)
                    ↓
Phase 5:  T013, T014, T015 (parallel) → T016
```

## Implementation Strategy

### MVP Scope

Phase 1 + Phase 3 = **T001–T008** (8 tasks). This delivers the core bug fix: WY filing history documents accessible via proxy URLs. Test-ui already renders them correctly — the buttons just won't have Jumpstart styling yet.

### Full Scope

All phases: **T001–T016** (16 tasks). Adds Jumpstart PDS3 button styling for document links.

### Task Summary

| Phase | Tasks | Parallel? | Description |
|-------|-------|-----------|-------------|
| Setup | T001–T002 | Sequential | Add constructor dependencies |
| US1 (P1) | T003–T008 | Partial | Core upload fix + unit tests |
| UI Polish | T009–T012 | Partial | Jumpstart button upgrade |
| Polish | T013–T016 | Mostly | Build verification + quickstart |
| **Total** | **16 tasks** | | |
