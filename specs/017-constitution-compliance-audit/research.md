# Research: Constitution Compliance Audit

**Feature**: 017-constitution-compliance-audit
**Date**: 2026-03-29

## R-001: DocumentationFile Enablement Strategy

**Decision**: Enable `GenerateDocumentationFile` in `Directory.Build.props`
with CS1591 suppressed via `NoWarn` during transition. Remove suppression
after all 102 public types receive XML doc-comments.

**Rationale**: The project already has `TreatWarningsAsErrors=true` and
`CodeAnalysisTreatWarningsAsErrors=true`. Enabling `GenerateDocumentationFile`
without suppressing CS1591 would immediately break the build for 102
undocumented types. A phased approach (enable + suppress → document all
types → remove suppression) preserves build stability during the work.

**Alternatives considered**:
- Enable `DocumentationFile` without suppression → Rejected: build breaks
  immediately with 102 errors, blocking all other development.
- Use `#pragma warning disable` per file → Rejected: 102 files would need
  pragmas, then removal. Central `NoWarn` is cleaner.
- Skip `DocumentationFile` entirely → Rejected: without it, Swagger UI
  cannot display XML doc-comment descriptions and SC-004 is unverifiable.

**Implementation**:
```xml
<!-- Directory.Build.props — add to PropertyGroup -->
<GenerateDocumentationFile>true</GenerateDocumentationFile>
<!-- Temporarily suppress CS1591 until all types documented -->
<NoWarn>$(NoWarn);CS1591</NoWarn>
```
After all 102 types are documented, remove `;CS1591` from `NoWarn`.

## R-002: Liquibase SQL Comment Syntax

**Decision**: Use `--comment:` on its own line immediately after the
`--changeset` line, matching the convention established in changesets
2.0.2603.3 through 2.0.2603.6.

**Rationale**: Direct observation of the 4 compliant files shows a
consistent format. The non-compliant files use `-- changeset` (with
space) while compliant files use `--changeset` (no space). The comment
line format is `--comment: <description text>`.

**4 files needing comments**:

| File | Changeset ID | Proposed Comment |
|------|-------------|-----------------|
| `1.0.2603.0-create-search-audit-table.sql` | `bizscraper:1.0.2603.0-create-search-audit-table` | "Create SearchAuditEntries table for business search audit trail" |
| `1.0.2603.1-add-diagnostics-url-column.sql` | `bizscraper:1.0.2603.1-add-diagnostics-url-column` | "Add DiagnosticsUrl column to SearchAuditEntries for scraping trace links" |
| `1.0.2603.2-create-entity-detail-audit-table.sql` | `bizscraper:1.0.2603.2-create-entity-detail-audit-table` | "Create EntityDetailAuditEntries table for entity detail scraping audit trail" |
| `2.0.2603.0-add-state-column-to-search-audit.sql` | `bizscraper:2.0.2603.0-add-state-column-to-search-audit` | "Add State column to SearchAuditEntries for multi-state search support" |

**Note**: The older files also use `-- changeset` (with space) vs
`--changeset` (no space). The comment addition should not change the
changeset header format as Liquibase already parses both forms.

## R-003: CI Pipeline Integration for Validation Scripts

**Decision**: Add a new `ValidationChecks` job in `ci.yaml` (Azure DevOps
format) that runs the 3 PowerShell validation scripts after the build.

**Rationale**: The pipeline uses Azure DevOps YAML with two parallel jobs
(`DotNetBuild`, `AngularBuild`). Documentation validation is independent
of both builds and can run as a third parallel job. PowerShell scripts are
the project's existing convention for CI tooling.

**Alternatives considered**:
- Add steps to existing `DotNetBuild` job → Rejected: makes the job
  longer and mixes concerns (build vs. documentation validation).
- Use MSBuild targets for doc validation → Rejected: Liquibase and
  specs/current-specs/ checks are not MSBuild concerns.
- Use a separate pipeline → Rejected: over-engineered for 3 scripts.

**Implementation**: New job `DocumentationValidation` with 3 `pwsh` steps.

## R-004: Feature Spec Supersession Map

**Decision**: The global snapshot will be organized by these current
capabilities, drawing from the authoritative feature specs:

| Capability | Authoritative Spec(s) | Endpoint |
|------------|----------------------|----------|
| Generic Script Execution | 016 | `POST /api/v1/execute-script` |
| Definition Discovery | 016 | `GET /api/v1/definitions` |
| CO Business Search | 001 → 005 → 006 → 016 (latest: 016) | Via execute-script `co-business-search` |
| WY Business Search | 005 → 006 → 016 (latest: 016) | Via execute-script `wy-business-search` |
| CO Entity Details | 004 → 006 → 016 (latest: 016) | Via execute-script `co-entity-details` |
| WY Entity Details | 007 + 008 + 009 → 016 (latest: 016) | Via execute-script `wy-entity-details` |
| Germany Business Search | 013 → 016 (latest: 016) | Via execute-script `de-business-search` |
| Germany Hardcopy Printout | 015 → 016 (latest: 016) | Via execute-script `de-hardcopy-printout` |
| PDF Proxy Streaming | 011 (not superseded) | `GET /api/v1/documents/{blobPath}` |
| Test UI | 010 (not superseded) | Angular app at `/` |
| Scraping Diagnostics | 002 (foundational, not superseded) | Infrastructure |
| Cloud Screenshot Storage | 003 (foundational, not superseded) | Infrastructure |
| JSON Scraping Engine v2 | 006 + 014 (not superseded) | Infrastructure |
| Semantic Error Handling | 012 (not superseded) | Error response pattern |

**Deprecated Endpoints** (include with `[DEPRECATED]` markers):
- `GET /api/v1/business-search` → Use `POST /api/v1/execute-script` with `co-business-search` or `wy-business-search`
- `GET /api/v1/entity-details` → Use `POST /api/v1/execute-script` with `co-entity-details` or `wy-entity-details`
- `GET /api/v1/germany-search` → Use `POST /api/v1/execute-script` with `de-business-search`
- `POST /api/v1/germany-search/current-hardcopy-printout` → Use `POST /api/v1/execute-script` with `de-hardcopy-printout`

## R-005: Quality Gate Compliance Assessment

**Decision**: Document the current state of each quality gate based on
CI/CD pipeline analysis. Two gates (Static Analysis, Security) are
not yet automated and will be documented as "Partial" with remediation
plans.

**Current State**:

| Gate | Status | Evidence |
|------|--------|----------|
| Test | ✅ Automated | `dotnet test` + `ng test --code-coverage` in CI |
| Static Analysis | ⚠️ Partial | `TreatWarningsAsErrors` + `.editorconfig` enforce at build time. SonarQube documented in design but not in CI pipeline. |
| Security | ⚠️ Partial | SecurityHeadersMiddleware exists. Black Duck SBOM in separate pipeline. No SAST in main CI. |
| Review | ✅ Manual | PR review process exists (Azure DevOps branch policies). Constitution requires ≥1 reviewer. |
| Build | ✅ Automated | `dotnet build -c Release` + Docker build in CI |

## R-006: Constitution Principle Compliance Pre-Assessment

**Findings by principle**:

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | ⚠️ Partial | Tests exist (xUnit, k6, Playwright). Full TDD discipline not formally verified. Test coverage metrics not enforced in CI. |
| II. SOLID | ✅ Compliant | Vertical slice architecture, CQRS via Litebus, DI throughout, interface segregation (IScraper, IBlobStorageClient, etc.) |
| III. Quality Attributes | ✅ Compliant | CorrelationIdMiddleware, SecurityHeadersMiddleware, RequestTimeoutMiddleware, GlobalExceptionHandler, Polly resilience, structured logging via [LoggerMessage] |
| IV. Versioning | ✅ Compliant | CalVer format observed (1.0.2603.x, 2.0.2603.x). Constitution version 1.2.2603.0. |
| V. Deployment | ✅ Compliant | Helm charts, Docker multi-stage builds, self-signed TLS, Azure DevOps pipelines, Key Vault for secrets |
| VI. Documentation | ❌ Non-Compliant | 118/120 public types undocumented (type-level `<summary>`), 4/8 changelogs missing comments, specs/current-specs/ missing. Method-level docs also missing (deferred to follow-up). Endpoint Swagger metadata ✅. Script descriptions ✅. |
