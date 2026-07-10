# Tasks: Test-UI And Spec Consistency Alignment

**Input**: Design documents from `/specs/023-test-ui-spec-alignment/`  
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Tests are included because the project constitution requires test-first delivery and the spec defines independent, measurable verification criteria.

**Organization**: Tasks are grouped by user story for independent implementation and validation.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare baseline artifacts and ensure canonical contract references exist in feature workspace.

- [ ] T001 Create task baseline and feature artifact index in specs/023-test-ui-spec-alignment/tasks.md
- [ ] T002 [P] Validate canonical request/envelope contract files exist in specs/023-test-ui-spec-alignment/contracts/execute-script-canonical-request.schema.json and specs/023-test-ui-spec-alignment/contracts/execute-script-canonical-envelope.schema.json
- [ ] T003 [P] Add implementation tracking notes for test-ui alignment in specs/023-test-ui-spec-alignment/quickstart.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish canonical execute-script contract and runtime path consistency before user-story work.

**CRITICAL**: No user story work begins until this phase is complete.

- [ ] T004 Align API contract source of truth for request fields in src/BizScraper.Api/Features/ExecuteScript/Models/ExecuteScriptRequest.cs
- [X] T005 [P] Add validator tests for required runtime keys (`searchTerm`, `uniqueKey`) in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptValidatorTests.cs
- [X] T006 [P] Add slug-resolution tests for Iowa detail/search definitions in tests/BizScraper.UnitTests/Features/ExecuteScript/FlowDefinitionLoaderSlugTests.cs
- [ ] T007 Correct plan/task path references to runtime definition location in specs/023-test-ui-spec-alignment/plan.md and specs/023-test-ui-spec-alignment/tasks.md
- [X] T008 Add integration guard for definitions listing coverage (`us-ia-business-search`, `us-ia-entity-details`) in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs

**Checkpoint**: Foundational contract/path consistency completed.

---

## Phase 3: User Story 1 - Canonical Execute-Script Contract (Priority: P1)

**Goal**: Enforce one canonical execute-script request/response contract across IA/CO/WY with no parameter-name ambiguity.

**Independent Test**: Send canonical requests to `/api/v2/execute-script` and verify one shared envelope/model can parse IA/CO/WY responses.

### Tests for User Story 1

- [X] T009 [P] [US1] Add search contract mapper tests for canonical Iowa row shape in tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/SearchResultMapperTests.cs
- [X] T010 [P] [US1] Add detail contract mapper tests for canonical Iowa detail shape in tests/BizScraper.UnitTests/Features/ExecuteScript/Mappers/EntityDetailMapperTests.cs
- [ ] T011 [P] [US1] Add cross-state envelope compatibility integration test in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [ ] T012 [P] [US1] Add unknown/legacy parameter rejection test coverage in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptValidatorTests.cs

### Implementation for User Story 1

- [ ] T013 [US1] Standardize request handling around `definition` + `parameters` in src/BizScraper.Api/Features/ExecuteScript/Endpoints/ExecuteScriptEndpoints.cs
- [X] T014 [US1] Normalize Iowa search mapping and uniqueKey semantics in src/BizScraper.Api/Features/ExecuteScript/Mappers/SearchResultMapper.cs
- [X] T015 [US1] Normalize Iowa detail mapping semantics and canonical fields in src/BizScraper.Api/Features/ExecuteScript/Mappers/EntityDetailMapper.cs
- [X] T016 [US1] Update Iowa definition contracts for canonical runtime fields in src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json and src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-entity-details.json
- [ ] T017 [US1] Ensure local TLS-ignore behavior remains explicit for Iowa execution in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
- [ ] T018 [US1] Add envelope metadata contract assertions (`results`, `totalCount`, `siteTotalCount`, `pagesScraped`, `searchTerm`, `state`, `executionTime`) in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [ ] T040 [US1] Add explicit IA concurrency integration test validating 503 and `Retry-After` semantics in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [X] T041 [US1] Add explicit IA audit persistence tests for `us-ia-business-search` and `us-ia-entity-details` definition slugs in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ScriptExecutionAuditRepositoryTests.cs
- [ ] T042 [US1] Add diagnostics checkpoint verification tests (page load, results, pagination, detail, error) in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs

**Checkpoint**: Canonical API contract is independently valid and testable.

---

## Phase 4: User Story 2 - Test-UI Alignment With Current Spec (Priority: P1)

**Goal**: Update test-ui to send canonical payloads and render canonical result/error semantics.

**Independent Test**: Use test-ui to run IA search and detail drill-down without manual payload edits and verify canonical rendering.

### Tests for User Story 2

- [ ] T019 [P] [US2] Add execute-script service contract tests for canonical payload fields in src/test-ui/src/app/core/services/execute-script.service.spec.ts
- [X] T020 [P] [US2] Add search feature UI tests for canonical request + result rendering in src/test-ui/src/app/features/search/search.component.spec.ts
- [X] T021 [P] [US2] Add entity-details UI tests for uniqueKey flow and certificate note rendering in src/test-ui/src/app/features/entity-details/entity-details.component.spec.ts
- [X] T022 [P] [US2] Add UI error-state tests for 400/503 and Retry-After guidance in src/test-ui/src/app/features/search/search.component.spec.ts

### Implementation for User Story 2

- [ ] T023 [US2] Update test-ui request model to canonical contract in src/test-ui/src/app/shared/models/execute-script.model.ts
- [ ] T024 [US2] Update execute-script API service payload construction in src/test-ui/src/app/core/services/execute-script.service.ts
- [X] T025 [US2] Update search screen request and response handling in src/test-ui/src/app/features/search/search.component.ts
- [X] T026 [US2] Update details screen to call `us-ia-entity-details` using `uniqueKey` in src/test-ui/src/app/features/entity-details/entity-details.component.ts
- [ ] T027 [US2] Update reusable error banner messaging for validation/busy responses in src/test-ui/src/app/shared/components/error-banner/error-banner.component.ts
- [ ] T028 [US2] Update test-ui route-level wiring if needed for canonical flow in src/test-ui/src/app/app.routes.ts

**Checkpoint**: test-ui is independently aligned with canonical spec behavior.

---

## Phase 5: User Story 3 - Governance And Snapshot Consistency (Priority: P2)

**Goal**: Ensure constitutional compliance and remove critical/high/medium specification drift by updating consolidated snapshots.

**Independent Test**: Verify `specs/current-specs/` reflects authoritative post-change spec, data model, and contracts.

### Tests for User Story 3

- [ ] T029 [P] [US3] Add documentation validation checklist for snapshot update completeness in specs/023-test-ui-spec-alignment/checklists/requirements.md
- [ ] T030 [P] [US3] Add automated or scripted assertions for current-specs file presence and basic schema validity in tools/ci/ (new check script)

### Implementation for User Story 3

- [X] T031 [US3] Merge canonical behavior updates into specs/current-specs/spec.md
- [X] T032 [US3] Merge canonical model updates into specs/current-specs/data-model.md
- [X] T033 [US3] Update impacted canonical contracts in specs/current-specs/contracts/
- [ ] T034 [US3] Reconcile naming and path consistency across feature docs in specs/023-test-ui-spec-alignment/spec.md, specs/023-test-ui-spec-alignment/plan.md, and specs/023-test-ui-spec-alignment/quickstart.md

**Checkpoint**: Governance and snapshot consistency are independently complete.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final hardening, regression safety, and release readiness.

- [X] T035 [P] Run API execute-script unit and integration test suites in tests/BizScraper.UnitTests/Features/ExecuteScript/ and tests/BizScraper.IntegrationTests/Features/ExecuteScript/
- [X] T036 [P] Run test-ui unit tests and static checks in src/test-ui/
- [ ] T037 Validate end-to-end quickstart steps and examples in specs/023-test-ui-spec-alignment/quickstart.md
- [ ] T038 Perform dead-code and stale-reference cleanup in src/BizScraper.Api/Features/ExecuteScript/ and src/test-ui/src/app/
- [ ] T039 Finalize feature documentation cross-links and acceptance evidence in specs/023-test-ui-spec-alignment/research.md and specs/023-test-ui-spec-alignment/quickstart.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: Starts immediately.
- **Phase 2 (Foundational)**: Depends on Phase 1 and blocks all stories.
- **Phase 3 (US1)**: Depends on Phase 2.
- **Phase 4 (US2)**: Depends on Phase 2 and consumes canonical contract from US1.
- **Phase 5 (US3)**: Depends on Phase 2 and outputs from US1/US2.
- **Phase 6 (Polish)**: Depends on all story phases.

### User Story Dependencies

- **US1**: Independent after foundational setup.
- **US2**: Independent after foundational setup; should consume US1 canonical contract definitions.
- **US3**: Depends on finalized canonical behavior from US1/US2 for snapshot consolidation.

### Within Each User Story

- Tests are written first and should fail before implementation.
- API contract/model adjustments precede UI adaptation.
- Snapshot updates occur only after canonical behavior is settled.

---

## Parallel Opportunities

- Setup: T002 and T003 in parallel.
- Foundational: T005 and T006 in parallel.
- US1 tests: T009 through T012 and T040 through T042 in parallel.
- US2 tests: T019 through T022 in parallel.
- US3 governance checks: T029 and T030 in parallel.
- Polish: T035 and T036 in parallel.

### Parallel Example: User Story 1

```bash
T009  # Search mapper contract tests
T010  # Detail mapper contract tests
T011  # Cross-state envelope integration test
T012  # Validator regression tests
```

### Parallel Example: User Story 2

```bash
T019  # Execute-script service tests
T020  # Search UI tests
T021  # Detail UI tests
T022  # Error-state UI tests
```

---

## Implementation Strategy

### MVP First (US1)

1. Finish Phase 1 and Phase 2.
2. Deliver Phase 3 (US1).
3. Validate canonical API contract behavior before UI updates.

### Incremental Delivery

1. Canonical API contract normalization (US1).
2. test-ui alignment to canonical contract (US2).
3. governance and current-specs consolidation (US3).
4. polish and release validation.

### Parallel Team Strategy

1. Engineer A: API contract and mapper work (US1).
2. Engineer B: test-ui service/components (US2).
3. Engineer C: governance/snapshot updates and CI checks (US3).

---

## Notes

- Task format follows required checklist format with IDs, optional `[P]`, and `[USx]` labels in user-story phases.
- File paths are explicit for direct implementation execution.
- No `.specify/extensions.yml` hooks were detected for post-task generation actions.
