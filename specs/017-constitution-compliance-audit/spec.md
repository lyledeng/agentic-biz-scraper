# Feature Specification: Constitution Compliance Audit

**Feature Branch**: `017-constitution-compliance-audit`  
**Created**: 2026-03-29  
**Status**: Draft  
**Input**: User description: "Perform an audit of our current codebase and bring it all up to the constitution."

## Clarifications

### Session 2026-03-29

- Q: Should this feature audit all 6 constitution principles + quality gates, or only Principle VI + Global Spec Snapshot? → A: Audit ALL 6 principles + quality gates (full constitution compliance).
- Q: How should the global snapshot consolidate 16 feature specs with superseded capabilities? → A: Capability-based — organize by current capability (search, entity-details, documents, script-execution, Germany), using only the latest authoritative spec for each.
- Q: Where should the compliance evidence for all 6 principles and 5 quality gates be recorded? → A: Inline in the global snapshot `specs/current-specs/spec.md` as a "Constitution Compliance" section (living document).
- Q: How should Test-First (Principle I) apply to documentation-only deliverables? → A: CI validation scripts — automated checks that verify doc coverage, changelog comments, and snapshot existence, satisfying the "automated verification" spirit without traditional unit tests.
- Q: Should the global snapshot document deprecated bespoke endpoints alongside the generic execute-script endpoint? → A: Yes — include deprecated endpoints with clear "[DEPRECATED]" markers and migration notes pointing to `/api/v1/execute-script`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Global Specification Snapshot (Priority: P1)

A new developer joins the project and needs to understand the current API
surface, data model, and behavioural contracts without reading 16 individual
feature specs. They navigate to `specs/current-specs/` and find a single
consolidated specification, a unified data model, and the current API
contracts in one place.

**Why this priority**: The constitution (v1.2.2603.0) explicitly requires
this folder as the "canonical reference for the project's current API
surface, data model, and behavioural contracts." It is the only
constitution standard with zero existing compliance.

**Independent Test**: Verify that `specs/current-specs/` exists and
contains `spec.md`, `data-model.md`, and a `contracts/` folder whose
contents accurately reflect the current system state.

**Acceptance Scenarios**:

1. **Given** the repository has 16 feature specifications, **When** a
   reviewer inspects `specs/current-specs/spec.md`, **Then** it is
   organized by current capability (search, entity-details, documents,
   script-execution, Germany) rather than by feature number, and uses
   only the latest authoritative spec for each capability.
2. **Given** the global snapshot exists, **When** a contributor reads
   any capability section, **Then** it reflects the current
   implementation without references to superseded feature-level
   details (e.g., the search section reflects feature 016's
   execute-script model, not feature 001's original endpoint).
3. **Given** the global snapshot contains `contracts/`, **When** an
   API consumer reads the contract files, **Then** they find the
   execute-script endpoint request/response schema, definitions
   listing, PDF proxy contract, and deprecated bespoke endpoints
   marked with `[DEPRECATED]` and migration guidance.

---

### User Story 2 - Code Documentation for Public Types (Priority: P2)

A developer reviewing a pull request opens a public record such as
`NormalizedSearchResult` and sees an XML doc-comment explaining the
record's purpose and each parameter. The IDE tooltip displays this
documentation without needing to read external docs.

**Why this priority**: 118 of 120 public types lack XML doc-comments.
This is the largest single compliance gap against Principle VI
(Documentation) and Principle III (Self-Documentability).

**Independent Test**: Run a documentation coverage check and confirm
that every public class, record, interface, and enum in
`src/BizScraper.Api/` has an XML `<summary>` comment.

**Acceptance Scenarios**:

1. **Given** 118 public types currently lack documentation, **When**
   all types receive XML `<summary>` comments, **Then** a grep for
   `public (sealed|abstract)? (record|class|interface|enum)` finds
   zero types without a preceding `/// <summary>`.
2. **Given** the project enables `DocumentationFile` in build props,
   **When** the project builds, **Then** zero CS1591 warnings are
   emitted for missing XML comments on public types.

---

### User Story 3 - Database Changelog Documentation (Priority: P3)

A DBA reviewing the Liquibase changelogs can understand the purpose of
each migration by reading the `comment` attribute on each changeset,
without needing to inspect the SQL statements.

**Why this priority**: 4 of 8 SQL changelog files lack Liquibase
`comment` attributes. The 4 newer changesets (2.0.2603.3 onward) are
already compliant. This is a constitution requirement but has lower
runtime impact than the code documentation gaps.

**Independent Test**: Inspect each changeset to confirm a `comment`
attribute exists describing the migration's purpose.

**Acceptance Scenarios**:

1. **Given** 4 SQL changelog files lack comments, **When** each
   receives a `--comment:` attribute, **Then** all 8 changeset
   headers include a comment describing the migration's purpose.
2. **Given** changelog comments are present, **When** `liquibase
   validate` runs, **Then** it passes without errors.

---

### User Story 4 - Full Constitution Compliance Assessment (Priority: P4)

An auditor reviews the project against all six constitution principles
(Test-First, SOLID, Quality Attributes, Versioning, Deployment,
Documentation) and five quality gates (Test, Static Analysis, Security,
Review, Build). For each principle, there is documented evidence of
compliance or a remediation action item, recorded inline in the global
snapshot `specs/current-specs/spec.md` as a "Constitution Compliance"
section so it remains a living document.

**Why this priority**: While Principles I-V and quality gates are
broadly compliant based on initial assessment (handler/service
architecture follows SOLID patterns, middleware provides quality
attributes, CalVer versioning is in use, Helm/pipelines exist), no
formal verification or compliance documentation exists. This story
closes the audit loop.

**Independent Test**: Review the compliance report and verify each
principle/gate has either "compliant with evidence" or an active
remediation task.

**Acceptance Scenarios**:

1. **Given** the constitution has 6 principles, **When** the
   global snapshot's "Constitution Compliance" section is reviewed,
   **Then** each principle has a status (Compliant / Partial /
   Non-Compliant) with supporting evidence.
2. **Given** 5 quality gates are defined, **When** the CI pipeline
   configuration is inspected, **Then** each gate is either
   automated or has a documented manual process.
3. **Given** any principle rated "Partial" or "Non-Compliant",
   **When** the compliance section is reviewed, **Then** a
   remediation plan or accepted-risk justification is documented.
4. **Given** a future feature changes compliance status, **When**
   the global snapshot is updated, **Then** the "Constitution
   Compliance" section is updated in the same PR.

---

### Edge Cases

- What happens when a future feature spec contradicts the global
  snapshot? The global snapshot MUST be updated in the same or a
  follow-up PR, per the constitution.
- What happens when a deprecated endpoint is finally removed? The
  global snapshot MUST remove its contract entry and update the
  "Constitution Compliance" section in the same PR.
- What happens when a Liquibase changelog has multiple changesets
  in one file? Each changeset MUST have its own comment.
- What happens when a public type is internal to a feature but
  marked `public` for test access? It still requires documentation
  per the constitution.
- What if a quality gate cannot be fully automated (e.g., Review
  Gate)? Manual process documentation is acceptable per the
  constitution.
- How is Test-First satisfied for docs-only work? CI validation
  scripts that fail on missing documentation serve as the automated
  verification layer, consistent with Principle I's intent.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `specs/current-specs/` folder MUST be created with
  a consolidated `spec.md` organized by current capability (search,
  entity-details, documents, script-execution, Germany), using only
  the latest authoritative spec for each capability.
- **FR-002**: The `specs/current-specs/` folder MUST contain a unified
  `data-model.md` reflecting the current data entities
  (NormalizedSearchResult, EntityDetailResult, RegisteredAgentResult,
  CertificateResult, PartyResult, HistoryDocumentResult,
  GermanyBusinessResult, ExecuteScriptRequest/Response, etc.).
- **FR-003**: The `specs/current-specs/contracts/` folder MUST contain
  the current API contract definitions for all 5 active endpoints:
  execute-script (`POST /api/v1/execute-script`), definitions
  listing (`GET /api/v1/definitions`), document proxy
  (`GET /api/v1/documents/{blobPath}`), Germany hardcopy printout
  (`POST /api/v1/execute-script` with `de-hardcopy-printout`), and
  health check (`GET /health`). All 4 deprecated bespoke endpoints
  (`GET /api/v1/business-search`, `GET /api/v1/entity-details`,
  `GET /api/v1/germany-search`,
  `POST /api/v1/germany-search/current-hardcopy-printout`) MUST be
  included with `[DEPRECATED]` markers and migration notes pointing
  to `/api/v1/execute-script`.
- **FR-004**: All 120 public types in `src/BizScraper.Api/` MUST have
  XML `<summary>` type-level doc-comments (118 currently missing;
  2 already compliant: `BlobDownloadResult`, `IStateSearchScraper`).
  Method-level documentation is deferred to a follow-up feature;
  the compliance assessment (FR-007) MUST document this as a known
  Partial gap for Principle VI with a remediation plan.
- **FR-005**: All 8 Liquibase SQL changelog files MUST include a
  `comment` attribute on each changeset header (4 currently missing;
  4 already compliant).
- **FR-006**: The Angular test UI's public services and components
  SHOULD have JSDoc comments on exported classes and methods.
- **FR-007**: A formal compliance assessment MUST be performed for
  all 6 constitution principles, with status and evidence recorded
  inline in `specs/current-specs/spec.md` under a "Constitution
  Compliance" section.
- **FR-008**: Any principle rated "Partial" or "Non-Compliant" MUST
  have a documented remediation plan or explicit accepted-risk
  justification.
- **FR-009**: The 5 quality gates (Test, Static Analysis, Security,
  Review, Build) MUST be verified as present in the CI/CD pipeline
  or documented as requiring implementation.
- **FR-010**: CI validation scripts MUST be created to automatically
  verify: (a) all public C# types have XML doc-comments, (b) all
  Liquibase changesets have comment attributes, and (c) the
  `specs/current-specs/` folder exists with required files. These
  scripts satisfy Principle I (Test-First) for documentation-only
  deliverables.

### Key Entities

- **Global Specification Snapshot**: A consolidated set of files in
  `specs/current-specs/` representing the current system state.
  Contains: `spec.md` (merged capabilities), `data-model.md`
  (unified entities), `contracts/` (API schemas).
- **XML Doc-Comment**: A `/// <summary>` block preceding every
  public C# type, describing its purpose.
- **Liquibase Comment**: A `comment:` attribute in each SQL
  changeset header describing the migration's intent.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: `specs/current-specs/` exists and contains `spec.md`,
  `data-model.md`, and `contracts/` with at least one contract file.
- **SC-002**: 100% of public types in `src/BizScraper.Api/` have
  XML `<summary>` comments (currently 2 of 120; target 120 of 120).
- **SC-003**: All 8 Liquibase changelog files contain `comment`
  attributes on every changeset (currently 4 of 8; target 8 of 8).
- **SC-004**: The project builds with zero CS1591 warnings when
  `DocumentationFile` is enabled.
- **SC-005**: The global snapshot's "Constitution Compliance" section
  documents all 6 principles with compliance status and evidence.
- **SC-006**: All 5 quality gates are verified as automated or have
  documented manual processes in the compliance section.

## Assumptions

- The existing 16 feature specifications are accurate and can be
  consolidated without re-validating their content.
- XML doc-comments will follow standard C# `<summary>` conventions;
  no custom documentation tooling is required.
- Swagger UI is already configured and functional; all 9 endpoints
  already have `.WithName()`, `.WithTags()`, and `.Produces()`
  metadata (no remediation needed for endpoint registration).
- The Angular test UI documentation (FR-006) is a SHOULD requirement,
  not blocking the compliance audit.
- The consolidated global snapshot will use the latest authoritative
  definitions where features have been superseded (e.g., feature 016
  supersedes features 001/005 for business search).
- Principles I-V are expected to be broadly compliant based on
  initial audit; formal verification may reveal minor gaps requiring
  remediation rather than large-scale code changes.
- Documentation-only deliverables (XML comments, Liquibase comments,
  global snapshot) use CI validation scripts as the Test-First
  compliance mechanism, not traditional unit tests.
- This feature scopes Principle VI code documentation to type-level
  `<summary>` comments only. Method-level doc-comments (also a
  Principle VI MUST) are deferred to a follow-up feature and will
  be documented as a known Partial gap in the compliance assessment.
