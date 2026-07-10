# Tasks: PDF Proxy Streaming Endpoint

**Input**: Design documents from `/specs/011-pdf-proxy-streaming/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included per Constitution Principle I (Test-First) and project conventions.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the Documents feature folder structure and foundational types

- [X] T001 Create Documents feature folder structure under src/BizScraper.Api/Features/Documents/ with Endpoints/, Handlers/, Models/, Validation/, Logging/ subdirectories
- [X] T002 [P] Create BlobDownloadResult record in src/BizScraper.Api/Common/Interfaces/BlobDownloadResult.cs
- [X] T003 [P] Create StreamDocumentQuery record in src/BizScraper.Api/Features/Documents/Models/StreamDocumentQuery.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend IBlobStorageClient with download capabilities — MUST be complete before any user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Add DownloadBlobAsync and GetBlobETagAsync method signatures to IBlobStorageClient interface in src/BizScraper.Api/Common/Interfaces/IBlobStorageClient.cs
- [X] T005 Implement DownloadBlobAsync and GetBlobETagAsync in AzureBlobStorageClient with ResiliencePipeline (3 retries, 30s timeout) in src/BizScraper.Api/Infrastructure/Storage/AzureBlobStorageClient.cs
- [X] T006 [P] Implement DownloadBlobAsync and GetBlobETagAsync no-op methods in NullBlobStorageClient in src/BizScraper.Api/Infrastructure/Storage/NullBlobStorageClient.cs
- [X] T007 [P] Create BlobPathValidator with [GeneratedRegex] pattern allowlist in src/BizScraper.Api/Features/Documents/Validation/BlobPathValidator.cs
- [X] T008 [P] Create DocumentLogMessages static partial class with [LoggerMessage] attributes (EventId 3000–3005) in src/BizScraper.Api/Features/Documents/Logging/DocumentLogMessages.cs

**Checkpoint**: Storage download capability + validation + logging infrastructure ready — user story implementation can begin

---

## Phase 3: User Story 1 — Stream a Certificate of Good Standing PDF (Priority: P1) 🎯 MVP

**Goal**: Users can stream certificate PDFs through the API proxy endpoint instead of accessing Azure Blob Storage directly

**Independent Test**: Perform entity detail lookup for a WY entity with certificate, GET the proxy URL, verify PDF streams with correct Content-Type and Content-Disposition headers

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T009 [P] [US1] Unit tests for BlobPathValidator — valid paths, invalid paths (empty, traversal, bad chars, too long) in tests/BizScraper.UnitTests/Features/Documents/BlobPathValidatorTests.cs
- [X] T010 [P] [US1] Unit tests for StreamDocumentHandler — happy path (200 stream), blob not found (404), storage unreachable (502), timeout (504), conditional 304 in tests/BizScraper.UnitTests/Features/Documents/StreamDocumentHandlerTests.cs

### Implementation for User Story 1

- [X] T011 [US1] Implement StreamDocumentHandler with IBlobStorageClient dependency, conditional ETag check, streaming response, error mapping, and observability in src/BizScraper.Api/Features/Documents/Handlers/StreamDocumentHandler.cs
- [X] T012 [US1] Create DocumentEndpoints with MapGet /api/v1/documents/{**blobPath} route, blob path validation, If-None-Match extraction, and CQRS dispatch in src/BizScraper.Api/Features/Documents/Endpoints/DocumentEndpoints.cs
- [X] T013 [US1] Register StreamDocumentHandler in DI and call MapDocumentEndpoints() in src/BizScraper.Api/Program.cs

**Checkpoint**: Proxy endpoint functional — certificates can be streamed via `GET /api/v1/documents/{correlationId}/{filename}.pdf`

---

## Phase 4: User Story 2 — Stream a History Document PDF (Priority: P1)

**Goal**: Users can stream history document PDFs (annual reports, filings) through the same proxy endpoint

**Independent Test**: Perform WY entity detail lookup, GET any history document proxy URL, verify chunked streaming without full-memory buffering

### Tests for User Story 2

- [X] T014 [P] [US2] Integration test for end-to-end document streaming — upload blob via test fixture, stream via proxy endpoint, verify content matches in tests/BizScraper.IntegrationTests/Features/Documents/DocumentStreamingTests.cs

### Implementation for User Story 2

> No additional implementation needed — the proxy endpoint from US1 already handles any blob path within the container. US2 validates that history documents (which use the same `{correlationId}/{filename}.pdf` pattern) stream correctly through the same endpoint. The integration test confirms this end-to-end.

**Checkpoint**: Both certificates and history documents stream correctly through the proxy

---

## Phase 5: User Story 3 — Angular Test UI Uses Proxy URLs (Priority: P1)

**Goal**: Entity-details API response returns proxy URLs instead of direct blob URLs; Angular test UI renders PDFs through the API without any client-side URL manipulation

**Independent Test**: Call entity-details API, inspect storageUrl values in response — they must point to `/api/v1/documents/` not to `blob.core.windows.net`

### Tests for User Story 3

- [X] T015 [P] [US3] Unit tests for URL rewriting in GetEntityDetailsHandler — verify storageUrl values are proxy URLs for certificate and history documents, and audit trail URLs remain direct blob URLs in tests/BizScraper.UnitTests/Features/Documents/StorageUrlRewritingTests.cs

### Implementation for User Story 3

- [X] T016 [US3] Modify GetEntityDetailsHandler to construct proxy URLs using HttpContext.Request base URL instead of direct blob URLs for certificate.storageUrl and historyDocuments[].storageUrl in src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs

**Checkpoint**: Entity-details response contains proxy URLs — Angular UI renders them as-is and PDFs open through the API

---

## Phase 6: User Story 4 — Graceful Error Handling for Unavailable Documents (Priority: P2)

**Goal**: When a blob cannot be streamed (missing, unreachable, invalid path, timeout), the API returns clear RFC 9457 Problem Details responses

**Independent Test**: Request proxy URL with non-existent blob path → 404; invalid path → 400; verify Problem Details body structure

### Tests for User Story 4

- [X] T017 [P] [US4] Integration tests for error scenarios — 400 (invalid path), 404 (missing blob), verify RFC 9457 Problem Details response bodies in tests/BizScraper.IntegrationTests/Features/Documents/DocumentErrorHandlingTests.cs

### Implementation for User Story 4

> Error handling is already implemented in StreamDocumentHandler (T011) — the handler maps storage exceptions to RFC 9457 responses. This phase adds integration-level validation that the full pipeline (endpoint → handler → storage → error response) returns correct Problem Details bodies for each error scenario.

- [X] T018 [US4] Verify and refine error response Problem Details type URIs and detail messages for consistency with existing API error patterns in src/BizScraper.Api/Features/Documents/Handlers/StreamDocumentHandler.cs

**Checkpoint**: All error scenarios return correct status codes and RFC 9457 Problem Details bodies

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Caching headers, observability metrics, and final validation

- [X] T019 [P] Add OpenTelemetry Meter with document request counter and duration histogram metrics to StreamDocumentHandler in src/BizScraper.Api/Features/Documents/Handlers/StreamDocumentHandler.cs
- [X] T020 [P] Add Cache-Control and ETag response headers to StreamDocumentHandler for immutable caching (FR-013) in src/BizScraper.Api/Features/Documents/Handlers/StreamDocumentHandler.cs
- [X] T021 Run quickstart.md validation — verify proxy endpoint works end-to-end per specs/011-pdf-proxy-streaming/quickstart.md
- [X] T022 Verify CORS policy allows GET on /api/v1/documents/ from TestUiOrigin in src/BizScraper.Api/Program.cs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational — core proxy endpoint
- **US2 (Phase 4)**: Depends on US1 (same endpoint, different test coverage)
- **US3 (Phase 5)**: Depends on Foundational — can run in parallel with US1/US2 (different files)
- **US4 (Phase 6)**: Depends on US1 (error paths are in the handler)
- **Polish (Phase 7)**: Depends on US1 completion

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — provides the proxy endpoint
- **User Story 2 (P1)**: Depends on US1 endpoint being functional — adds integration test coverage
- **User Story 3 (P1)**: Can start after Foundational (Phase 2) — modifies different files (GetEntityDetailsHandler) than US1 (Documents feature)
- **User Story 4 (P2)**: Depends on US1 — validates error paths in the endpoint

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before handlers
- Handlers before endpoints
- Endpoint registration before integration tests

### Parallel Opportunities

- T002 and T003 can run in parallel (different files)
- T006, T007, T008 can run in parallel (different files)
- T009 and T010 can run in parallel (different test files)
- US1 (Phase 3) and US3 (Phase 5) can run in parallel — they modify different files
- T019 and T020 can run in parallel (additive changes to the same handler but different concerns)

---

## Parallel Example: Setup + Foundational

```bash
# Phase 1 — after T001 folder creation:
Task T002: "Create BlobDownloadResult record"          # runs in parallel
Task T003: "Create StreamDocumentQuery record"         # runs in parallel

# Phase 2 — after T004 interface update:
Task T005: "Implement download in AzureBlobStorageClient"
Task T006: "Implement download in NullBlobStorageClient"  # runs in parallel with T007, T008
Task T007: "Create BlobPathValidator"                     # runs in parallel with T006, T008
Task T008: "Create DocumentLogMessages"                   # runs in parallel with T006, T007
```

## Parallel Example: User Stories

```bash
# After Foundational completes, these can start in parallel:
Stream A (US1): T009 → T010 → T011 → T012 → T013
Stream B (US3): T015 → T016
# Then sequential:
Stream C (US2): T014 (after US1 complete)
Stream D (US4): T017 → T018 (after US1 complete)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (3 tasks)
2. Complete Phase 2: Foundational (5 tasks)
3. Complete Phase 3: User Story 1 (5 tasks)
4. **STOP and VALIDATE**: Test proxy endpoint with curl per quickstart.md
5. Deploy/demo if ready — certificates can be streamed through the API

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test proxy streaming → Deploy (MVP!)
3. Add User Story 3 → Test URL rewriting → Angular UI uses proxy URLs
4. Add User Story 2 → Integration test confirms history docs stream correctly
5. Add User Story 4 → Error handling validated end-to-end
6. Polish → Metrics, caching headers, CORS verification

### Files Modified Summary

| File | Action | Phase |
|------|--------|-------|
| `src/BizScraper.Api/Common/Interfaces/IBlobStorageClient.cs` | MODIFY | Phase 2 (T004) |
| `src/BizScraper.Api/Common/Interfaces/BlobDownloadResult.cs` | NEW | Phase 1 (T002) |
| `src/BizScraper.Api/Infrastructure/Storage/AzureBlobStorageClient.cs` | MODIFY | Phase 2 (T005) |
| `src/BizScraper.Api/Infrastructure/Storage/NullBlobStorageClient.cs` | MODIFY | Phase 2 (T006) |
| `src/BizScraper.Api/Features/Documents/Models/StreamDocumentQuery.cs` | NEW | Phase 1 (T003) |
| `src/BizScraper.Api/Features/Documents/Validation/BlobPathValidator.cs` | NEW | Phase 2 (T007) |
| `src/BizScraper.Api/Features/Documents/Logging/DocumentLogMessages.cs` | NEW | Phase 2 (T008) |
| `src/BizScraper.Api/Features/Documents/Handlers/StreamDocumentHandler.cs` | NEW | Phase 3 (T011) |
| `src/BizScraper.Api/Features/Documents/Endpoints/DocumentEndpoints.cs` | NEW | Phase 3 (T012) |
| `src/BizScraper.Api/Program.cs` | MODIFY | Phase 3 (T013) |
| `src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs` | MODIFY | Phase 5 (T016) |
| `tests/BizScraper.UnitTests/Features/Documents/BlobPathValidatorTests.cs` | NEW | Phase 3 (T009) |
| `tests/BizScraper.UnitTests/Features/Documents/StreamDocumentHandlerTests.cs` | NEW | Phase 3 (T010) |
| `tests/BizScraper.UnitTests/Features/Documents/StorageUrlRewritingTests.cs` | NEW | Phase 5 (T015) |
| `tests/BizScraper.IntegrationTests/Features/Documents/DocumentStreamingTests.cs` | NEW | Phase 4 (T014) |
| `tests/BizScraper.IntegrationTests/Features/Documents/DocumentErrorHandlingTests.cs` | NEW | Phase 6 (T017) |
