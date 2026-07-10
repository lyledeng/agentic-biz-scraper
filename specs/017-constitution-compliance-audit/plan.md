# Implementation Plan: Constitution Compliance Audit

**Branch**: `017-constitution-compliance-audit` | **Date**: 2026-03-29 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/017-constitution-compliance-audit/spec.md`

## Summary

Bring the BizScraper codebase into full compliance with constitution
v1.2.2603.0 by: (1) creating the `specs/current-specs/` global
specification snapshot with capability-based organization, (2) adding
XML doc-comments to 118 undocumented public C# types, (3) adding
Liquibase `--comment:` attributes to 4 older changelog files, (4)
performing a formal compliance assessment of all 6 principles and
5 quality gates with evidence recorded in the global snapshot, and
(5) creating CI validation scripts that enforce documentation
compliance going forward.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS), Angular 17 (test UI)
**Primary Dependencies**: System.Text.Json (source-gen), Litebus (CQRS), Playwright, Flurl.Http, Polly, OpenTelemetry
**Storage**: Azure SQL Managed Instance (audit trail), Azure Blob Storage (screenshots/documents)
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers + Azure SQL Edge, k6 (performance), Playwright (E2E)
**Target Platform**: Azure AKS (Alpine containers, HTTPS-only on port 8443)
**Project Type**: Web service (API) + Angular test UI
**Performance Goals**: N/A (documentation feature — no runtime changes)
**Constraints**: Zero CS1591 warnings when `DocumentationFile` enabled; all CI validation scripts must pass; method-level doc-comments deferred to follow-up feature
**Scale/Scope**: 120 public C# types (118 undocumented), 8 Liquibase changesets, 16 feature specs to consolidate, 9 API endpoints to document

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Phase 0 Assessment

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Test-First** | ✅ PASS | CI validation scripts serve as automated verification for docs-only work (per clarification Q4). No production logic changes require TDD. |
| **II. SOLID** | ✅ PASS | No new classes/abstractions. CI scripts are single-responsibility PowerShell files. |
| **III. Quality Attributes** | ✅ PASS | No runtime changes. Self-documentability is the target of this feature. |
| **IV. Versioning** | ✅ PASS | No artifact version changes needed (docs-only). |
| **V. Deployment** | ✅ PASS | No deployment changes. CI pipeline additions are additive. |
| **VI. Documentation** | ⚠️ TARGET | This is the principle we are remediating. Feature completion = compliance. |

### Quality Gates

| Gate | Status | Notes |
|------|--------|-------|
| **Test Gate** | ✅ | CI validation scripts + existing test suite unchanged |
| **Static Analysis** | ⚠️ | Not yet in CI pipeline (remediation documented in compliance assessment) |
| **Security** | ⚠️ | Not yet in CI pipeline (remediation documented in compliance assessment) |
| **Review** | ✅ | PR review process exists |
| **Build** | ✅ | Existing CI build gate passes |

**Gate Verdict**: PASS — No violations block this feature. The missing
Static Analysis and Security gates are pre-existing gaps that will be
documented in the compliance assessment (User Story 4, FR-009).

## Project Structure

### Documentation (this feature)

```text
specs/017-constitution-compliance-audit/
├── plan.md              # This file
├── research.md          # Phase 0: research findings
├── data-model.md        # Phase 1: entity catalog
├── quickstart.md        # Phase 1: implementation guide
├── contracts/           # Phase 1: N/A (no new API contracts)
├── checklists/          # Quality checklist
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### New Artifacts (repository root)

```text
specs/
└── current-specs/                    # NEW: Global specification snapshot
    ├── spec.md                       # Capability-based consolidated spec
    ├── data-model.md                 # Unified data model
    └── contracts/
        ├── execute-script.md         # Execute-script endpoint contract
        ├── definitions.md            # Definitions listing contract
        ├── document-proxy.md         # PDF proxy streaming contract
        ├── germany-hardcopy.md       # Germany hardcopy printout contract
        ├── health-check.md           # Health check contract
        ├── deprecated-business-search.md   # [DEPRECATED] business-search
        ├── deprecated-entity-details.md    # [DEPRECATED] entity-details
        └── deprecated-germany-search.md    # [DEPRECATED] germany-search + hardcopy

tools/
└── ci/
    ├── check-xml-doc-coverage.ps1    # CI script: verify C# XML docs
    ├── check-liquibase-comments.ps1  # CI script: verify changeset comments
    └── check-global-snapshot.ps1     # CI script: verify specs/current-specs/
```

### Modified Files

```text
Directory.Build.props                  # Enable GenerateDocumentationFile
deploy/pipelines/ci.yaml               # Add doc validation steps
deploy/liquibase/changelog/ddl/
  ├── 1.0.2603.0-create-search-audit-table.sql          # Add --comment:
  ├── 1.0.2603.1-add-diagnostics-url-column.sql         # Add --comment:
  ├── 1.0.2603.2-create-entity-detail-audit-table.sql   # Add --comment:
  └── 2.0.2603.0-add-state-column-to-search-audit.sql   # Add --comment:
src/BizScraper.Api/**/*.cs             # Add XML <summary> to 118 public types
```

**Structure Decision**: No new C# projects or Angular components.
Deliverables are documentation artifacts (specs/current-specs/),
CI validation scripts (tools/ci/), XML doc-comments on existing types,
and Liquibase comment attributes on existing changesets.

## Complexity Tracking

No constitution violations requiring justification. This feature
reduces complexity by closing documentation gaps.

## Post-Phase 1 Constitution Re-Check

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Test-First** | ✅ PASS | CI validation scripts (tools/ci/) serve as the Red→Green cycle: scripts written first (fail), then artifacts created (pass). |
| **II. SOLID** | ✅ PASS | Each CI script has single responsibility. No new abstractions. |
| **III. Quality Attributes** | ✅ PASS | Self-documentability remediated by this feature. |
| **IV. Versioning** | ✅ PASS | No version changes needed. |
| **V. Deployment** | ✅ PASS | CI pipeline additions are additive (new job, no existing changes). |
| **VI. Documentation** | ✅ PASS | Feature completion = full Principle VI compliance. |

**Re-Check Verdict**: All gates pass. No design changes introduced
complexity or violations.
