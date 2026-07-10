# Tasks: Iowa Business Entity Search Integration

**Input**: Design documents from `/specs/022-iowa-entity-search/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are included because the feature spec defines mandatory acceptance scenarios and measurable success criteria.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare definition assets and test scaffolding for Iowa implementation.

- [X] T001 Create Iowa flow definition files from feature contracts in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json and src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json
- [X] T002 [P] Add Iowa sample payloads for local validation in tests/BizScraper.IntegrationTests/TestData/ExecuteScript/us-ia-business-search.sample.json and tests/BizScraper.IntegrationTests/TestData/ExecuteScript/us-ia-entity-details.sample.json
- [ ] T003 [P] Add Iowa diagnostics quick-run script commands to docs in specs/022-iowa-entity-search/quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core runtime support required before implementing user stories.

**CRITICAL**: User story work starts only after this phase completes.

- [X] T004 Extend expected unique-key state mapping for Iowa in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [X] T005 [P] Add Iowa slug validation coverage in tests/BizScraper.UnitTests/Features/ExecuteScript/FlowDefinitionLoaderSlugTests.cs
- [X] T006 [P] Add Iowa required-parameter validation scenarios (searchTerm search and uniqueKey detail) in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptValidatorTests.cs
- [X] T007 Add Iowa definition discovery assertions to v2 definitions endpoint integration tests in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [ ] T008 Verify Iowa definitions load with startup loader and update startup comments if needed in src/BizScraper.Api/Program.cs
- [ ] T009 Add explicit response-envelope contract integration tests for `results`, `totalCount`, `siteTotalCount`, `truncated`, `pagesScraped`, `searchTerm`, `state`, and `executionTime` in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [ ] T010 Add explicit local TLS-ignore behavior verification for Iowa execution in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs and src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
- [ ] T011 Add diagnostics/tracing artifact verification checkpoints (page load, results, pagination, detail, error) in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs

**Checkpoint**: Foundation complete; user stories can proceed.

---

## Phase 3: User Story 1 - Iowa Business Entity Search via Execute-Script (Priority: P1) 🎯 MVP

**Goal**: Execute Iowa business name searches through `us-ia-business-search` and return normalized search results.

**Independent Test**: POST `/api/v2/execute-script` with `definition=us-ia-business-search` and `parameters.searchTerm` returns normalized Iowa result rows with identifier/name/status/entityType/formationDate/uniqueKey/state.

### Tests for User Story 1

- [X] T012 [P] [US1] Add Iowa search mapper unit tests for normalized result fields in tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/SearchResultMapperTests.cs
- [X] T013 [P] [US1] Add Iowa search integration test for `/api/v2/execute-script` success response envelope in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [ ] T014 [P] [US1] Add Iowa no-results and pagination-truncation mapper tests in tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/SearchResultMapperTests.cs

### Implementation for User Story 1

- [X] T015 [US1] Implement Iowa search mapping branch in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs
- [X] T016 [US1] Implement Iowa uniqueKey encoding (`US-IA`) from drill-down URL in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs
- [ ] T017 [US1] Align Iowa search definition variables/actions with engine schema in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json
- [ ] T018 [US1] Add Iowa-specific no-result/error pattern handling in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json
- [ ] T019 [US1] Validate US1 end-to-end behavior by updating request/response examples in specs/022-iowa-entity-search/quickstart.md

**Checkpoint**: US1 is independently functional and testable (MVP).

---

## Phase 4: User Story 2 - Unified v2 Execute-Script API for IA/CO/WY (Priority: P1)

**Goal**: Ensure Iowa operations work through the same v2 endpoint and response model without state-specific branching by API consumers.

**Independent Test**: Execute CO, WY, and IA v2 definitions sequentially; all responses deserialize through the same envelope and unified output model.

### Tests for User Story 2

- [ ] T020 [P] [US2] Add multi-state v2 endpoint regression test (CO/WY/IA) in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [ ] T021 [P] [US2] Add handler concurrency test for Iowa busy behavior and retry semantics in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerTests.cs

### Implementation for User Story 2

- [X] T022 [US2] Ensure v2 endpoint description/examples include Iowa definitions in src/BizScraper.Api/Features/ExecuteScript/Endpoints/ExecuteScriptEndpoints.cs
- [ ] T023 [US2] Ensure per-definition semaphore behavior remains correct for Iowa slugs in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [X] T024 [US2] Record and validate Iowa script slugs in audit persistence path in src/BizScraper.Api/Infrastructure/Persistence/ScriptExecutionAuditRepository.cs and tests/BizScraper.IntegrationTests/Features/ExecuteScript/ScriptExecutionAuditRepositoryTests.cs
- [X] T025 [US2] Add IA-specific definition listing metadata validation in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs

**Checkpoint**: US2 is independently functional and testable with unified v2 behavior.

---

## Phase 5: User Story 3 - Iowa Entity Details Drill-Down via Execute-Script (Priority: P1)

**Goal**: Use Iowa `uniqueKey` to retrieve full entity details, including names list, agent, principal office, dates, chapter/code, and certificate note.

**Independent Test**: Search Iowa entities, extract one `uniqueKey`, then call `us-ia-entity-details` and verify extended detail fields map into unified detail response.

### Tests for User Story 3

- [X] T026 [P] [US3] Add Iowa detail mapper unit tests for extended fields in tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs
- [ ] T027 [P] [US3] Add Iowa detail integration test using uniqueKey-driven request in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [X] T028 [P] [US3] Add certificate paywall note assertion tests in tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs

### Implementation for User Story 3

- [X] T029 [US3] Implement Iowa detail mapping branch in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [X] T030 [US3] Map Iowa names list and registered agent/principal office fields in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [X] T031 [US3] Map Iowa chapter/code, filing/effective/expiration dates, and modified flag in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [X] T032 [US3] Set certificate paywall note handling for Iowa detail responses in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [X] T033 [US3] Align detail extraction selectors and output compose object in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json

**Checkpoint**: US3 is independently functional and testable.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, cleanup, and readiness checks.

- [X] T034 [US3] Update consolidated snapshot artifacts in specs/current-specs/spec.md, specs/current-specs/data-model.md, and impacted files under specs/current-specs/contracts/
- [ ] T035 [P] Remove dead code or obsolete comments introduced by Iowa integration in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs and src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [ ] T036 [P] Add/refresh XML comments for Iowa-specific mapping behavior in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs and src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [X] T037 Run targeted unit and integration suites for execute-script in tests/BizScraper.UnitTests/Features/ExecuteScript and tests/BizScraper.IntegrationTests/Features/ExecuteScript
- [ ] T038 Validate quickstart request/response examples against implemented behavior in specs/022-iowa-entity-search/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies; starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1; blocks all user stories.
- **Phase 3 (US1)**: Depends on Phase 2; defines MVP.
- **Phase 4 (US2)**: Depends on Phase 2; can proceed in parallel with US1 after foundation, but validate after US1 mapper merge.
- **Phase 5 (US3)**: Depends on Phase 2 and partially on US1 uniqueKey mapping.
- **Phase 6 (Polish)**: Depends on completion of all user stories.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories once foundational tasks complete.
- **US2 (P1)**: Independent after foundational tasks; regression checks include US1 outputs.
- **US3 (P1)**: Requires uniqueKey flow from US1 but otherwise independently testable.

### Within Each User Story

- Tests first (should fail before implementation).
- Mapper and definition updates before endpoint docs and examples.
- Story-level checkpoint validation before moving forward.

---

## Parallel Opportunities

- **Setup**: T002 and T003 can run in parallel after T001 starts.
- **Foundational**: T005 and T006 can run in parallel; T007 can proceed once definitions are present.
- **US1**: T012/T013/T014 can run in parallel; T015/T016 can run before T017/T018 final definition wiring.
- **US2**: T020 and T021 can run in parallel.
- **US3**: T026/T027/T028 can run in parallel; T030/T031 can run in parallel after T029 skeleton.
- **Polish**: T035 and T036 can run in parallel before T037.

### Parallel Example: User Story 1

```bash
# Parallel test creation
T012  # Iowa search mapper unit tests
T013  # Iowa search integration test
T014  # No-results/pagination unit tests

# Parallel implementation after baseline mapping exists
T016  # UniqueKey encoding branch
T017  # Search definition alignment
```

### Parallel Example: User Story 3

```bash
# Parallel detail test tasks
T026  # Extended detail field tests
T027  # uniqueKey detail integration test
T028  # Certificate note assertion tests

# Parallel mapping implementation
T030  # Names + addresses mapping
T031  # Dates + chapter mapping
```

---

## Implementation Strategy

### MVP First (US1)

1. Complete Phase 1 and Phase 2.
2. Deliver Phase 3 (US1) fully.
3. Validate Iowa search independently via `/api/v2/execute-script`.
4. Demo MVP.

### Incremental Delivery

1. Add US1 (search) and validate.
2. Add US2 (unified multi-state v2 guarantees) and validate.
3. Add US3 (detail drill-down) and validate.
4. Finish polish and release checks.

### Team Parallel Strategy

1. Engineer A: US1 mapper + search definition tasks.
2. Engineer B: US2 endpoint/audit regression tasks.
3. Engineer C: US3 detail mapper/definition tasks.

---

## Notes

- Task format follows strict checklist syntax with IDs, optional `[P]`, required `[USx]` labels in story phases, and explicit file paths.
- All user stories are independently testable with dedicated checkpoints.
- No `.specify/extensions.yml` hooks were found, so no pre/post hook execution is required.
