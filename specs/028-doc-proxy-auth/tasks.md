# Tasks: Document Proxy Authenticated Download

**Input**: Design documents from `/specs/028-doc-proxy-auth/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Required per Constitution Principle I (Test-First, NON-NEGOTIABLE). Unit tests included.

**Organization**: Single user story (US1) — US2 was merged into US1 during clarification.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: No new project structure needed — this feature adds files to the existing Angular test-ui.

- [X] T001 Verify MSAL interceptor configuration covers document proxy URLs in src/test-ui/src/app/core/auth/auth.config.ts

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: New Angular service that US1 implementation depends on.

**⚠️ CRITICAL**: The DocumentProxyService must be complete before template changes.

- [X] T002 Create DocumentProxyService with fetchDocument method in src/test-ui/src/app/core/services/document-proxy.service.ts
- [X] T003 Create DocumentViewerState interface in src/test-ui/src/app/shared/models/document-viewer.model.ts

**Checkpoint**: Foundation ready — DocumentProxyService injectable and DocumentViewerState model available.

---

## Phase 3: User Story 1 — View Document Inline (Priority: P1) 🎯 MVP

**Goal**: Replace all `<a href>` document links with HttpClient-based inline PDF viewing that passes through the MSAL interceptor, fixing the 401 regression.

**Independent Test**: Sign in, navigate to a WY entity with history documents, click any document link, verify the PDF renders inline without 401.

### Implementation for User Story 1

- [X] T004 [US1] Add inline viewer state management (viewerStates Map, viewDocument/retryDocument/closeViewer methods, HTTP status → user message error mapping, OnDestroy blob URL cleanup, single-viewer-at-a-time behaviour) to src/test-ui/src/app/features/entity-details/entity-details.component.ts
- [X] T005 [US1] Replace certificate `<a href>` links with click-to-view buttons and inline `<object>` PDF viewer in src/test-ui/src/app/features/entity-details/entity-details.component.html (the `@if (d.certificate)` block)
- [X] T006 [US1] Replace documents table `<a href>` links with click-to-view buttons and inline `<object>` PDF viewer in src/test-ui/src/app/features/entity-details/entity-details.component.html (the `@if (d.documents)` block)
- [X] T007 [US1] Add inline error display with categorised messages and Retry button for failed document fetches in src/test-ui/src/app/features/entity-details/entity-details.component.html
- [X] T008 [US1] Add CSS styles for inline PDF viewer container, loading state, error state, and retry button in src/test-ui/src/app/features/entity-details/entity-details.component.css
- [X] T009 [US1] Verify CSP compatibility — check nginx config for `object-src blob:` in src/test-ui/nginx.conf (if present)

**Checkpoint**: All document links (certificates, WY filings, DE originals/translated) render PDFs inline. No 401 errors. Blob URLs cleaned up on navigation.

---

## Phase 4: Tests (Constitution Principle I)

**Purpose**: Unit tests per TDD discipline. Written before verifying implementation passes.

- [X] T010 [P] [US1] Unit test for DocumentProxyService — verify fetchDocument returns blob on success, verify error observable on 401/404/502/504 in src/test-ui/src/app/core/services/document-proxy.service.spec.ts
- [X] T011 [P] [US1] Unit test for EntityDetailsComponent — verify viewDocument sets loading→loaded state, verify error state with categorised message on failure, verify retryDocument re-fetches, verify ngOnDestroy revokes all blob URLs, verify single-viewer-at-a-time behaviour in src/test-ui/src/app/features/entity-details/entity-details.component.spec.ts

**Checkpoint**: All tests written and passing against implementation.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Documentation and current-specs alignment.

- [X] T012 [P] Update current-specs documentation in specs/current-specs/spec.md to reflect inline document viewing (already drafted during planning — verify accuracy)
- [X] T013 [P] Run quickstart.md validation — follow steps in specs/028-doc-proxy-auth/quickstart.md end-to-end
- [X] T014 Build test-ui and verify no compilation errors via `npx ng build`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — verification only
- **Foundational (Phase 2)**: Depends on Phase 1 — creates the service and model
- **User Story 1 (Phase 3)**: Depends on Phase 2 — component changes use DocumentProxyService
- **Tests (Phase 4)**: Depends on Phase 3 — tests verify implementation
- **Polish (Phase 5)**: Depends on Phase 4 — docs and build validation

### Within User Story 1

```text
T004 (component TS) ──► T005 + T006 + T007 (template changes, can be done together)
                                │
                                ▼
                         T008 (CSS styles)
                                │
                                ▼
                         T009 (CSP check)
```

- T004 must come first (adds methods the template calls)
- T005, T006, T007 modify different sections of the same template — apply sequentially
- T008 adds styles referenced by T005-T007
- T009 is a verification step

### Parallel Opportunities

- T002 and T003 can run in parallel (different files, no dependencies)
- T010 and T011 can run in parallel (different test files, no dependencies)
- T012 and T013 can run in parallel (different concerns)

---

## Parallel Example: Foundational Phase

```bash
# Launch foundational tasks together:
Task T002: "Create DocumentProxyService in src/test-ui/src/app/core/services/document-proxy.service.ts"
Task T003: "Create DocumentViewerState interface in src/test-ui/src/app/shared/models/document-viewer.model.ts"
```

---

## Implementation Strategy

### MVP First (Single Story)

1. Complete Phase 1: Setup verification (T001)
2. Complete Phase 2: Service + model (T002, T003)
3. Complete Phase 3: Component changes (T004-T009)
4. Complete Phase 4: Unit tests (T010, T011)
5. **STOP and VALIDATE**: Test inline PDF viewing for WY, CO, DE entities
6. Complete Phase 5: Documentation + build verification (T012-T014)

### Key Constraint

This is a **UI-only fix**. No API backend changes. The document proxy endpoint already works correctly — the fix ensures the Angular test-ui passes the bearer token when fetching documents.

---

## Notes

- All changes are in `src/test-ui/` — no backend changes needed
- The MSAL interceptor already attaches tokens to HttpClient requests matching the API base URL
- Blob URLs must be revoked on component destroy to prevent memory leaks
- Error messages are categorised by HTTP status (401 → re-auth, 404 → not found, 502/504 → retry)
- Commit after each task or logical group
