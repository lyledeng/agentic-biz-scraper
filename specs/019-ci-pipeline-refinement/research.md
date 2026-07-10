# Research: Refine CI Pipelines

**Feature**: 019-ci-pipeline-refinement  
**Date**: 2026-03-30

## Research Tasks

### 1. Azure DevOps Multi-Stage Pipeline with Conditional Execution

**Decision**: Use Azure DevOps YAML `stages` with `condition` expressions to separate validation from image push.

**Rationale**: Azure DevOps supports `condition: and(succeeded(), ne(variables['Build.Reason'], 'PullRequest'))` to skip the image-push stage on PRs. Branch-specific registry selection uses `${{ if eq(variables['Build.SourceBranchName'], 'main') }}` template expressions to set the service connection name at compile time.

**Alternatives considered**:
- Single-stage pipeline with conditional steps: Rejected because steps that fail still mark the job as partially succeeded; stage-level gating provides clear pass/fail semantics.
- Separate pipeline files per branch: Rejected because it duplicates YAML and increases maintenance burden.

### 2. Container Image Tagging with Build Number

**Decision**: Tag all Docker images with `$(Build.BuildNumber)`.

**Rationale**: `$(Build.BuildNumber)` is a built-in pipeline variable that is unique per run, auto-incrementing, and visible in the Azure DevOps UI for traceability. It avoids the need to invoke `git rev-parse` in a script step.

**Alternatives considered**:
- Git short SHA: Requires a script step and `git` availability; less readable in ACR browser.
- Branch + Build ID composite: Longer tag names, no additional traceability benefit over Build Number alone.

### 3. Liquibase Validation via Testcontainers SQL Server Edge

**Decision**: Run the existing Liquibase Docker image (`cososwebscraping-liquibase:latest`) against a Testcontainers-managed SQL Server Edge instance in CI. Execute both `validate` and `update` commands.

**Rationale**: The project already uses `Testcontainers.MsSql` (v4.11.0) with `MsSqlBuilder().Build()` for integration tests. Running Liquibase against a real SQL Server Edge container validates not just changelog syntax but actual DDL/DML applicability. The Azure DevOps `ubuntu-latest` image includes Docker, so Docker-in-Docker is not required — Testcontainers communicates with the host Docker daemon.

**Implementation approach**:
- Build the Liquibase Docker image as a CI step (reuse existing VS Code task: `docker build -f deploy/liquibase/liquibase_dockerfile -t cososwebscraping-liquibase:latest .`).
- Start a SQL Server Edge container (`mcr.microsoft.com/azure-sql-edge:latest`) with a health check.
- Run the Liquibase container with `--network host` or Docker network linking, passing the SQL Edge JDBC URL.
- Execute `liquibase validate` followed by `liquibase update`.

**Alternatives considered**:
- Liquibase offline mode (`--url=offline:mssql`): Only validates syntax, not actual SQL execution; misses DDL errors that only surface against a real engine.
- Shared Azure SQL test instance: Adds external dependency, state pollution risk, and network requirements to CI.

### 4. Helm Lint in CI

**Decision**: Use `helm lint deploy/helm/bizscraper` and `helm lint deploy/helm/bizscraper-ui` as separate pipeline steps.

**Rationale**: Helm lint validates Chart.yaml, values.yaml, and template rendering without requiring a cluster. It catches malformed templates, missing required values, and YAML syntax errors. Running lint for both charts independently ensures each is valid in isolation.

**Implementation approach**: Install Helm via the `HelmInstaller@1` Azure DevOps task, then run `helm lint` as script steps.

**Alternatives considered**:
- `helm template` only: Renders templates but doesn't validate Chart.yaml metadata or required values.
- Skipping Helm validation: Unacceptable — constitution requires build gate for all artifacts.

### 5. Angular Base Href and API URL Configuration

**Decision**: Add `ARG BASE_HREF=/` and `ARG API_BASE_URL=https://localhost:8443` to the UI Dockerfile. Pass `--base-href=${BASE_HREF}` to `ng build`. Update `ApiConfigService` to use `API_BASE_URL` as the default (injected at build time via `environment.ts` replacement or nginx env substitution).

**Rationale**: The Angular app currently uses a runtime-configurable `ApiConfigService` with `DEFAULT_URL = 'https://localhost:8443'` backed by localStorage. For deployed environments, the default needs to point to the relative API path so the app works out of the box. The runtime override via the config bar remains functional for local testing.

**Implementation approach**:
- Add `ARG BASE_HREF=/` to Dockerfile build stage; pass to `ng build --base-href ${BASE_HREF}`.
- Add `ARG API_BASE_URL=https://localhost:8443`; use `sed` or a build-time script to replace `DEFAULT_URL` in the built JS bundle, or better: update `environment.prod.ts` to read from a compile-time constant.
- In CI, pass `--build-arg BASE_HREF=/mvpoc/bizscraper-ui/ --build-arg API_BASE_URL=/mvpoc/bizscraper-api` to `docker build`.

**Alternatives considered**:
- Runtime nginx env substitution: More complex nginx config; requires `envsubst` at container start; breaks read-only filesystem unless temp files are used.
- Hardcode in source: Prevents reuse across environments with different paths.

### 6. Nginx Sub-Path SPA Hosting

**Decision**: Update `nginx.conf` to support serving the Angular app from a sub-path when `BASE_HREF` is set.

**Rationale**: When Angular is built with `--base-href /mvpoc/bizscraper-ui/`, all asset references are prefixed. Nginx must serve the app from the root of the container but the ingress handles the path rewrite (`/$2`), so Nginx sees requests at `/` after rewrite. No Nginx location changes are needed — the ingress rewrite-target strips the prefix before it reaches Nginx.

**Key insight**: The ingress pattern `/mvpoc/bizscraper-ui(/|$)(.*)` with `rewrite-target: /$2` means Nginx receives paths like `/`, `/assets/main.js`, etc. — the sub-path is already stripped. The `--base-href` ensures Angular generates correct `<base href>` and asset URLs that pass through the browser back to the ingress with the full prefix.

**Alternatives considered**: None — this is the standard NGINX ingress + SPA pattern.

### 7. API Ingress for bizscraper (New Template)

**Decision**: Add an ingress template to `deploy/helm/bizscraper/templates/ingress.yaml` mirroring the existing `bizscraper-ui` ingress template, with API-specific path and backend-protocol HTTPS annotation.

**Rationale**: The `bizscraper` API Helm chart currently has no ingress template. The API needs path-based routing at `/mvpoc/bizscraper-api(/|$)(.*)` on port 8443 (HTTPS backend). The ingress template follows the same Helm value-driven pattern as the UI chart.

**Values additions** to `deploy/helm/bizscraper/values.yaml`:
- `ingress.enabled`, `ingress.className`, `ingress.annotations`, `ingress.hosts`, `ingress.tls`

**Alternatives considered**:
- Single ingress manifest covering both API and UI: Rejected because each chart should be independently deployable with its own ingress lifecycle.
