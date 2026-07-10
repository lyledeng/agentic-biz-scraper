# Implementation Plan: Refine CI Pipelines

**Branch**: `019-ci-pipeline-refinement` | **Date**: 2026-03-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/019-ci-pipeline-refinement/spec.md`

## Summary

Refine the Azure DevOps CI pipeline (`deploy/pipelines/ci.yaml`) into a two-stage pipeline: a **Validation Stage** (build, test, Helm lint, Liquibase validate+update via Testcontainers, Angular build+test, documentation validation) that runs on every commit including PRs, and a **Container Image Build & Push Stage** that runs only for branch pushes (not PRs), routing images to the Assembly or Staging Azure Container Registry based on the branch name. Helm chart ingress values are updated for path-based routing on the development cluster.

## Technical Context

**Language/Version**: Azure DevOps YAML Pipelines; .NET 10 (C# 14); Node.js 22; Angular 17  
**Primary Dependencies**: Azure DevOps, Docker, Helm 3, Liquibase 4.33, Testcontainers (Azure SQL Edge)  
**Storage**: Azure SQL Managed Instance (validated via Testcontainers SQL Edge in CI)  
**Testing**: dotnet test (xUnit), Angular test (Karma/ChromeHeadless), Helm lint, Liquibase validate+update  
**Target Platform**: Azure DevOps hosted agent (ubuntu-latest) → Azure AKS (mvpoc namespace)  
**Project Type**: CI/CD pipeline + Helm chart configuration  
**Performance Goals**: N/A (pipeline execution; no runtime latency targets)  
**Constraints**: Pipeline must complete within Azure DevOps timeout defaults; Docker tasks require Docker-capable agent  
**Scale/Scope**: Single `ci.yaml` pipeline, two Helm charts (`bizscraper`, `bizscraper-ui`), two Dockerfiles, three CI validation scripts

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Validation stage runs full test suite before any image push |
| II. SOLID Design | PASS | Pipeline uses stages/jobs for separation of concerns |
| III. Architectural Quality Attributes | PASS | Ingress has TLS, security headers, backend HTTPS; images tagged for traceability |
| IV. Hybrid SemVer+CalVer Versioning | PASS | Chart versions follow `MAJOR.MINOR.YYMM.PATCH`; images tagged with Build Number |
| V. Hands-Free Deployment & Rollback | PASS | Full automation; no manual steps beyond merge trigger |
| VI. Documentation | PASS | Pipeline YAML self-documents; CI scripts have header comments; feature spec + plan artifacts maintained |
| Design Standards — Error Handling | PASS | Pipeline fails fast on any validation error |
| Design Standards — API Design | N/A | No API contract changes |
| Design Standards — Global Spec Snapshot | DEFERRED | Update to `specs/current-specs/` after merge |
| Quality Gates — Build Gate | PASS | Pipeline IS the build gate |
| Quality Gates — Static Analysis Gate | PASS | Documentation validation + Helm lint enforced |

**Gate result: PASS** — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/019-ci-pipeline-refinement/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
deploy/
├── pipelines/
│   └── ci.yaml                          # PRIMARY: Refined two-stage CI pipeline
│
├── helm/
│   ├── bizscraper/
│   │   ├── values.yaml                  # MODIFY: Add ingress values for dev cluster
│   │   └── templates/
│   │       └── ingress.yaml             # NEW: API ingress template
│   │
│   └── bizscraper-ui/
│       ├── values.yaml                  # MODIFY: Update ingress host, TLS, and path config
│       └── templates/
│           └── ingress.yaml             # EXISTING: Update annotations and path pattern
│
└── liquibase/
    └── liquibase_dockerfile             # EXISTING: Used by Testcontainers Liquibase job

src/
├── bizscraper-dockerfile                # EXISTING: API Docker image (no changes)
├── bizscraper-ui-dockerfile             # MODIFY: Add BASE_HREF and API_BASE_URL build args
└── test-ui/
    ├── nginx.conf                       # EXISTING: No changes needed (ingress rewrite strips sub-path)
    └── src/app/core/services/
        └── api-config.service.ts        # EXISTING: DEFAULT_URL replaced in built JS by Dockerfile sed step

tools/
└── ci/
    ├── check-xml-doc-coverage.ps1       # EXISTING: Preserved
    ├── check-liquibase-comments.ps1     # EXISTING: Preserved
    └── check-global-snapshot.ps1        # EXISTING: Preserved
```

**Structure Decision**: No new projects or folders are introduced. Changes target the existing pipeline YAML, Helm values/templates, and the UI Dockerfile/nginx config. The `bizscraper` API Helm chart gains an ingress template (currently missing).

## Complexity Tracking

No constitution violations — table not required.
