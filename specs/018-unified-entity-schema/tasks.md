# Tasks: Unified Entity Schema

**Input**: Design documents from `/specs/018-unified-entity-schema/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US5)
- Exact file paths included in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create unified model records and encoding utility shared by all user stories

- [x] T001 Create UnifiedSearchResult record with STJ source generation attributes in src/BizScraper.Api/Common/Models/UnifiedSearchResult.cs — fields: name, identifier, status, entityType, formationDate, state, event, uniqueKey, standingTax?, standingRA?, registeredOffice? (see data-model.md mapping table). Register with the project's JsonSerializerContext for AOT compatibility.
- [x] T002 [P] Create UniqueKeyEncoder static utility (Encode/Decode) using base64url-encoded JSON with state + params payload in src/BizScraper.Api/Common/UniqueKeyEncoder.cs — state values: US-CO, US-WY, DE-DE; per-state params per research.md §1. Decode MUST validate the state field is a known value (US-CO, US-WY, DE-DE) and return a typed error on failure.
- [x] T003 [P] Create entity detail model records with STJ attributes in src/BizScraper.Api/Common/Models/ — UnifiedEntityDetailResponse.cs (details, registeredAgent?, certificate?, parties?, documents?), DetailSection.cs, AgentSection.cs, CertificateSection.cs, PartyEntry.cs, DocumentEntry.cs, DownloadReference.cs (see data-model.md for all fields). Register all new record types with the project's JsonSerializerContext for AOT compatibility.

**Checkpoint**: All shared types available — mapper implementation can begin

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Mapper infrastructure and v2 route wiring — MUST complete before user story implementation

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create SearchResultMapper class with MapToUnified method that accepts JsonElement output + definition slug, dispatches to per-state mapping based on slug suffix *-business-search in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs — return type: UnifiedSearchResult[]
- [x] T005 [P] Create EntityDetailMapper class with MapToUnified method that accepts JsonElement output + definition slug, dispatches to per-state mapping based on slug suffix *-entity-details in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs — return type: UnifiedEntityDetailResponse
- [x] T006 [P] Register /api/v2 route group with POST /execute-script and GET /definitions endpoints in src/BizScraper.Api/ (Program.cs or endpoint registration file) — wire to existing ExecuteScript and Definitions handlers
- [x] T007 Integrate uniqueKey decode pre-processing into ExecuteScriptHandler: for entity-details definitions (*-entity-details), decode the `uniqueKey` parameter via UniqueKeyEncoder.Decode, validate the decoded state prefix matches the target definition's state (return 400 Bad Request with Problem Details if mismatched), and inject the decoded params into the definition engine as runtime variables replacing the raw uniqueKey string in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [x] T008 Integrate post-processing mapper dispatch into ExecuteScriptHandler: after FlowExecutionResult, classify definition slug (*-business-search → SearchResultMapper, *-entity-details → EntityDetailMapper), set mapped result as ExecuteScriptResponse.Data in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs

**Checkpoint**: v2 endpoints registered, uniqueKey decode and state validation wired, handler pipes through mapper — per-state mapping methods are stubs returning raw data until US1/US2 fill them in

---

## Phase 3: User Story 1 — Unified Search Results Shape (Priority: P1) 🎯 MVP

**Goal**: All search definitions return flat array of UnifiedSearchResult with uniqueKey replacing detailsUrl

**Independent Test**: Execute us-co-business-search, us-wy-business-search, de-de-business-search via POST /api/v2/execute-script and verify identical top-level field names on each result row

### Tests for User Story 1

> **NOTE: Write these tests FIRST — they MUST fail (Red) before implementation begins**

- [x] T009 [P] [US1] Unit tests for SearchResultMapper: test CO, WY, DE mapping produces correct UnifiedSearchResult fields, verifies uniqueKey is generated, verifies detailsUrl is absent, verifies DE nested results are flattened to flat array in tests/BizScraper.UnitTests/
- [x] T010 [P] [US1] Integration test for v2 search endpoint: POST /api/v2/execute-script with each search definition slug returns 200 with data array where every row has the required base fields (name, identifier, status, entityType, formationDate, state, event, uniqueKey) in tests/BizScraper.IntegrationTests/

### Implementation for User Story 1

- [x] T011 [US1] Implement CO search result mapping in SearchResultMapper: read JsonElement properties (Name, Identifier, Status, EntityType, FormationDate, DetailsUrl, State, Event) → UnifiedSearchResult; generate uniqueKey via UniqueKeyEncoder.Encode with state=US-CO and SOS URL params extracted from DetailsUrl in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs
- [x] T012 [US1] Implement WY search result mapping in SearchResultMapper: read JsonElement properties → UnifiedSearchResult; include standingTax, standingRA; generate uniqueKey with state=US-WY and eFNum from RegistrationId in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs
- [x] T013 [US1] Implement DE search result mapping in SearchResultMapper: read JsonElement properties (CompanyName→name, RegistrationId→identifier, RegisteredOffice→registeredOffice, Status→status); set entityType=null, formationDate=null, event=null, state="DE"; generate uniqueKey with state=DE-DE and searchTerm+registrationId; handle DE nested data.results by flattening to flat array in the mapper (normalization is a server concern, not definition JSON) in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs
- [x] T014 [P] [US1] Rename co-business-search.json → us-co-business-search.json and update metadata.definitionSlug to "us-co-business-search" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/
- [x] T015 [P] [US1] Rename wy-business-search.json → us-wy-business-search.json and update metadata.definitionSlug to "us-wy-business-search" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/
- [x] T016 [P] [US1] Rename de-business-search.json → de-de-business-search.json and update metadata.definitionSlug to "de-de-business-search" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/

**Checkpoint**: All three search definitions return consistent UnifiedSearchResult[] with uniqueKey, no detailsUrl — Story 1 independently testable

---

## Phase 4: User Story 2 — Unified Entity Details Shape (Priority: P1)

**Goal**: All entity-details definitions return UnifiedEntityDetailResponse with five nullable sections (details, registeredAgent, certificate, parties, documents)

**Independent Test**: Call us-co-entity-details and us-wy-entity-details via POST /api/v2/execute-script using uniqueKey from respective search results; verify identical top-level sections

### Tests for User Story 2

> **NOTE: Write these tests FIRST — they MUST fail (Red) before implementation begins**

- [x] T017 [P] [US2] Unit tests for EntityDetailMapper: test CO and WY mapping produces correct UnifiedEntityDetailResponse with five sections, null sections where expected, WY documents mapped from HistoryDocumentResult, CO certificate mapped from CertificateResult in tests/BizScraper.UnitTests/
- [x] T018 [P] [US2] Unit tests for UniqueKeyEncoder roundtrip: encode → decode for each state, verify cross-state mismatch detection returns error in tests/BizScraper.UnitTests/
- [x] T019 [P] [US2] Integration test for v2 entity-details endpoint: POST /api/v2/execute-script with entity-details slugs returns 200 with data containing all five top-level sections (details, registeredAgent, certificate, parties, documents) in tests/BizScraper.IntegrationTests/

### Implementation for User Story 2

- [x] T020 [US2] Implement CO entity-details mapping in EntityDetailMapper: map EntityDetailResult → DetailSection (IdNumber→identifier, map address fields), RegisteredAgentResult → AgentSection, CertificateResult → CertificateSection (StorageUrl→downloads[0].proxyUrl); set parties=null, documents=null in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [x] T021 [US2] Implement WY entity-details mapping in EntityDetailMapper: map to DetailSection (with WY-specific fields: subStatus, standingTax, standingRA, standingOther, inactiveDate, termOfDuration, formedIn, latestAnnualReportYear, annualReportExempt, licenseTaxPaid), AgentSection, PartyEntry[]; map HistoryDocumentResult[] → DocumentEntry[] with single DownloadReference per document (label="Filing Document", proxyUrl from StorageUrl, fileName from FileName) in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [x] T022 [P] [US2] Rename co-entity-details.json → us-co-entity-details.json and update metadata.definitionSlug to "us-co-entity-details" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/
- [x] T023 [P] [US2] Rename wy-entity-details.json → us-wy-entity-details.json and update metadata.definitionSlug to "us-wy-entity-details" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/

**Checkpoint**: CO and WY entity details return unified shapes — Story 2 independently testable

---

## Phase 5: User Story 3 — Germany Entity Details with Document Downloads (Priority: P2)

**Goal**: New de-de-entity-details definition replaces standalone hardcopy endpoint; returns entity-details shape with documents containing original + translated PDFs

**Independent Test**: Call de-de-entity-details via POST /api/v2/execute-script with a uniqueKey from a DE search result; verify response has documents with Original and Translated download references

### Tests for User Story 3

> **NOTE: Write these tests FIRST — they MUST fail (Red) before implementation begins**

- [x] T024 [P] [US3] Unit tests for DE entity-details mapping: verify documents section populated with Original + Translated downloads, verify partial success (proxyUrl=null + error) when FAB translation fails, verify registeredAgent/certificate/parties are null in tests/BizScraper.UnitTests/
- [x] T025 [P] [US3] Integration test for DE entity-details: POST /api/v2/execute-script with de-de-entity-details returns unified shape with documents array, verify 502 when scraping fails, verify error response when no AD document is available for entity in tests/BizScraper.IntegrationTests/

### Implementation for User Story 3

- [x] T026 [US3] Create de-de-entity-details.json definition with browser navigation actions: navigate to Handelsregister → enter search term (from decoded uniqueKey params) → locate entity row by registrationId → click AD (Aktuelle Drucke) link → capture PDF download; handle case where no AD link is found by returning a 502 error with detail "No document available for entity" in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-de-entity-details.json
- [x] T027 [US3] Implement DE entity-details post-processing as a dedicated post-processor (following existing GetHardcopyPrintoutHandler pattern): receive raw PDF bytes from definition execution → upload original PDF to Azure Blob → call FAB translation service → convert translated markdown to PDF → upload translated PDF to blob → assemble documents array; handle FAB failure as partial success (proxyUrl=null, error message on translated entry); handle missing AD document as 502 with descriptive error in src/BizScraper.Api/Features/ExecuteScript/Handlers/
- [x] T028 [US3] Add DE entity-details mapping in EntityDetailMapper: populate DetailSection from decoded uniqueKey params (name, identifier, status, registeredOffice); set registeredAgent=null, certificate=null, parties=null; populate documents with DocumentEntry containing two DownloadReference entries (Original + Translated) in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs

**Checkpoint**: DE entity details accessible through execute-script with unified shape — Story 3 independently testable

---

## Phase 6: User Story 4 — Deprecated API Removal (Priority: P2)

**Goal**: Remove all deprecated v1 endpoints; v1 execute-script and definitions return 410 Gone; business-search, entity-details, germany-search return 404 (removed from routing)

**Independent Test**: Send requests to all former v1 endpoints; verify 410 for execute-script/definitions, 404 for business-search/entity-details/germany-search; verify all definitions still work via v2

### Tests for User Story 4

> **NOTE: Write these tests FIRST — they MUST fail (Red) before implementation begins**

- [x] T029 [P] [US4] Integration tests for deprecated endpoints: verify GET /api/v1/business-search returns 404, GET /api/v1/entity-details returns 404, GET /api/v1/germany-search returns 404, POST /api/v1/execute-script returns 410 Gone with Problem Details, GET /api/v1/definitions returns 410 Gone with Problem Details in tests/BizScraper.IntegrationTests/

### Implementation for User Story 4

- [x] T030 [US4] Add v1 410 Gone stub routes for POST /api/v1/execute-script and GET /api/v1/definitions returning RFC 7807 Problem Details with migration guidance to v2 in src/BizScraper.Api/ (Program.cs or endpoint registration file)
- [x] T031 [P] [US4] Delete BusinessSearch feature subfolder contents (Endpoints/, Handlers/, Queries/) — keep Models/ temporarily if needed by other code; remove DI registrations for BusinessSearch handlers in src/BizScraper.Api/Features/BusinessSearch/
- [x] T032 [P] [US4] Delete EntityDetails feature subfolder contents (Endpoints/, Handlers/, Queries/) — keep Models/ temporarily; remove DI registrations for EntityDetails handlers in src/BizScraper.Api/Features/EntityDetails/
- [x] T033 [P] [US4] Delete GermanySearch feature folder entirely (all subfolders and files) and remove all DI registrations for GermanySearch handlers in src/BizScraper.Api/Features/GermanySearch/
- [x] T034 [US4] Remove deprecated endpoint route registrations from application startup, clean up any orphaned using statements, and verify application compiles without deprecated features in src/BizScraper.Api/

**Checkpoint**: API surface reduced to /api/v2/execute-script, /api/v2/definitions, /api/v1/documents/{blobPath} — Story 4 independently testable

---

## Phase 7: User Story 5 — Test UI Updates (Priority: P3)

**Goal**: Test UI consumes unified shapes, uses uniqueKey for entity detail navigation, displays documents section for WY filings and DE hardcopy

**Independent Test**: Open test UI → search each state → verify consistent results grid → click result → verify entity details render with unified sections and document downloads

### Tests for User Story 5

> **NOTE: Write these tests FIRST — they MUST fail (Red) before implementation begins**

- [x] T035 [P] [US5] E2E test for test UI: search each state via UI → verify results grid shows unified columns → click result → verify entity details render with unified sections and document downloads where applicable in tests/BizScraper.EndToEndTests/

### Implementation for User Story 5

- [x] T036 [P] [US5] Create unified TypeScript interfaces matching API contracts (UnifiedSearchResult, UnifiedEntityDetailResponse, DetailSection, DocumentEntry, DownloadReference) in src/test-ui/src/app/models/
- [x] T037 [US5] Update execute-script service to use /api/v2 base URL and type responses with unified interfaces in src/test-ui/src/app/services/execute-script.service.ts
- [x] T038 [US5] Update search results grid component for unified column layout (Name, Identifier, Status, Entity Type, Formation Date, State) consistent across all states in src/test-ui/src/app/components/
- [x] T039 [US5] Update entity detail view component to use uniqueKey (not detailsUrl) for requesting details, render unified detail sections with state-specific fields shown conditionally in src/test-ui/src/app/components/
- [x] T040 [US5] Add documents section to entity detail view component displaying DocumentEntry[] with download links (label + proxyUrl) for WY history filings and DE original/translated PDFs in src/test-ui/src/app/components/
- [x] T041 [US5] Remove germany-search service, germany-specific components, and all references to deprecated /api/v1 endpoints from the test UI codebase in src/test-ui/src/app/

**Checkpoint**: Test UI renders search results, entity details, and document downloads from all states using unified v2 API — Story 5 independently testable

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, spec snapshot, and final validation across all stories

- [x] T042 [P] Update OpenAPI/Swagger annotations for v2 endpoints (request/response schemas, error codes, deprecation notices) in src/BizScraper.Api/
- [x] T043 [P] Update global spec snapshot with unified entity schema feature summary in specs/current-specs/
- [x] T044 [P] Update Helm chart ingress/route configuration in deploy/helm/ to expose /api/v2 routes and remove deprecated v1 route entries (keep /api/v1/documents/{blobPath} unchanged)
- [x] T045 Remove tests for deprecated handlers and clean up any stale test references to old definition slugs or v1 endpoints in tests/BizScraper.UnitTests/, tests/BizScraper.IntegrationTests/, tests/BizScraper.EndToEndTests/
- [x] T046 Run quickstart.md validation — execute all curl examples from specs/018-unified-entity-schema/quickstart.md against running API and verify responses match documented shapes

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (model records must exist) — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — can run in parallel with US2
- **US2 (Phase 4)**: Depends on Phase 2 — can run in parallel with US1
- **US3 (Phase 5)**: Depends on Phase 2 — can run in parallel with US1/US2 (mapper additions are additive)
- **US4 (Phase 6)**: Depends on US1 + US2 + US3 (all v2 functionality must be working before removing v1)
- **US5 (Phase 7)**: Depends on US1 + US2 (v2 endpoints must return unified shapes); US3 optional (DE details)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (Unified Search Results)**: Standalone after Foundational — no dependency on other stories
- **US2 (Unified Entity Details)**: Standalone after Foundational — no dependency on other stories
- **US3 (DE Entity Details)**: Standalone after Foundational — adds DE mapping to EntityDetailMapper
- **US4 (Deprecated Removal)**: MUST wait for US1 + US2 + US3 — all definitions must work via v2 before removing v1
- **US5 (Test UI)**: MUST wait for US1 + US2 — UI needs unified shapes available; US3 enhances but doesn't block

### Within Each User Story

- Tests MUST be written and FAIL before implementation begins (Red phase)
- Mapping methods before definition slug renames (renames can be parallel)
- Core mapping before edge-case handling
- Handler integration before validation

### Parallel Opportunities

- **Phase 1**: T001 ‖ T002 ‖ T003 (all different files)
- **Phase 2**: T004 ‖ T005 ‖ T006 (mapper files + route registration)
- **Phase 3**: T009 ‖ T010 (tests in parallel); T014 ‖ T015 ‖ T016 (definition file renames — after T011–T013 complete)
- **Phase 4**: T017 ‖ T018 ‖ T019 (tests in parallel); T022 ‖ T023 (definition file renames — after T020–T021 complete)
- **Phase 5**: T024 ‖ T025 (tests in parallel); implementation is sequential pipeline
- **Phase 6**: T031 ‖ T032 ‖ T033 (independent feature folder deletions)
- **Phase 7**: T036 can start alongside T037 (models independent of service)
- **Phase 8**: T042 ‖ T043 ‖ T044 (documentation + Helm tasks)

---

## Parallel Example: User Story 1

```text
# Launch tests first (must fail before implementation):
T009: Unit tests for SearchResultMapper (Red)
T010: Integration test for v2 search endpoint (Red)

# After tests fail, launch search mappers sequentially (same file):
T011: CO search result mapping in SearchResultMapper.cs
T012: WY search result mapping in SearchResultMapper.cs
T013: DE search result mapping in SearchResultMapper.cs

# Then launch all definition renames in parallel:
T014: Rename co-business-search.json → us-co-business-search.json
T015: Rename wy-business-search.json → us-wy-business-search.json
T016: Rename de-business-search.json → de-de-business-search.json
```

---

## Parallel Example: User Stories 1 + 2 (both P1)

```text
# After Phase 2, US1 and US2 can proceed in parallel:

Developer A (US1):                    Developer B (US2):
T009: Unit tests (Red)                 T017: Unit tests (Red)
T011: CO search mapping               T020: CO entity-details mapping
T012: WY search mapping               T021: WY entity-details mapping
T013: DE search mapping               T022: Rename co-entity-details.json
T014: Rename co-business-search.json   T023: Rename wy-entity-details.json
T015: Rename wy-business-search.json
T016: Rename de-business-search.json
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (unified models + UniqueKeyEncoder)
2. Complete Phase 2: Foundational (mappers + v2 routes + handler integration)
3. Complete Phase 3: User Story 1 (search result mapping + slug renames)
4. **STOP and VALIDATE**: Execute all three search definitions via v2 and verify unified shapes
5. Deploy/demo if ready — consumers can start migrating search integrations

### Incremental Delivery

1. Setup + Foundational → Core infrastructure ready
2. US1 (Unified Search) → MVP: consistent search results across all states
3. US2 (Unified Details) → Entity details standardized
4. US3 (DE Details) → Germany hardcopy via entity-details pattern
5. US4 (Deprecated Removal) → Clean API surface (v2 only + 410 stubs)
6. US5 (Test UI) → Internal verification tool updated
7. Polish → Documentation, tests, validation

### Key Technical Notes

- **UniqueKey encoding**: Base64url JSON with `state` (US-CO/US-WY/DE-DE) + `params` (state-specific). See research.md §1 for per-state param structure
- **Mapper dispatch**: Definition slug suffix determines mapping: `*-business-search` → SearchResultMapper, `*-entity-details` → EntityDetailMapper
- **Slug rename**: Only metadata.definitionSlug in JSON needs updating; metadata.state stays as CO/WY/DE (engine internal). File rename is cosmetic but recommended
- **DE partial success**: FAB translation failure → original PDF returned normally, translated entry has proxyUrl=null + error message
- **STJ source generation**: All new records in Common/Models/ must be registered with the JsonSerializerContext for AOT compatibility
