# Tasks: Generic Script Execution Endpoint

**Input**: Design documents from `/specs/016-generic-script-execution/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/api-contract.md, quickstart.md

**Tests**: Included per constitution Principle I (Test-First, NON-NEGOTIABLE). Tests MUST be written and fail (Red) before implementation (Green).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Exact file paths included in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Database migration and definition slug additions needed by all phases

- [X] T001 Create Liquibase changeset for ScriptExecutionAuditEntries table in deploy/liquibase/changelog/ddl/2.0.2603.6-create-script-execution-audit-table.sql
- [X] T002 Register new changeset in deploy/liquibase/masterfiles/master-1.0.2603.0.yaml
- [X] T003 [P] Add `definitionSlug` field to metadata in src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-business-search.json
- [X] T004 [P] Add `definitionSlug` field to metadata in src/BizScraper.Api/Infrastructure/Scraping/Definitions/co-entity-details.json
- [X] T005 [P] Add `definitionSlug` field to metadata in src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json
- [X] T006 [P] Add `definitionSlug` field to metadata in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-business-search.json
- [X] T007 [P] Add `definitionSlug` field to metadata in src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core model changes, loader extension, and shared infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T008 Add `DefinitionSlug` property to `FlowMetadata` record in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs
- [X] T009 Extend `FlowDefinitionLoader` with slug-based dictionary index and `GetDefinitionBySlug(string slug)` method; add startup uniqueness validation for slugs in src/BizScraper.Api/Infrastructure/Scraping/Engine/FlowDefinitionLoader.cs
- [X] T010 [P] Create `ExecuteScriptRequest` model (`Definition`, `Parameters`) in src/BizScraper.Api/Features/ExecuteScript/Models/ExecuteScriptRequest.cs
- [X] T011 [P] Create `ExecuteScriptResponse` model (`Definition`, `CorrelationId`, `Truncated`, `Data`) in src/BizScraper.Api/Features/ExecuteScript/Models/ExecuteScriptResponse.cs
- [X] T012 [P] Create `DefinitionInfo` and `ParameterInfo` models for discovery response in src/BizScraper.Api/Features/ExecuteScript/Models/DefinitionInfo.cs
- [X] T013 [P] Create `UnknownDefinitionException` in src/BizScraper.Api/Features/ExecuteScript/Exceptions/UnknownDefinitionException.cs
- [X] T014 [P] Create `MissingParametersException` in src/BizScraper.Api/Features/ExecuteScript/Exceptions/MissingParametersException.cs
- [X] T015 Register `ExecuteScriptRequest`, `ExecuteScriptResponse`, `DefinitionInfo`, and `ParameterInfo` in source-generated JSON context in src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2JsonContext.cs
- [X] T016 Update `GlobalExceptionHandler` to map `UnknownDefinitionException` and `MissingParametersException` to Problem Details responses; verify existing exception types (`CheckTextMatchException` → 422, `InvalidOperationException` wrapping scraping errors → 502) propagate correctly through the generic endpoint flow in src/BizScraper.Api/Middleware/GlobalExceptionHandler.cs

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Execute Any Definition via a Single Endpoint (Priority: P1) 🎯 MVP

**Goal**: A single `POST /api/v1/execute-script` endpoint resolves a definition slug, validates parameters, executes the scraping flow, and returns results in a uniform envelope.

**Independent Test**: `POST /api/v1/execute-script` with `{"definition": "co-business-search", "parameters": {"searchTerm": "Wendys"}}` returns the uniform response envelope with search results in `data`.

### Tests for User Story 1 (Red phase — MUST fail before implementation)

- [X] T017 [P] [US1] Write `ExecuteScriptValidatorTests` — test parameter validation (missing params, extra params, valid params) in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptValidatorTests.cs
- [X] T018 [P] [US1] Write `ExecuteScriptHandlerTests` — test definition lookup, concurrency limiting, response envelope construction, audit recording in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerTests.cs
- [X] T019 [P] [US1] Write `FlowDefinitionLoaderSlugTests` — test slug index construction, `GetDefinitionBySlug()`, duplicate slug rejection at startup in tests/BizScraper.UnitTests/Features/ExecuteScript/FlowDefinitionLoaderSlugTests.cs
- [X] T020 [P] [US1] Write `ExecuteScriptEndpointTests` — integration tests for POST /api/v1/execute-script via WebApplicationFactory (200, 400, 502 scenarios) in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ExecuteScriptEndpointTests.cs
- [X] T021 [P] [US1] Write `ScriptExecutionAuditRepositoryTests` — integration tests for audit persistence via Testcontainers in tests/BizScraper.IntegrationTests/Features/ExecuteScript/ScriptExecutionAuditRepositoryTests.cs

### Implementation for User Story 1

- [X] T022 [P] [US1] Create `ExecuteScriptValidator` with parameter validation against definition runtime variables in src/BizScraper.Api/Features/ExecuteScript/Validation/ExecuteScriptValidator.cs
- [X] T023 [P] [US1] Create `ExecuteScriptLogging` with [LoggerMessage] source-generated log methods in src/BizScraper.Api/Features/ExecuteScript/Logging/ExecuteScriptLogging.cs
- [X] T024 [P] [US1] Create `ExecuteScriptMetrics` with OpenTelemetry Meter, Counter, Histogram, and UpDownCounter following existing pattern in src/BizScraper.Api/Features/ExecuteScript/Metrics/ExecuteScriptMetrics.cs
- [X] T025 [US1] Create `GenericScriptScraper` for definition-agnostic browser management and flow execution in src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs
- [X] T026 [US1] Create `ScriptExecutionAuditRepository` for persisting audit entries in src/BizScraper.Api/Infrastructure/Persistence/ScriptExecutionAuditRepository.cs
- [X] T027 [US1] Create `ExecuteScriptCommand` LiteBus command in src/BizScraper.Api/Features/ExecuteScript/Commands/ExecuteScriptCommand.cs
- [X] T028 [US1] Create `ExecuteScriptHandler` orchestrating definition lookup, validation, concurrency limiting, execution, and audit recording in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [X] T029 [US1] Create `ExecuteScriptEndpoints` mapping `POST /api/v1/execute-script` with `X-Correlation-Id` response header in src/BizScraper.Api/Features/ExecuteScript/Endpoints/ExecuteScriptEndpoints.cs
- [X] T030 [US1] Add concurrency limit configuration section `ExecuteScript:ConcurrencyLimits` to src/BizScraper.Api/appsettings.json
- [X] T031 [US1] Register `GenericScriptScraper`, `ScriptExecutionAuditRepository`, `ExecuteScriptValidator`, concurrency `ConcurrentDictionary<string, SemaphoreSlim>`, and map endpoints in src/BizScraper.Api/Program.cs

**Checkpoint**: `POST /api/v1/execute-script` is fully functional for all 5 existing definitions — independently testable

---

## Phase 4: User Story 2 — Input Validation and Clear Error Reporting (Priority: P2)

**Goal**: Invalid requests (unknown definition, missing parameters, missing `definition` field) return structured RFC 9457 Problem Details error responses.

**Independent Test**: Send `POST /api/v1/execute-script` with `{"definition": "unknown"}` → 400; with `{"definition": "co-business-search", "parameters": {}}` → 400 listing missing `searchTerm`; with `{}` → 400 requiring `definition`.

> **Note**: Core validation is implemented in Phase 3 (T022, T016). This phase adds edge-case handling and concurrency error responses.

### Tests for User Story 2 (Red phase — MUST fail before implementation)

- [X] T032 [P] [US2] Extend `ExecuteScriptHandlerTests` with 503 Service Busy and 504 Timeout test cases in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptHandlerTests.cs
- [X] T033 [P] [US2] Extend `ExecuteScriptValidatorTests` with extra-parameter-ignored test case in tests/BizScraper.UnitTests/Features/ExecuteScript/ExecuteScriptValidatorTests.cs

### Implementation for User Story 2

- [X] T034 [US2] Add 503 Service Busy response with `Retry-After` header when concurrency semaphore cannot be acquired — update `ExecuteScriptHandler` and `GlobalExceptionHandler` in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs and src/BizScraper.Api/Middleware/GlobalExceptionHandler.cs
- [X] T035 [US2] Add 504 Gateway Timeout handling when flow execution exceeds configured timeout — update `ExecuteScriptHandler` in src/BizScraper.Api/Features/ExecuteScript/Handlers/ExecuteScriptHandler.cs
- [X] T036 [US2] Ensure extra unrecognized parameters in request body are silently ignored (no error) in `ExecuteScriptValidator` in src/BizScraper.Api/Features/ExecuteScript/Validation/ExecuteScriptValidator.cs

**Checkpoint**: All error scenarios from the API contract return correct Problem Details responses

---

## Phase 5: User Story 3 — Automatic Discovery of New Definitions (Priority: P3)

**Goal**: A developer adds a new JSON definition file with a `definitionSlug`, restarts the app, and the new definition is automatically available via `POST /api/v1/execute-script` — zero code changes.

**Independent Test**: Add a dummy definition JSON with `definitionSlug: "test-auto-discovery"`, restart, and verify execution works.

> **Note**: The core auto-discovery mechanism already exists in `FlowDefinitionLoader` (which scans the definitions directory at startup). Adding `definitionSlug` (Phase 1 T003-T007) and the slug index (Phase 2 T009) completes this story. This phase validates the behavior.

### Tests for User Story 3 (Red phase — MUST fail before implementation)

- [X] T037 [US3] Write duplicate-slug startup rejection test in tests/BizScraper.UnitTests/Features/ExecuteScript/FlowDefinitionLoaderSlugTests.cs

### Implementation for User Story 3

- [X] T038 [US3] Verify startup slug-uniqueness validation in `FlowDefinitionLoader` throws and logs a clear error when duplicate `definitionSlug` values are detected in src/BizScraper.Api/Infrastructure/Scraping/Engine/FlowDefinitionLoader.cs

**Checkpoint**: New definitions auto-discovered at startup with duplicate detection — no code changes needed for new definitions

---

## Phase 6: User Story 4 — List Available Definitions (Priority: P3)

**Goal**: A `GET /api/v1/definitions` endpoint returns all loaded definitions with their slugs, names, states, and required parameters.

**Independent Test**: `GET /api/v1/definitions` returns a JSON array with 5 entries matching the loaded definition files.

### Tests for User Story 4 (Red phase — MUST fail before implementation)

- [X] T039 [US4] Write `DefinitionsEndpointTests` — integration test verifying `GET /api/v1/definitions` returns all 5 definitions with correct slugs, names, states, and required parameters via WebApplicationFactory in tests/BizScraper.IntegrationTests/Features/ExecuteScript/DefinitionsEndpointTests.cs

### Implementation for User Story 4

- [X] T040 [US4] Add `GET /api/v1/definitions` route to `ExecuteScriptEndpoints` that queries `FlowDefinitionLoader` and maps to `DefinitionInfo[]` in src/BizScraper.Api/Features/ExecuteScript/Endpoints/ExecuteScriptEndpoints.cs

**Checkpoint**: Discovery endpoint returns all 5 definitions with required parameters

---

## Phase 7: Deprecation & Test UI Migration

**Purpose**: Mark existing endpoints deprecated in Swagger and migrate the test UI to use the new generic endpoint

### Deprecation

- [X] T041 [P] Mark `GET /api/v1/business-search` as deprecated via `.WithOpenApi(op => { op.Deprecated = true; return op; })` in src/BizScraper.Api/Features/BusinessSearch/Endpoints/BusinessSearchEndpoints.cs
- [X] T042 [P] Mark `GET /api/v1/entity-details` as deprecated via `.WithOpenApi(op => { op.Deprecated = true; return op; })` in src/BizScraper.Api/Features/EntityDetails/Endpoints/EntityDetailsEndpoints.cs
- [X] T043 [P] Mark `GET /api/v1/germany-search` (GET only, not POST hardcopy) as deprecated via `.WithOpenApi(op => { op.Deprecated = true; return op; })` in src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs

### Test UI Migration

- [X] T044 [P] Create `ExecuteScriptRequest` and `ExecuteScriptResponse` TypeScript models in src/test-ui/src/app/shared/models/execute-script.model.ts
- [X] T045 [P] Create `DefinitionInfo` TypeScript model in src/test-ui/src/app/shared/models/definition-info.model.ts
- [X] T046 Create `ExecuteScriptService` Angular service calling `POST /api/v1/execute-script` in src/test-ui/src/app/core/services/execute-script.service.ts
- [X] T047 Update search component to use `ExecuteScriptService` instead of `SearchService` in src/test-ui/src/app/features/search/search.component.ts
- [X] T048 Update entity-details component to use `ExecuteScriptService` instead of `EntityDetailsService` in src/test-ui/src/app/features/entity-details/entity-details.component.ts
- [X] T049 Update germany-search component to use `ExecuteScriptService` instead of `GermanySearchService` in src/test-ui/src/app/features/germany-search/germany-search.component.ts

**Checkpoint**: All 3 existing endpoints show deprecated in Swagger; test UI exercises the generic endpoint for all operations

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and cleanup

- [X] T050 Run quickstart.md curl examples against the running API and verify all responses match expected output
- [X] T051 Verify OpenAPI/Swagger spec shows the new endpoints and deprecated markers correctly

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 (T008 needs slug field in definitions; T009 needs T008)
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion — tests first (T017–T021), then implementation (T022–T031)
- **User Story 2 (Phase 4)**: Depends on Phase 3 (builds on handler and exception handler from US1)
- **User Story 3 (Phase 5)**: Depends on Phase 2 (slug index in loader)
- **User Story 4 (Phase 6)**: Depends on Phase 2 (DefinitionInfo model, loader slug access)
- **Deprecation & Test UI (Phase 7)**: Depends on Phase 3 (generic endpoint must be functional first)
- **Polish (Phase 8)**: Depends on all prior phases

### User Story Dependencies

- **US1 (P1)**: Depends on Foundational only — no other story dependencies
- **US2 (P2)**: Depends on US1 (extends the handler and exception handler)
- **US3 (P3)**: Depends on Foundational only — independent of US1/US2
- **US4 (P3)**: Depends on Foundational only — independent of US1/US2/US3

### Within Each User Story

- **Tests FIRST** (Red phase) — write and verify they fail
- Models / shared types before services
- Services before endpoints
- Core implementation before integration
- **Tests pass** (Green phase) — verify all tests now pass
- Story complete before moving to next priority

### Parallel Opportunities

**Phase 1** — All definition updates (T003–T007) can run in parallel; T001–T002 are sequential

**Phase 2** — Models (T010–T012) and exceptions (T013–T014) can all run in parallel after T008–T009 complete; T015 depends on T010–T012; T016 depends on T013–T014

**Phase 3** — Tests (T017–T021) can all run in parallel; then implementation: validator/logging/metrics (T022–T024) in parallel; scraper+audit (T025–T026) in parallel; T028 depends on all prior US1 tasks; T029 depends on T028

**Phase 5 & Phase 6** — US3 (T037–T038) and US4 (T039–T040) can run in parallel with each other once Foundational completes

**Phase 7** — All deprecation tasks (T041–T043) can run in parallel; UI models (T044–T045) can run in parallel; UI component updates (T047–T049) can run in parallel after T046

---

## Parallel Example: User Story 1

```bash
# FIRST: Launch all tests in parallel (Red phase — must fail):
Task T017: Write ExecuteScriptValidatorTests
Task T018: Write ExecuteScriptHandlerTests
Task T019: Write FlowDefinitionLoaderSlugTests
Task T020: Write ExecuteScriptEndpointTests (integration)
Task T021: Write ScriptExecutionAuditRepositoryTests (integration)

# THEN: Launch validator, logging, and metrics in parallel (Green):
Task T022: Create ExecuteScriptValidator
Task T023: Create ExecuteScriptLogging
Task T024: Create ExecuteScriptMetrics

# Then launch scraper and audit repo in parallel:
Task T025: Create GenericScriptScraper
Task T026: Create ScriptExecutionAuditRepository

# Then sequential: Command → Handler → Endpoints → Config → Registration
Task T027 → T028 → T029 → T030 → T031
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (Liquibase DDL + slug fields)
2. Complete Phase 2: Foundational (models, loader, exceptions, JSON context, exception handler)
3. Write Phase 3 tests (T017–T021) — verify they fail (Red)
4. Complete Phase 3 implementation (T022–T031) — verify tests pass (Green)
5. **STOP and VALIDATE**: Test `POST /api/v1/execute-script` with all 5 definitions
6. Deploy/demo if ready — the generic endpoint is fully functional

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test all 5 definitions → Deploy/Demo (**MVP!**)
3. Add User Story 2 → Test error scenarios → Deploy/Demo
4. Add User Stories 3 & 4 (parallel) → Test auto-discovery and listing → Deploy/Demo
5. Add Deprecation & Test UI Migration → Full feature complete
6. Polish → Production-ready

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (Phase 3) — critical path
   - Developer B: User Stories 3 & 4 (Phases 5–6) — independent
3. After US1 is complete:
   - Developer A: User Story 2 (Phase 4)
   - Developer B: Deprecation & Test UI (Phase 7)
4. All: Polish (Phase 8)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- `POST /germany-search/current-hardcopy-printout` and `GET /documents/{blobPath}` are NOT deprecated — they stream binary PDF responses
- `correlationId` is system-generated from `HttpContext.TraceIdentifier` — not a consumer-supplied parameter
- Config-source variables (e.g., `searchEntryUrl`, `maxPages`) come from `PlaywrightOptions`, not from the request parameters
