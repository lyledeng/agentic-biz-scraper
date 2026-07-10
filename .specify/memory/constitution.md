<!--
  Sync Impact Report
  Version change: 1.1.2603.0 → 1.2.2603.0
  Modified principles: none
  Added sections:
    - Design Standards > Global Specification Snapshot (new)
  Removed sections: none
  Changes:
    - Added Global Specification Snapshot standard requiring a
      living specs/current-specs/ folder that consolidates the
      latest spec, data-model, and contracts from every feature.
  Templates requiring updates:
    - .specify/templates/plan-template.md ✅ no update needed
      (Constitution Check gate already covers new standards)
    - .specify/templates/spec-template.md ✅ no update needed
    - .specify/templates/tasks-template.md ✅ no update needed
      (Polish phase already includes documentation tasks;
       spec snapshot update is a post-merge workflow step)
  Follow-up TODOs:
    - Create initial specs/current-specs/ folder and seed it
      with consolidated snapshots from features 001–016.
-->

# bizscraper Constitution

## Core Principles

### I. Test-First (NON-NEGOTIABLE)

All development MUST follow a test-first (TDD) discipline using the
Red-Green-Refactor cycle:

- Tests MUST be written and approved before production code exists.
- Tests MUST fail (Red) before any implementation begins.
- Implementation MUST be the minimum code needed to pass (Green).
- Refactoring MUST occur only while all tests pass.
- Coverage MUST span the full test pyramid: unit, integration,
  contract, and end-to-end tests.
- No feature or bug-fix is considered complete until every layer of
  the pyramid has passing, relevant tests.

**Rationale**: Test-first development catches defects at the lowest
cost, produces inherently testable designs, and serves as living
documentation of intended behaviour.

### II. SOLID Design & Engineering Discipline

Production code MUST adhere to the SOLID principles and their
complementary practices:

- **Single Responsibility**: Each module, class, or function MUST have
  exactly one reason to change.
- **Open/Closed**: Components MUST be open for extension and closed
  for modification.
- **Liskov Substitution**: Subtypes MUST be substitutable for their
  base types without altering correctness.
- **Interface Segregation**: Clients MUST NOT be forced to depend on
  interfaces they do not use.
- **Dependency Inversion**: High-level modules MUST NOT depend on
  low-level modules; both MUST depend on abstractions.
- **DRY**: Duplicated knowledge MUST be extracted into a single
  authoritative source.
- **YAGNI**: Code MUST NOT be written for speculative future
  requirements.
- **KISS**: The simplest solution that satisfies current requirements
  MUST be preferred.
- **Separation of Concerns**: Distinct responsibilities MUST reside
  in distinct modules.
- **Composition over Inheritance**: Behaviour reuse MUST favour
  composition unless inheritance is the demonstrably simpler choice.

**Rationale**: These principles keep codebases maintainable,
extensible, and comprehensible as they grow.

### III. Architectural Quality Attributes

Every component MUST be designed and validated against the following
non-functional requirements:

- **Reliability**: The system MUST produce correct results under
  stated conditions and degrade gracefully under faults.
- **Resilience**: The system MUST recover from transient failures
  automatically (retries, circuit breakers, bulkheads).
- **Security**: Inputs MUST be validated at system boundaries; secrets
  MUST NOT be stored in source; the OWASP Top 10 MUST be mitigated.
- **Supportability**: Logs, metrics, and traces MUST be emitted in
  structured formats to enable rapid diagnosis.
- **Scalability**: Components MUST be stateless or explicitly manage
  state so that horizontal scaling is possible without redesign.
- **Auditability**: State-changing operations MUST produce an
  immutable audit trail with actor, action, timestamp, and outcome.
- **Self-Documentability**: Code MUST be clear enough to minimise
  the need for external documentation; public APIs MUST carry
  doc-comments.
- **Observability**: Systems MUST expose health checks, structured
  logs, distributed traces, and key business metrics.

**Rationale**: Quality attributes define whether the system is fit
for production; functional correctness alone is insufficient.

### IV. Hybrid SemVer+CalVer Versioning

All artifacts—code releases, documents, and API contracts—MUST use
the format **MAJOR.MINOR.YYMM.PATCH**:

- **MAJOR**: Incremented for backward-incompatible changes.
- **MINOR**: Incremented for backward-compatible feature additions.
- **YYMM**: Two-digit year + two-digit month of the release
  (e.g., `2603` = March 2026). This segment anchors every version
  to a calendar point and MUST reflect the actual release month.
- **PATCH**: Incremented for backward-compatible bug fixes within
  the same YYMM window.

Version precedence: MAJOR > MINOR > YYMM > PATCH.

Pre-release and build metadata MAY be appended using SemVer
conventions (e.g., `1.2.2603.0-beta.1`).

**Rationale**: Semantic segments communicate compatibility intent
while the calendar segment provides instant temporal context,
eliminating the need to consult changelogs for release timing.

### V. Hands-Free Deployment & Rollback

All deployments and rollbacks MUST be fully automated with zero
manual intervention:

- CI/CD pipelines MUST build, test, and deploy artifacts without
  human steps beyond the initial trigger (merge or tag).
- Rollback MUST be a single automated action (revert to prior
  known-good artifact) requiring no manual server access.
- Infrastructure changes MUST be expressed as code (IaC) and
  versioned alongside application code.
- Deployment configurations MUST NOT contain environment-specific
  secrets in plaintext; secrets MUST be injected at runtime from
  a secure vault.

**Rationale**: Manual deployment steps introduce variance, delay
incident response, and accumulate tribal knowledge that cannot be
reviewed or tested.

### VI. Documentation

All public-facing code, APIs, scripts, and database artifacts MUST
be documented:

- **API Documentation**: Every public API endpoint MUST have a
  corresponding OpenAPI/Swagger specification with descriptions,
  request/response schemas, error codes, and usage examples.
- **Code Documentation**: All public classes, methods, interfaces,
  and functions MUST carry doc-comments (e.g., `<summary>` in C#,
  JSDoc in TypeScript). Internal/private members SHOULD be
  documented when intent is non-obvious.
- **Script Documentation**: Every scraping definition, automation
  script, or infrastructure script MUST include a header or
  description field explaining its purpose, inputs, outputs, and
  any prerequisites.
- **Database Documentation**: All database schemas, migrations,
  and changelog files MUST document the purpose of each table,
  column, constraint, and migration step. Liquibase changesets
  MUST include a `comment` attribute.
- **README & Quickstart**: Each deployable component MUST maintain
  a README with setup instructions, environment requirements, and
  a quickstart guide sufficient for a new developer to run the
  component locally.
- **Living Documentation**: Documentation MUST be updated in the
  same pull request as the code change it describes. Stale
  documentation MUST be treated as a defect.

**Rationale**: Undocumented public interfaces create implicit
knowledge dependencies, slow onboarding, and increase the cost of
every future change. Documentation co-located with code ensures it
stays current and reviewable.

## Design Standards

The following standards complement the Core Principles at the
code and architecture level:

- **Error Handling**: Errors MUST be communicated through typed
  results or exceptions with structured context; generic catch-all
  handlers MUST NOT silently swallow errors.
- **Configuration**: Application configuration MUST follow the
  twelve-factor methodology (environment variables, external config)
  with sensible defaults and fail-fast on invalid values.
- **Dependencies**: Third-party dependencies MUST be pinned to
  exact versions in lock files; updates MUST pass the full test
  suite before adoption.
- **API Design**: Public APIs MUST be versioned, documented, and
  validated against contracts (OpenAPI, JSON Schema, or equivalent).
- **Global Specification Snapshot**: The `specs/current-specs/`
  folder MUST contain a consolidated, up-to-date snapshot of the
  project's current specifications at all times:
  - **Structure**: `specs/current-specs/` MUST mirror the latest
    state of the system. It contains a single `spec.md` (merged
    from all feature specs), a single `data-model.md` (unified
    data model), and a `contracts/` folder with the current API
    contracts.
  - **Update Trigger**: Whenever a new feature specification is
    completed (i.e., its `spec.md`, `data-model.md`, or contracts
    are finalized), the global snapshot MUST be updated in the
    same pull request or a dedicated follow-up PR before the
    next feature begins.
  - **Conflict Resolution**: When a new feature modifies or
    supersedes an earlier feature's models or contracts, the
    global snapshot MUST reflect only the current, authoritative
    definitions—not the historical ones.
  - **Single Source of Truth**: The global snapshot serves as the
    canonical reference for the project's current API surface,
    data model, and behavioural contracts. Individual feature
    specs under `specs/NNN-feature/` remain the audit trail of
    how the system evolved.

## Quality Gates & Development Workflow

All changes MUST pass through the following gates before merge:

1. **Test Gate**: The full test pyramid MUST pass. New code MUST
   include tests at the appropriate pyramid layer(s). Coverage
   MUST NOT decrease.
2. **Static Analysis Gate**: Linting, formatting, and type checks
   MUST pass with zero warnings treated as errors.
3. **Security Gate**: Dependency vulnerability scans and SAST checks
   MUST report no critical or high findings.
4. **Review Gate**: At least one peer review MUST approve the change.
   Reviewers MUST verify compliance with this constitution.
5. **Build Gate**: A reproducible build producing a versioned,
   deployable artifact MUST complete successfully.

Pull requests MUST reference the relevant spec or task identifier.
Commits MUST follow Conventional Commits format.

## Governance

This constitution is the authoritative source of engineering
standards for the bizscraper project. It supersedes any
conflicting guidance in other documents or tools.

- **Supremacy**: Where conflict exists between this constitution
  and any other project document, this constitution prevails.
- **Compliance Verification**: Every pull request and code review
  MUST verify adherence to these principles. Non-compliance MUST
  be resolved before merge.
- **Justified Complexity**: Any deviation from KISS or YAGNI MUST
  be documented with a rationale and approved during review.
- **Amendment Procedure**: Amendments MUST be proposed as a pull
  request modifying this file, reviewed by at least one maintainer,
  and merged only after approval. The Sync Impact Report (HTML
  comment at the top of this file) MUST be updated with every
  amendment.
- **Versioning Policy**: This constitution follows the same
  MAJOR.MINOR.YYMM.PATCH scheme defined in Principle IV.
  MAJOR for principle removals or incompatible redefinitions,
  MINOR for new principles or material expansions, PATCH for
  clarifications and wording fixes.

**Version**: 1.2.2603.0 | **Ratified**: 2026-03-04 | **Last Amended**: 2026-03-29
