# Tasks: CO Entity Certified Document Download

**Input**: Design documents from `/specs/034-co-entity-doc-download/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Required by constitution Principle I (Test-First). Test tasks precede implementation in each phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No project initialization needed — this feature extends existing code. This phase is intentionally empty.

_(No setup tasks — all changes are additive to existing files.)_

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: JSON definition and compose block changes that MUST be in place before any user story implementation.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T001 Add `certifiedDocumentsViewerUrl` and `certifiedDocumentsLocalPath` variables to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-co-entity-details.json`
- [X] T002 Add actions 005–008 (click certified copies link, screenshot list page, click All documents, extract CertificationViewer.do URL) to `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-co-entity-details.json`
- [X] T003 Add `certifiedDocumentsUrl` entry to the output compose block in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-co-entity-details.json`

**Checkpoint**: Definition file updated — post-processing and mapper work can now begin.

---

## Phase 3: User Story 1 — Download All Certified Document Copies (Priority: P1) 🎯 MVP

**Goal**: When scraped CO entity has a "Get certified copies of documents" link, navigate through the 3-page flow, download the combined PDF via browser API request, upload to blob storage, and return a proxy URL as `certifiedDocumentsUrl` in the response.

**Independent Test**: Call execute-script for `us-co-entity-details` with entity 20201222644. Response includes `certifiedDocumentsUrl` with a valid proxy URL. PDF is downloadable via proxy.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T004 [P] [US1] Unit test for `ProcessCoCertifiedDocumentsAsync` in `tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/GenericScriptScraperCoCertDocsTests.cs` — test download/upload happy path (mocked `IBrowserContext.APIRequest`, mocked `IBlobStorageClient`), test skip when `certifiedDocumentsViewerUrl` is absent, test validation rejects non-PDF content-type, test validation rejects zero-byte body
- [X] T005 [P] [US1] Unit test for `MapCoDetails` certified documents mapping in `tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs` — add tests: compose output with both `certificateUrl` and `certifiedDocumentsUrl` → `Certificate.Downloads` has 2 entries; compose output with only `certifiedDocumentsUrl` → `Certificate` section created with 1 entry; compose output with neither → `Certificate` is null

### Implementation for User Story 1

- [X] T006 [US1] Add `ProcessCoCertifiedDocumentsAsync` method to `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — check if `certifiedDocumentsViewerUrl` exists in composed output, download PDF via `browserContext.APIRequest.GetAsync()`, validate content-type is `application/pdf` and body length > 0, save locally as `{idNumber}-alldocuments.pdf` to diagnostics folder, upload to blob storage under `{blobPrefix}/{filename}`, replace `certifiedDocumentsLocalPath` with proxy URL `/api/v1/documents/{blobPrefix}/{filename}`
- [X] T007 [US1] Add CO certified documents post-processing dispatch to `ExecuteCoreAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — after existing WA filings block (~line 345), add condition: if composed output contains `certifiedDocumentsViewerUrl` AND definition slug contains `us-co-entity-details`, call `ProcessCoCertifiedDocumentsAsync`
- [X] T008 [US1] Update `MapCoDetails` in `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs` — read `certifiedDocumentsUrl` from output via `GetStringOrDefault`, when non-empty add a second `DownloadReference` with label "Certified Documents" to `Certificate.Downloads` array; if `certificateUrl` is empty but `certifiedDocumentsUrl` is present, still create `Certificate` section with just the certified docs entry

**Checkpoint**: US1 complete — CO entity details now download certified document copies and return proxy URL.

---

## Phase 4: User Story 2 — Graceful Handling When Documents Unavailable (Priority: P2)

**Goal**: When entity details page does NOT have a "Get certified copies of documents" link, scraper skips the document download step. Response returns normally without `certifiedDocumentsUrl`.

**Independent Test**: Call execute-script for a CO entity without the link (e.g., a delinquent entity). Response has no `certifiedDocumentsUrl` field and no errors.

### Implementation for User Story 2

- [X] T009 [US2] Verify `element-exists` conditions on actions 005-008 in `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-co-entity-details.json` — confirm each action (005: certified copies link, 006: All documents element, 007: All documents link, 008: CertificationViewer.do link) has its own `condition` guard so each is independently skipped when its target element is absent on the current page
- [X] T010 [US2] Verify `ProcessCoCertifiedDocumentsAsync` no-op path in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — when `certifiedDocumentsViewerUrl` is absent or empty in composed output, method exits early without error
- [X] T011 [US2] Verify `MapCoDetails` null-safe path in `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs` — when `certifiedDocumentsUrl` is null/empty, no certified documents entry appears in response

**Checkpoint**: US2 complete — entities without certified documents return cleanly.

---

## Phase 5: User Story 3 — Error Handling for Download Failures (Priority: P3)

**Goal**: When the link exists but download fails (timeout, non-PDF response, empty body, blob upload error), record error on the certified documents field without failing the overall entity details request.

**Independent Test**: Simulate a timeout or invalid response during certified document download. Entity details and certificate still returned; certified documents field contains error message.

### Implementation for User Story 3

- [X] T012 [US3] Add error handling to `ProcessCoCertifiedDocumentsAsync` in `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs` — wrap download/upload in try-catch, on failure set `certifiedDocumentsLocalPath` to null and log error with correlation ID, validate content-type before upload, reject zero-byte responses
- [X] T013 [US3] Ensure `MapCoDetails` handles error state in `src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs` — when `certifiedDocumentsUrl` is null but download was attempted (link was present), map with `Available = false` and appropriate error message if available in output

**Checkpoint**: US3 complete — download failures are isolated and do not break entity detail retrieval.

---

## Phase 6: Test UI — Certified Documents Download Button

**Purpose**: Add "Certified Documents" download button to the test UI entity details view.

- [X] T014 [P] [US1] Add certified documents card to `src/test-ui/src/app/features/entity-details/entity-details.component.html` — after the certificate card block, add a new `card-pds3` section that checks for certified documents downloads in the response, renders a download button using `viewDocument()` with `'certdocs-' + $index` key and label "Certified Documents"
- [X] T015 [P] [US1] Update entity details component type/interface in `src/test-ui/src/app/features/entity-details/entity-details.component.ts` — ensure the response type uses existing `certificate.downloads` array (certified docs are mapped as a second `DownloadReference` entry in that array)

**Checkpoint**: Test UI shows Certified Documents button alongside certificate download.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation and cleanup.

- [X] T016 [P] Update `specs/034-co-entity-doc-download/quickstart.md` with actual response shape after implementation
- [ ] T017 Run quickstart.md validation — ⚠ requires running API instance with Playwright + blob storage
- [X] T018 [P] Verify FR-012 independence — run execute-script for a CO entity that has both certificate AND certified documents; confirm both downloads succeed independently and failure of one does not affect the other

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Empty — no work needed
- **Phase 2 (Foundational)**: No dependencies — start immediately. T001 → T002 → T003 (sequential, same file)
- **Phase 3 (US1)**: Depends on Phase 2 completion. T004 ∥ T005 (tests, parallel, different files) → T006 → T007 (sequential, same file), T008 (parallel-safe, different file)
- **Phase 4 (US2)**: Depends on Phase 3 (verifies US1 code paths). T009, T010, T011 can run in parallel
- **Phase 5 (US3)**: Depends on Phase 3. T012 → T013 (sequential, builds on US1 code)
- **Phase 6 (Test UI)**: Depends on Phase 3 (needs response shape finalized). T014 ∥ T015 (parallel, different files)
- **Phase 7 (Polish)**: Depends on all previous phases. T016 ∥ T017 ∥ T018

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational (Phase 2) — standalone MVP
- **US2 (P2)**: Depends on US1 code being in place (verifies skip/null paths)
- **US3 (P3)**: Depends on US1 code being in place (adds error handling around it)

### Within User Story 1

1. T004 ∥ T005 — Write tests first (parallel, different files)
2. T006 — Create the post-processing method (must pass T004 tests)
3. T007 — Wire the dispatch call (depends on T006)
4. T008 — Update mapper (can parallel with T007 since different file, must pass T005 tests)

### Parallel Opportunities

```text
# After Phase 2 completes:
# Stream A (backend tests + implementation):
T004 ∥ T005 → T006 → T007 → T008

# Stream B (test UI, after T008 completes):
T014 ∥ T015

# Stream C (verification, after T008):
T009 ∥ T010 ∥ T011 (US2 verification)
T012 → T013 (US3 error handling)

# Stream D (polish, after all):
T016 ∥ T017 ∥ T018
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: JSON definition updates (T001–T003)
2. Write tests first: T004 (post-processing tests) ∥ T005 (mapper tests)
3. Complete Phase 3 implementation: T006–T008
4. **STOP and VALIDATE**: Run tests, then test with entity 20201222644 — verify `certifiedDocumentsUrl` in response
5. Deploy/demo if ready

### Incremental Delivery

1. Phase 2 → Definition ready
2. Phase 3 (US1) → Tests + core download works → **MVP!**
3. Phase 4 (US2) → Graceful skip verified
4. Phase 5 (US3) → Error isolation confirmed
5. Phase 6 → Test UI button added
6. Phase 7 → Polish, validate, and verify FR-012 independence

---

## Notes

- All definition changes (T001–T003) are in the SAME file — execute sequentially
- Tests (T004–T005) MUST be written and FAIL before implementation begins (constitution Principle I)
- Post-processing method (T006) follows `ProcessHistoryDocumentsAsync` pattern from WY
- Post-processing dispatch (T007) wires into `ExecuteCoreAsync` after WA filings block (~line 345)
- Mapper update (T008) follows existing `certificateUrl` mapping pattern in `MapCoDetails` — adds to `Certificate.Downloads` array
- Actions 006-008 each have independent `element-exists` conditions to prevent execution on wrong page when action-005 is skipped
- Test UI (T014–T015) follows existing certificate card pattern — no new response field needed, certified docs appear as additional `DownloadReference` in `Certificate.Downloads`
- No new C# model types needed — reuses `CertificateSection` and `DownloadReference`
- No database schema changes required
- T018 validates FR-012: certificate and certified documents downloads are independent
