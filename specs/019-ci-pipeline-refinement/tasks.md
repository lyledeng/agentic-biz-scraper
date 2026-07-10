# Tasks: Refine CI Pipelines

**Input**: Design documents from `specs/019-ci-pipeline-refinement/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Exact file paths included in descriptions

---

## Phase 1: Setup

**Purpose**: Helm tooling and foundational configuration needed before pipeline and chart work

- [X] T001 Install Helm 3 step in pipeline — add `HelmInstaller@1` task at the top of deploy/pipelines/ci.yaml

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Dockerfile and Helm chart changes that MUST be complete before the pipeline can reference them

**⚠️ CRITICAL**: The pipeline YAML (Phase 3+) references build args, ingress templates, and values that must exist first.

- [X] T002 [P] Add `ARG BASE_HREF=/` and `ARG API_BASE_URL=https://localhost:8443` to the build stage of src/bizscraper-ui-dockerfile, pass `--base-href ${BASE_HREF}` to `ng build`, and add a `sed` step to replace `DEFAULT_URL` in the built JS with `${API_BASE_URL}`
- [X] T003 [P] Create API ingress template in deploy/helm/bizscraper/templates/ingress.yaml mirroring the existing bizscraper-ui ingress template, with backend-protocol HTTPS and port 8443 backend
- [X] T004 [P] Add ingress values block (enabled, className, annotations, hosts, tls) to deploy/helm/bizscraper/values.yaml per the data-model Helm values for API
- [X] T005 [P] Update deploy/helm/bizscraper-ui/values.yaml — set ingress host to `devcaas-az.ilienonline.com`, path to `/mvpoc/bizscraper-ui(/|$)(.*)`, pathType to `ImplementationSpecific`, add rewrite-target and proxy timeout annotations, and configure TLS with `ingress-tls-cert-secret`
- [X] T006 [P] Update deploy/helm/bizscraper-ui/templates/ingress.yaml — add `pathType` field from values and ensure annotations render rewrite-target pattern correctly

**Checkpoint**: Dockerfiles and Helm charts are ready; pipeline YAML tasks can now reference them.

---

## Phase 3: User Story 1 — Validate Every Commit (Priority: P1) 🎯 MVP

**Goal**: Every commit to any branch runs build, test, Helm lint, Liquibase validate+update, Angular build+test, and documentation validation. Failures block subsequent stages.

**Independent Test**: Push a commit to any branch; verify all validation jobs run and report pass/fail.

### Implementation for User Story 1

- [X] T007 [US1] Restructure deploy/pipelines/ci.yaml into two stages: `Validation` (runs always) and `ImageBuildAndPush` (runs conditionally) — define the stage skeleton with `dependsOn` and `condition`
- [X] T008 [US1] Define the `DotNetBuild` job in the Validation stage of deploy/pipelines/ci.yaml — UseDotNet@2 (sdk 10.0.x), dotnet restore, dotnet build (Release), dotnet test
- [X] T009 [P] [US1] Define the `AngularBuild` job in the Validation stage of deploy/pipelines/ci.yaml — UseNode@1 (22.x), npm ci, ng build (production), ng test (ChromeHeadless, no watch)
- [X] T010 [P] [US1] Define the `HelmLint` job in the Validation stage of deploy/pipelines/ci.yaml — HelmInstaller@1, `helm lint deploy/helm/bizscraper`, `helm lint deploy/helm/bizscraper-ui`
- [X] T011 [P] [US1] Define the `LiquibaseValidate` job in the Validation stage of deploy/pipelines/ci.yaml — start SQL Server Edge container (`mcr.microsoft.com/azure-sql-edge:latest`), build Liquibase image (`docker build -f deploy/liquibase/liquibase_dockerfile -t cososwebscraping-liquibase:latest .`), run `liquibase validate` then `liquibase update` against the SQL Edge JDBC URL
- [X] T012 [P] [US1] Define the `DocumentationValidation` job in the Validation stage of deploy/pipelines/ci.yaml — UseDotNet@2, run `tools/ci/check-xml-doc-coverage.ps1`, `tools/ci/check-liquibase-comments.ps1`, `tools/ci/check-global-snapshot.ps1`

**Checkpoint**: Validation stage is complete. Every commit triggers all validation jobs; any failure blocks the pipeline.

---

## Phase 4: User Story 2 — Push Container Images for Branch Builds (Priority: P1)

**Goal**: Non-main branch pushes (not PRs) build and push API + UI images to the Assembly ACR with the Build Number as tag.

**Independent Test**: Push to a feature branch (not a PR); verify images land in Assembly ACR tagged with `$(Build.BuildNumber)`.

### Implementation for User Story 2

- [X] T013 [US2] Define the `ImageBuildAndPush` stage condition in deploy/pipelines/ci.yaml — `condition: and(succeeded(), ne(variables['Build.Reason'], 'PullRequest'))` with `dependsOn: Validation`
- [X] T014 [US2] Define pipeline variables for container registry service connection — use `${{ if eq(variables['Build.SourceBranchName'], 'main') }}` template expression to set `containerRegistryServiceConnection` to Staging or Assembly ACR name
- [X] T015 [US2] Define the `BuildAndPushAPI` job in the ImageBuildAndPush stage of deploy/pipelines/ci.yaml — Docker@2 task with `buildAndPush` command, `repository: bizscraper`, `dockerfile: src/bizscraper-dockerfile`, `containerRegistry: $(containerRegistryServiceConnection)`, `tags: $(Build.BuildNumber)`
- [X] T016 [US2] Define the `BuildAndPushUI` job in the ImageBuildAndPush stage of deploy/pipelines/ci.yaml — Docker@2 task with `buildAndPush` command, `repository: bizscraper-ui`, `dockerfile: src/bizscraper-ui-dockerfile`, `containerRegistry: $(containerRegistryServiceConnection)`, `tags: $(Build.BuildNumber)`, `arguments: --build-arg BASE_HREF=/mvpoc/bizscraper-ui/ --build-arg API_BASE_URL=/mvpoc/bizscraper-api`

**Checkpoint**: Branch pushes produce images in Assembly ACR; PRs skip image push entirely.

---

## Phase 5: User Story 3 — Push Container Images for Main Branch (Priority: P1)

**Goal**: Main branch pushes route images to the Staging ACR. Same build process, different registry.

**Independent Test**: Merge to main; verify images land in Staging ACR.

### Implementation for User Story 3

- [X] T017 [US3] Verify the conditional variable expression from T014 correctly resolves to `Azure Container Registry (Staging)-iLienMVP` when `Build.SourceBranchName` is `main` — add a pipeline diagnostic step that logs the resolved service connection name in deploy/pipelines/ci.yaml

**Checkpoint**: Main and non-main branches route to the correct registry. US2 and US3 share the same build jobs; the only difference is the service connection variable.

---

## Phase 6: User Story 4 — Ingress Configuration for Non-Main Branches (Priority: P2)

**Goal**: Helm chart ingress values are configured so that non-main deployments are reachable at the expected paths on `devcaas-az.ilienonline.com`.

**Independent Test**: Deploy to the dev cluster from a feature branch; verify API and UI are reachable at their ingress paths with valid TLS.

### Implementation for User Story 4

- [X] T018 [US4] Verify deploy/helm/bizscraper/values.yaml ingress values from T004 match data-model specification — host `devcaas-az.ilienonline.com`, path `/mvpoc/bizscraper-api(/|$)(.*)`, pathType `ImplementationSpecific`, TLS secret `ingress-tls-cert-secret`, annotations (backend-protocol HTTPS, rewrite-target `/$2`, ssl-redirect, proxy timeouts 120s). Also confirm namespace `mvpoc` is documented in quickstart.md deploy commands (FR-019).
- [X] T019 [US4] Verify deploy/helm/bizscraper-ui/values.yaml ingress values from T005 match data-model specification — host `devcaas-az.ilienonline.com`, path `/mvpoc/bizscraper-ui(/|$)(.*)`, pathType `ImplementationSpecific`, TLS secret `ingress-tls-cert-secret`, annotations (backend-protocol HTTP, rewrite-target `/$2`, ssl-redirect, proxy timeouts 120s)
- [X] T020 [US4] Verify deploy/helm/bizscraper/templates/ingress.yaml from T003 renders correctly via `helm template deploy/helm/bizscraper` — confirm output matches the ingress sample from the spec (NGINX class, annotations, paths, TLS)
- [X] T021 [US4] Verify deploy/helm/bizscraper-ui/templates/ingress.yaml from T006 renders correctly via `helm template deploy/helm/bizscraper-ui` — confirm output matches expected annotations, paths, and TLS

**Checkpoint**: Both Helm charts produce correct ingress manifests for the dev cluster.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and cleanup

- [X] T022 Run `helm lint deploy/helm/bizscraper` and `helm lint deploy/helm/bizscraper-ui` locally to confirm charts pass linting
- [ ] T023 Run a local Docker build of the UI image with `--build-arg BASE_HREF=/mvpoc/bizscraper-ui/ --build-arg API_BASE_URL=/mvpoc/bizscraper-api` to confirm the Dockerfile changes work
- [X] T024 Verify the complete ci.yaml is valid YAML — no syntax errors, all variable references resolve, stage/job dependency graph is correct
- [X] T025 Update specs/current-specs/ global snapshot with CI pipeline changes from this feature

---

## Dependencies

```
T001 (Helm install) ←── T010 (HelmLint job)
T002 (UI Dockerfile) ←── T016 (BuildAndPushUI job), T023 (local build test)
T003 (API ingress template) ←── T018, T020 (ingress verification)
T004 (API ingress values) ←── T018, T020 (ingress verification)
T005 (UI ingress values) ←── T019, T021 (ingress verification)
T006 (UI ingress template) ←── T019, T021 (ingress verification)
T007 (stage skeleton) ←── T008-T012 (validation jobs), T013-T016 (image jobs)
T013 (stage condition) ←── T015, T016 (image build jobs)
T014 (registry variable) ←── T015, T016 (image build jobs), T017 (main verification)
```

### User Story Completion Order

1. **Phase 2** (Foundational) → completed first; no user story dependency
2. **US1** (Phase 3) → completed next; pipeline validation stage
3. **US2 + US3** (Phases 4-5) → completed together; image push stage + registry routing
4. **US4** (Phase 6) → completed last; ingress verification depends on Helm chart work from Phase 2

### Parallel Execution Opportunities

| Phase | Parallelizable Tasks | Reason |
|-------|---------------------|--------|
| Phase 2 | T002, T003, T004, T005, T006 | All modify different files |
| Phase 3 | T009, T010, T011, T012 | Independent validation jobs (different files within ci.yaml, but separate job definitions) |
| Phase 4 | T015, T016 | API and UI build jobs are independent |
| Phase 6 | T018+T020, T019+T021 | API and UI ingress verification are independent |
| Phase 7 | T022, T023 | Local linting and Docker build are independent |

## Implementation Strategy

- **MVP scope**: User Story 1 (Phase 3) — even with just the validation stage, every commit gets quality gates.
- **Incremental delivery**: Each phase adds a complete, independently testable increment.
- **Risk mitigation**: Foundational Dockerfile/Helm changes (Phase 2) are done first, so pipeline tasks can reference them without broken references.
- **Total tasks**: 25
