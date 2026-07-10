# Research: Test-UI And Spec Consistency Alignment

**Feature**: 023-test-ui-spec-alignment  
**Date**: 2026-04-01  
**Phase**: 0 - Research & Clarification

## Objective

Resolve all critical/high/medium consistency findings from prior analysis and align test-ui behavior with the canonical execute-script specification.

## Inputs Reviewed

- `specs/022-iowa-entity-search/spec.md`
- `specs/022-iowa-entity-search/tasks.md`
- `specs/022-iowa-entity-search/plan.md`
- `src/BizScraper.Api/Features/ExecuteScript/*`
- `src/test-ui/src/app/**/*`
- `.specify/memory/constitution.md`

## Decision Log

### Decision 1: Canonical request shape is `definition` + `parameters`

- **Decision**: Standardize all API examples, test-ui payloads, and contracts on:
  - `definition: string`
  - `parameters: object`
- **Rationale**: Backend request model (`ExecuteScriptRequest`) already enforces these field names; using aliases (`scriptName`) in specs/UI introduces avoidable integration bugs.
- **Alternatives considered**:
  - Keep dual aliases (`scriptName` and `definition`) for compatibility: rejected due to ambiguity and extra branching.
  - Rename backend to `scriptName`: rejected because it would create larger breaking change across existing tests and docs.

### Decision 2: Iowa detail flow uses `us-ia-entity-details`

- **Decision**: Use `us-ia-entity-details` for detail requests and `us-ia-business-search` for search requests.
- **Rationale**: Matches existing slug semantics (`*-business-search` vs `*-entity-details`) and mapper dispatch logic in backend.
- **Alternatives considered**:
  - Reuse search slug for details with conditional parameters: rejected due to role confusion and harder validation.

### Decision 3: Response envelope must be explicitly specified

- **Decision**: Define canonical envelope fields and where search-only metadata applies.
- **Rationale**: Prior medium finding showed envelope field expectations were implied but not concretely testable.
- **Alternatives considered**:
  - Leave envelope behavior implementation-defined: rejected due to poor contract testability.

### Decision 4: test-ui should be contract-first

- **Decision**: Update test-ui service/model interfaces and feature components to use canonical request and envelope contracts.
- **Rationale**: test-ui is a primary verification surface; drift from API contract causes false negatives and debugging noise.
- **Alternatives considered**:
  - Keep ad-hoc payload construction in components: rejected because it duplicates and fragments request contract logic.

### Decision 5: Snapshot updates are mandatory in delivery workflow

- **Decision**: Make updates to `specs/current-specs/spec.md`, `specs/current-specs/data-model.md`, and impacted `specs/current-specs/contracts/*` explicit planning/task outputs.
- **Rationale**: Constitution defines this as a MUST; omission was previously a critical finding.
- **Alternatives considered**:
  - Treat snapshot updates as optional post-merge housekeeping: rejected as constitution non-compliance.

## Best Practices Confirmed

1. Keep slug and parameter validation centralized in API layer (`ExecuteScriptValidator`, handler decode logic).
2. Keep test-ui data contracts in shared models (`src/test-ui/src/app/shared/models/*`) with one source of truth.
3. Use integration tests for cross-state schema consistency and unit tests for mapper behavior.
4. Treat docs and contracts as versioned artifacts updated in same PR as behavior changes.

## Open Questions Status

No unresolved clarifications remain.

## Research Outcome

All previously identified critical/high/medium consistency issues can be addressed through:

- Spec and contract normalization
- test-ui contract alignment
- Explicit plan/tasks coverage for envelope, TLS, diagnostics, and snapshot maintenance
