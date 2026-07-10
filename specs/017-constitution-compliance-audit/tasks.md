# Tasks: Constitution Compliance Audit

**Input**: Design documents from `specs/017-constitution-compliance-audit/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3, US4)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Enable documentation infrastructure and create CI validation scripts (Test-First: scripts fail first, then artifacts make them pass)

- [X] T001 Enable GenerateDocumentationFile with CS1591 suppression in Directory.Build.props
- [X] T002 [P] Create CI validation script for XML doc coverage in tools/ci/check-xml-doc-coverage.ps1
- [X] T003 [P] Create CI validation script for Liquibase comments in tools/ci/check-liquibase-comments.ps1
- [X] T004 [P] Create CI validation script for global snapshot in tools/ci/check-global-snapshot.ps1
- [X] T005 Run all 3 CI validation scripts — confirm they all FAIL (Red phase)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: None required. This feature has no shared infrastructure dependencies beyond Phase 1 setup. All user stories can begin after Phase 1 completes.

**⚠️ NOTE**: Phase 1 completion = foundational readiness. User story phases can begin immediately.

---

## Phase 3: User Story 1 - Global Specification Snapshot (Priority: P1) 🎯 MVP

**Goal**: Create `specs/current-specs/` with capability-based consolidated spec, unified data model, and API contracts

**Independent Test**: Run `tools/ci/check-global-snapshot.ps1` — passes (Green)

### Implementation for User Story 1

- [X] T006 [US1] Create consolidated spec organized by capability in specs/current-specs/spec.md
- [X] T007 [P] [US1] Create unified data model catalog in specs/current-specs/data-model.md
- [X] T008 [P] [US1] Create execute-script endpoint contract in specs/current-specs/contracts/execute-script.md
- [X] T009 [P] [US1] Create definitions listing contract in specs/current-specs/contracts/definitions.md
- [X] T010 [P] [US1] Create document proxy contract in specs/current-specs/contracts/document-proxy.md
- [X] T011 [P] [US1] Create Germany hardcopy printout contract in specs/current-specs/contracts/germany-hardcopy.md
- [X] T012 [P] [US1] Create health check contract in specs/current-specs/contracts/health-check.md
- [X] T013 [P] [US1] Create deprecated business-search contract with [DEPRECATED] marker in specs/current-specs/contracts/deprecated-business-search.md
- [X] T014 [P] [US1] Create deprecated entity-details contract with [DEPRECATED] marker in specs/current-specs/contracts/deprecated-entity-details.md
- [X] T015 [P] [US1] Create deprecated germany-search contract with [DEPRECATED] marker in specs/current-specs/contracts/deprecated-germany-search.md (covers both `GET /api/v1/germany-search` and `POST /api/v1/germany-search/current-hardcopy-printout`)
- [X] T016 [US1] Run tools/ci/check-global-snapshot.ps1 — confirm it passes (Green)

**Checkpoint**: `specs/current-specs/` exists with all required files. A new developer can understand the full API surface from this folder alone.

---

## Phase 4: User Story 2 - Code Documentation for Public Types (Priority: P2)

**Goal**: Add XML `<summary>` doc-comments to all 118 undocumented public C# types across src/BizScraper.Api/

**Independent Test**: Run `tools/ci/check-xml-doc-coverage.ps1` — passes (Green). Build with zero CS1591 warnings.

### Implementation for User Story 2

- [X] T017 [P] [US2] Add XML doc-comments to 21 public types in src/BizScraper.Api/Infrastructure/Scraping/ (engine models, action handlers, flow types)
- [X] T018 [P] [US2] Add XML doc-comments to 18 public types in src/BizScraper.Api/Features/BusinessSearch/ (BusinessSearchEndpoints, BusinessSearchMetrics, exceptions, handler, models, queries)
- [X] T019 [P] [US2] Add XML doc-comments to 15 public types in src/BizScraper.Api/Features/GermanySearch/ (GermanySearchEndpoints, GermanySearchMetrics, exceptions, handlers, models, queries)
- [X] T020 [P] [US2] Add XML doc-comments to 12 public types in src/BizScraper.Api/Features/EntityDetails/ (EntityDetailsEndpoints, EntityDetailsMetrics, exception, models, queries)
- [X] T021 [P] [US2] Add XML doc-comments to 7 public types in src/BizScraper.Api/Common/Configuration/ (options classes, TracingMode enum)
- [X] T022 [P] [US2] Add XML doc-comments to 7 remaining undocumented types in src/BizScraper.Api/Common/Interfaces/ (IBlobStorageClient, ICaptchaSolver, IDocumentTranslator, IEntityDetailScraper, IHardcopyDocumentDownloader, DocumentTranslationResult, HardcopyDownloadResult)
- [X] T023 [P] [US2] Add XML doc-comments to 9 public types in src/BizScraper.Api/Features/ExecuteScript/ (ExecuteScriptEndpoints, ExecuteScriptMetrics, command, exceptions, models)
- [X] T024 [P] [US2] Add XML doc-comments to 5 public types in src/BizScraper.Api/Infrastructure/CaptchaSolving/ (FabCaptchaSolver, request/response models)
- [X] T025 [P] [US2] Add XML doc-comments to 5 public types in src/BizScraper.Api/Infrastructure/DocumentTranslation/ (FabDocumentTranslator, request/response models)
- [X] T026 [P] [US2] Add XML doc-comments to 3 public types in src/BizScraper.Api/Domain/Entities/ (SearchAuditEntry, EntityDetailAuditEntry, ScriptExecutionAuditEntry)
- [X] T027 [P] [US2] Add XML doc-comments to 3 public types in src/BizScraper.Api/Infrastructure/Persistence/ (AuditTrailRepository, EntityDetailAuditRepository, ScriptExecutionAuditRepository)
- [X] T028 [P] [US2] Add XML doc-comments to 4 public types in src/BizScraper.Api/Features/Documents/ (DocumentEndpoints, DocumentMetrics, StreamDocumentHandler, StreamDocumentQuery)
- [X] T029 [P] [US2] Add XML doc-comments to 2 public types in src/BizScraper.Api/Infrastructure/Pdf/ (IMarkdownToPdfConverter, MarkdownToPdfConverter)
- [X] T030 [P] [US2] Add XML doc-comments to 2 public types in src/BizScraper.Api/Infrastructure/Storage/ (AzureBlobStorageClient, NullBlobStorageClient)
- [X] T031 [P] [US2] Add XML doc-comments to 5 public types in src/BizScraper.Api/Middleware/ and Features/HealthCheck/ (CorrelationIdMiddleware, GlobalExceptionHandler, RequestTimeoutMiddleware, SecurityHeadersMiddleware, HealthCheckEndpoints)
- [X] T032 [US2] Retain CS1591 in NoWarn (method-level docs deferred per C1 resolution); type-level enforcement via check-xml-doc-coverage.ps1
- [X] T033 [US2] Build project with dotnet build -c Release — confirm zero CS1591 warnings
- [X] T034 [US2] Run tools/ci/check-xml-doc-coverage.ps1 — confirm it passes (Green)

**Checkpoint**: All 120 public types have XML doc-comments. IDE tooltips display documentation. Build produces zero CS1591 warnings.

---

## Phase 5: User Story 3 - Database Changelog Documentation (Priority: P3)

**Goal**: Add `--comment:` attributes to the 4 non-compliant Liquibase changelog files

**Independent Test**: Run `tools/ci/check-liquibase-comments.ps1` — passes (Green)

### Implementation for User Story 3

- [X] T035 [P] [US3] Add --comment: to deploy/liquibase/changelog/ddl/1.0.2603.0-create-search-audit-table.sql
- [X] T036 [P] [US3] Add --comment: to deploy/liquibase/changelog/ddl/1.0.2603.1-add-diagnostics-url-column.sql
- [X] T037 [P] [US3] Add --comment: to deploy/liquibase/changelog/ddl/1.0.2603.2-create-entity-detail-audit-table.sql
- [X] T038 [P] [US3] Add --comment: to deploy/liquibase/changelog/ddl/2.0.2603.0-add-state-column-to-search-audit.sql
- [X] T039 [US3] Run tools/ci/check-liquibase-comments.ps1 — confirm it passes (Green)

**Checkpoint**: All 8 Liquibase changesets have comment attributes. `liquibase validate` passes.

---

## Phase 6: User Story 4 - Full Constitution Compliance Assessment (Priority: P4)

**Goal**: Perform formal compliance assessment of all 6 principles and 5 quality gates, documented inline in the global snapshot

**Independent Test**: Review `specs/current-specs/spec.md` "Constitution Compliance" section — every principle and gate has a status with evidence

### Implementation for User Story 4

- [X] T040 [US4] Audit Principle I (Test-First) — assess test pyramid coverage, TDD discipline, coverage enforcement. Document status and evidence in specs/current-specs/spec.md
- [X] T041 [P] [US4] Audit Principle II (SOLID) — assess architecture patterns, DI use, interface segregation. Document status and evidence in specs/current-specs/spec.md
- [X] T042 [P] [US4] Audit Principle III (Quality Attributes) — assess reliability, resilience, security middleware, observability. Document status and evidence in specs/current-specs/spec.md
- [X] T043 [P] [US4] Audit Principle IV (Versioning) — assess version format compliance across artifacts. Document status and evidence in specs/current-specs/spec.md
- [X] T044 [P] [US4] Audit Principle V (Deployment) — assess CI/CD automation, IaC, secrets management. Document status and evidence in specs/current-specs/spec.md
- [X] T045 [US4] Audit Principle VI (Documentation) — document post-remediation compliance (after US1-US3 completion). Document status and evidence in specs/current-specs/spec.md
- [X] T046 [US4] Audit 5 quality gates (Test, Static Analysis, Security, Review, Build) — verify automation status in deploy/pipelines/ci.yaml and document in specs/current-specs/spec.md
- [X] T047 [US4] Document remediation plans for any principle/gate rated Partial or Non-Compliant in specs/current-specs/spec.md

**Checkpoint**: The "Constitution Compliance" section in the global snapshot has all 6 principles and 5 quality gates assessed with evidence.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: CI pipeline integration and final validation

- [X] T048 Add DocumentationValidation job to deploy/pipelines/ci.yaml with 3 PowerShell validation steps
- [X] T049 [P] Add JSDoc comments to Angular test UI services and components in src/test-ui/src/app/ (FR-006 SHOULD)
- [X] T050 Run all 3 CI validation scripts — confirm all pass (final Green)
- [X] T051 Run quickstart.md verification steps — confirm all success criteria met

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **User Stories (Phases 3–6)**: All depend on Phase 1 (CI scripts must exist to validate)
  - US1 (Global Snapshot), US2 (XML Docs), US3 (Liquibase Comments) can proceed in parallel
  - US4 (Compliance Assessment) depends on US1 completion (writes into the global snapshot)
  - US4 T045 (Principle VI audit) depends on US1, US2, US3 completion (documents post-remediation state)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Phase 1 — No dependencies on other stories
- **User Story 2 (P2)**: Can start after Phase 1 — No dependencies on other stories
- **User Story 3 (P3)**: Can start after Phase 1 — No dependencies on other stories
- **User Story 4 (P4)**: Depends on US1 (T006 creates the file T040-T047 write into). T045 depends on US1+US2+US3 completion

### Within Each User Story

- US1: T006 (spec.md) first, then T007-T015 in parallel, then T016 (validation)
- US2: T017-T031 all in parallel (different files), then T032 (remove suppression), T033 (build verify), T034 (script verify)
- US3: T035-T038 all in parallel (different files), then T039 (validation)
- US4: T040-T044 in parallel (different principles), then T045 (after US1-US3), T046, T047

### Parallel Opportunities

```
Phase 1 (Setup): T002, T003, T004 in parallel
Phase 3 (US1):   T007-T015 in parallel (all contract files)
Phase 4 (US2):   T017-T031 in parallel (all different source directories)
Phase 5 (US3):   T035-T038 in parallel (all different SQL files)
Phase 6 (US4):   T041-T044 in parallel (different principles)
Phase 7:         T048, T049 in parallel
```

---

## Parallel Example: User Story 2 (Most Tasks)

```
# All doc-comment tasks can run simultaneously (different directories):
T017: Infrastructure/Scraping (21 types)
T018: Features/BusinessSearch (18 types)
T019: Features/GermanySearch (15 types)
T020: Features/EntityDetails (12 types)
T021: Common/Configuration (7 types)
T022: Common/Interfaces (7 types)
T023: Features/ExecuteScript (9 types)
T024: Infrastructure/CaptchaSolving (5 types)
T025: Infrastructure/DocumentTranslation (5 types)
T026: Domain/Entities (3 types)
T027: Infrastructure/Persistence (3 types)
T028: Features/Documents (4 types)
T029: Infrastructure/Pdf (2 types)
T030: Infrastructure/Storage (2 types)
T031: Middleware + HealthCheck (5 types)

# After all parallel tasks complete:
T032: Remove CS1591 suppression
T033: Build verification
T034: Script verification
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (CI validation scripts)
2. Complete Phase 3: User Story 1 (Global Specification Snapshot)
3. **STOP and VALIDATE**: `specs/current-specs/` exists, snapshot script passes
4. A new developer can now understand the full API surface

### Incremental Delivery

1. Phase 1 → CI validation scripts exist (all fail)
2. US1 → Global snapshot created → snapshot script passes
3. US3 → Liquibase comments added → Liquibase script passes (quick win)
4. US2 → XML docs added → doc coverage script passes (largest effort)
5. US4 → Compliance assessment completed → all evidence documented
6. Phase 7 → CI integration + final validation → all scripts pass in pipeline

### Parallel Team Strategy

With multiple developers:

1. Team completes Phase 1 together
2. Once Phase 1 done:
   - Developer A: User Story 1 (Global Snapshot — 11 tasks)
   - Developer B: User Story 2 (XML Docs — 18 tasks, largest workload)
   - Developer C: User Story 3 (Liquibase Comments — 5 tasks, quickest)
3. After US1 completes: Developer A or C takes User Story 4
4. Phase 7 after all stories complete

---

## Notes

- All [P] tasks target different files — no merge conflicts when parallelized
- US2 is the largest story (102 types across 15 parallel tasks) but each task is mechanical (add `/// <summary>` comments)
- US3 is the smallest story (4 file edits) and can be completed in minutes
- US4 writes into `specs/current-specs/spec.md` (created by US1), so US1 must complete first
- T032 (remove CS1591 suppression) is the critical gate for SC-004 — only remove after ALL 102 types are documented
- FR-006 (Angular JSDoc) is a SHOULD requirement (T049) — non-blocking if time-constrained
