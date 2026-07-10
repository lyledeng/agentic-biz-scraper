# Feature Specification: Refine CI Pipelines

**Feature Branch**: `019-ci-pipeline-refinement`  
**Created**: 2026-03-30  
**Status**: Draft  
**Input**: User description: "Refine the CI Pipelines — build, test, helm lint, liquibase validate, branch-aware container image push to environment-specific Azure Container Registries, with ingress configuration for non-main branches"

## Clarifications

### Session 2026-03-30

- Q: What tag should container images receive when pushed? → A: Azure DevOps Build Number (`$(Build.BuildNumber)`)
- Q: How should Liquibase validate run in CI (null connection method)? → A: Testcontainers SQL Server Edge instance; run both validate and update.
- Q: What Kubernetes namespace should deployments target? → A: Single shared namespace `mvpoc` for all branch deployments.
- Q: How is the API base URL configured in the Angular UI? → A: Bake relative path `/mvpoc/bizscraper-api` into Angular environment file at Docker build time via build arg.
- Q: Should documentation validation gate image push or run as informational? → A: Documentation validation gates image push; must pass before images are built.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Validate Every Commit (Priority: P1)

As a developer pushing code to any branch, I want the CI pipeline to automatically build the solution, run all tests, lint Helm charts, and validate Liquibase changelogs so that I have immediate confidence that my changes are correct before any container images are produced.

**Why this priority**: Gating all subsequent steps on a passing build-and-test phase prevents broken images from ever being pushed, protecting downstream environments.

**Independent Test**: Push a commit to any branch and confirm the pipeline runs restore, build, test, helm lint (for both `bizscraper` and `bizscraper-ui` charts), and Liquibase validate (against a null SQL Server). Verify the pipeline reports pass/fail accurately.

**Acceptance Scenarios**:

1. **Given** a commit is pushed to any branch, **When** the CI pipeline runs, **Then** the .NET solution is restored, built in Release mode, and all unit/integration tests pass.
2. **Given** a commit is pushed to any branch, **When** the CI pipeline runs, **Then** Helm lint is executed against both the `bizscraper` and `bizscraper-ui` charts and reports any errors.
3. **Given** a commit is pushed to any branch, **When** the CI pipeline runs, **Then** a Testcontainers SQL Server Edge instance is started and both Liquibase validate and update are executed against it, reporting any changelog errors.
4. **Given** any of build, test, helm lint, liquibase validate/update, or documentation validation fails, **When** the pipeline evaluates the gate, **Then** no container images are built or pushed.

---

### User Story 2 - Push Container Images for Branch Builds (Priority: P1)

As a developer merging or pushing to a non-main branch (not via a Pull Request), I want the pipeline to build and push both the API and Angular UI container images to the Assembly container registry so that my feature is deployable to a development environment.

**Why this priority**: Feature branches need deployable artifacts in the Assembly registry for dev/test validation; this is the core delivery mechanism for non-main work.

**Independent Test**: Push a commit directly to a feature branch (not a PR). Verify both `bizscraper` and `bizscraper-ui` images are built and pushed to "Azure Container Registry (Assembly)-iLienMVP". Confirm the Angular build uses `/mvpoc/bizscraper-ui` as the base href and the API is configured for `/mvpoc/bizscraper-api`.

**Acceptance Scenarios**:

1. **Given** a commit is pushed to a non-main branch (not a PR) and all validation steps pass, **When** the image-build stage runs, **Then** the API container image is built and pushed to "Azure Container Registry (Assembly)-iLienMVP".
2. **Given** a commit is pushed to a non-main branch (not a PR) and all validation steps pass, **When** the image-build stage runs, **Then** the Angular UI container image is built with base href `/mvpoc/bizscraper-ui` and pushed to "Azure Container Registry (Assembly)-iLienMVP".
3. **Given** a Pull Request is opened or updated, **When** the CI pipeline runs, **Then** only the validation stage executes; no container images are built or pushed.

---

### User Story 3 - Push Container Images for Main Branch (Priority: P1)

As a release manager merging to main, I want the pipeline to build and push both container images to the Staging container registry so that the latest stable build is available for staging deployment.

**Why this priority**: The main branch represents the promotion path to staging/production; images must land in the correct Staging registry.

**Independent Test**: Merge a PR into main. Verify both `bizscraper` and `bizscraper-ui` images are built and pushed to "Azure Container Registry (Staging)-iLienMVP".

**Acceptance Scenarios**:

1. **Given** a commit is pushed to the `main` branch (not a PR) and all validation steps pass, **When** the image-build stage runs, **Then** the API container image is built and pushed to "Azure Container Registry (Staging)-iLienMVP".
2. **Given** a commit is pushed to the `main` branch and all validation steps pass, **When** the image-build stage runs, **Then** the Angular UI container image is built with base href `/mvpoc/bizscraper-ui` and pushed to "Azure Container Registry (Staging)-iLienMVP".

---

### User Story 4 - Ingress Configuration for Non-Main Branches (Priority: P2)

As a developer deploying a feature branch, I want the pipeline to configure Kubernetes Ingress so that the API and UI are reachable at their expected paths on the development host, enabling end-to-end testing.

**Why this priority**: Deployability to the dev cluster is critical for feature validation, but depends on images being pushed first (Stories 2 & 3).

**Independent Test**: Deploy a feature branch build and verify the ingress routes traffic for `/mvpoc/bizscraper-api` and `/mvpoc/bizscraper-ui` on `devcaas-az.ilienonline.com` using TLS secret `ingress-tls-cert-secret`.

**Acceptance Scenarios**:

1. **Given** a non-main branch deployment, **When** ingress is configured, **Then** the API is reachable at `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-api` with path rewriting.
2. **Given** a non-main branch deployment, **When** ingress is configured, **Then** the UI is reachable at `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-ui` with path rewriting.
3. **Given** a non-main branch deployment, **When** ingress is configured, **Then** TLS is terminated using the `ingress-tls-cert-secret` secret for host `devcaas-az.ilienonline.com`.
4. **Given** a non-main branch deployment, **When** ingress is configured, **Then** NGINX annotations include backend-protocol HTTPS for the API ingress (port 8443) and backend-protocol HTTP for the UI ingress (port 80), rewrite-target, ssl-redirect, and proxy read/send timeouts of 120 seconds.

---

### Edge Cases

- What happens when the Liquibase changelog has no changesets? The validate step should still pass cleanly.
- What happens when the Helm chart has a values file mismatch (e.g., a referenced value is missing)? Helm lint should catch and fail the pipeline.
- What happens when a PR is opened from a fork? The image-push stage must NOT execute (PRs never push images).
- What happens when the container registry is temporarily unavailable? The push step should fail the pipeline with a clear error rather than silently succeeding.
- What happens when both API and UI builds succeed but one image push fails? The pipeline should report partial failure and not mark the run as successful.

## Requirements *(mandatory)*

### Functional Requirements

#### Validation Stage (all branches, including PRs)

- **FR-001**: The pipeline MUST restore, build (Release configuration), and run all tests for the .NET solution (`BizScraper.slnx`) on every triggered run.
- **FR-002**: The pipeline MUST execute Helm lint against the `deploy/helm/bizscraper` chart on every triggered run.
- **FR-003**: The pipeline MUST execute Helm lint against the `deploy/helm/bizscraper-ui` chart on every triggered run.
- **FR-004**: The pipeline MUST spin up a disposable SQL Server Edge Docker container and execute both Liquibase validate and Liquibase update against it on every triggered run, verifying that changelogs apply cleanly to a real database.
- **FR-005**: The pipeline MUST restore, build, and test the Angular UI application on every triggered run.

#### Image Build & Push Stage (branches only, not PRs)

- **FR-006**: The image-build stage MUST only execute when the validation stage passes AND the trigger is a branch push (not a Pull Request).
- **FR-007**: For the `main` branch, the pipeline MUST push container images to the "Azure Container Registry (Staging)-iLienMVP" service connection.
- **FR-008**: For all non-main branches, the pipeline MUST push container images to the "Azure Container Registry (Assembly)-iLienMVP" service connection.
- **FR-009**: The pipeline MUST build and push an API container image using the existing `src/bizscraper-dockerfile`.
- **FR-010**: The pipeline MUST build and push an Angular UI container image using the existing `src/bizscraper-ui-dockerfile`, with the base href set to `/mvpoc/bizscraper-ui`.
- **FR-011**: The API MUST be accessible at the relative path `/mvpoc/bizscraper-api` via Kubernetes ingress path rewriting (rewrite-target `/$2`); no API-side PathBase middleware is required.
- **FR-018**: All container images MUST be tagged with the Azure DevOps Build Number (`$(Build.BuildNumber)`) for deployment traceability.
- **FR-020**: The Angular UI Docker build MUST accept a build arg for the API base URL (`API_BASE_URL`) and bake the relative path `/mvpoc/bizscraper-api` into the built application bundle at Docker build time (e.g., via `sed` replacement of the default URL in the compiled JS output).

#### Ingress Configuration (non-main branches)

- **FR-012**: For non-main branch deployments, the ingress host MUST be `devcaas-az.ilienonline.com`.
- **FR-013**: For non-main branch deployments, the TLS secret MUST be `ingress-tls-cert-secret`.
- **FR-014a**: The API ingress MUST use NGINX ingress class with annotations for `backend-protocol: "HTTPS"` (Kestrel on port 8443), `rewrite-target: /$2` with path capture, `ssl-redirect: "true"`, and proxy read/send timeouts of 120 seconds.
- **FR-014b**: The UI ingress MUST use NGINX ingress class with annotations for `backend-protocol: "HTTP"` (Nginx on port 80), `rewrite-target: /$2` with path capture, `ssl-redirect: "true"`, and proxy read/send timeouts of 120 seconds.
- **FR-015**: The API ingress path MUST be `/mvpoc/bizscraper-api(/|$)(.*)` with rewrite-target `/$2`.
- **FR-016**: The UI ingress path MUST be `/mvpoc/bizscraper-ui(/|$)(.*)` with rewrite-target `/$2`.
- **FR-019**: All deployments (main and non-main) MUST target the `mvpoc` Kubernetes namespace. This is enforced at deploy-time via the Helm release `--namespace mvpoc` flag; CI validates the Helm charts are lint-clean but does not deploy.

#### Existing Steps Preservation

- **FR-017**: The pipeline MUST continue to run the documentation validation steps (XML doc-comment coverage, Liquibase changeset comments, global spec snapshot) from the existing CI configuration. Documentation validation MUST pass before any container images are built or pushed.

### Key Entities

- **CI Pipeline**: The Azure DevOps YAML pipeline definition that orchestrates all build, test, and publish steps.
- **Container Registry**: An Azure Container Registry instance that stores built Docker images; two registries are used — Assembly (dev) and Staging (pre-production).
- **Helm Chart**: Kubernetes deployment descriptor for each service (`bizscraper` API and `bizscraper-ui`), subject to linting in CI.
- **Ingress**: Kubernetes networking resource that exposes services externally via HTTPS with path-based routing and TLS termination.
- **Liquibase Changelog**: Database migration definitions validated for correctness during CI.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every commit to any branch triggers the validation stage (build, test, helm lint, liquibase validate) and reports results within the pipeline summary.
- **SC-002**: Container images are never pushed when any validation step fails.
- **SC-003**: Container images are never pushed for Pull Request triggers.
- **SC-004**: Commits to `main` result in images pushed exclusively to the Staging container registry.
- **SC-005**: Commits to non-main branches result in images pushed exclusively to the Assembly container registry.
- **SC-006**: The Angular UI image serves the application correctly under the `/mvpoc/bizscraper-ui` base path.
- **SC-007**: The API is accessible under the `/mvpoc/bizscraper-api` base path after deployment.
- **SC-008**: Non-main branch deployments are reachable at `https://devcaas-az.ilienonline.com` with valid TLS using the configured secret.

## Assumptions

- The Azure DevOps service connections "Azure Container Registry (Staging)-iLienMVP" and "Azure Container Registry (Assembly)-iLienMVP" already exist and the pipeline has permissions to push images through them.
- The existing Dockerfiles (`src/bizscraper-dockerfile` and `src/bizscraper-ui-dockerfile`) produce valid, runnable images and do not need modification beyond build-arg configuration for the base href.
- Liquibase validation in CI uses a disposable Testcontainers SQL Server Edge instance to run both validate and update, confirming changelogs apply cleanly against a real database engine.
- The `ingress-tls-cert-secret` Kubernetes secret already exists in the `mvpoc` namespace on the development cluster.
- All Helm releases deploy into the shared `mvpoc` namespace; no per-branch namespace isolation is used.
- The NGINX Ingress Controller is already deployed and operational on the target Kubernetes cluster.
- Main branch ingress and TLS configuration are managed separately (e.g., by CD pipeline or platform team) and are out of scope for this feature.
- The existing documentation validation job (XML doc-comment coverage, Liquibase changeset comments, global spec snapshot) should be preserved as-is in the refined pipeline.
