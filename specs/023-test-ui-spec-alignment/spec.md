# Feature Specification: Test-UI And Spec Consistency Alignment

**Feature Branch**: `023-test-ui-spec-alignment`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "add updating the test-ui to the current spec. Also resolve all the critical, high and mediums found by /speckit.analyze"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Canonical Execute-Script Contract (Priority: P1)

As an API consumer, I want one unambiguous request and response contract for execute-script across IA/CO/WY, so I can integrate once without parameter-name or definition-name branching.

**Why this priority**: The highest-risk issues found by analysis were contract inconsistencies that can break client integrations.

**Independent Test**: Submit search and detail requests to `/api/v2/execute-script` using the canonical request shape and verify all states deserialize through the same envelope and normalized models.

**Acceptance Scenarios**:

1. **Given** a client sends `definition=us-ia-business-search` with `parameters.searchTerm`, **When** the request is processed, **Then** the search succeeds without requiring `name` or other alternate parameter keys.
2. **Given** a client sends `definition=us-ia-entity-details` with `parameters.uniqueKey`, **When** the request is processed, **Then** the detail flow returns the extended detail profile for the matching entity.
3. **Given** IA, CO, and WY execute-script calls, **When** responses are returned, **Then** the envelope and normalized fields are consistent and usable by one client model.

---

### User Story 2 - Test-UI Alignment With Current Spec (Priority: P1)

As a QA or developer user, I want the test-ui to match the current execute-script specification, so I can reliably validate search/detail behavior and troubleshoot failures.

**Why this priority**: test-ui is the fastest verification surface and must reflect the same canonical contract defined by the spec.

**Independent Test**: Use the test-ui to run IA search and drill-down; confirm payloads use canonical fields and rendered output shows required metadata and certificate-note behavior.

**Acceptance Scenarios**:

1. **Given** a user enters an Iowa search term in test-ui, **When** the request is sent, **Then** test-ui sends canonical execute-script payload fields and displays normalized search rows.
2. **Given** a user clicks drill-down in test-ui, **When** detail request is sent with `uniqueKey`, **Then** test-ui renders extended detail fields including certificate note.
3. **Given** a busy or invalid request response (400/503), **When** test-ui receives it, **Then** it shows actionable error details including retry guidance where present.

---

### User Story 3 - Governance And Snapshot Consistency (Priority: P2)

As a maintainer, I want planning/tasks/spec artifacts to be aligned with constitution rules, including current-specs snapshot maintenance, so that documentation remains a reliable source of truth.

**Why this priority**: Constitution non-compliance was flagged as a critical issue and can cause drift between feature artifacts and project-level canonical specs.

**Independent Test**: Verify the feature checklist/tasks include explicit updates to `specs/current-specs/` and all identified critical/high/medium inconsistencies are resolved in spec language.

**Acceptance Scenarios**:

1. **Given** this feature spec is finalized, **When** governance checks run, **Then** no critical/high/medium inconsistencies remain from the prior analysis report.
2. **Given** this feature introduces contract or model updates, **When** work is completed, **Then** `specs/current-specs/spec.md`, `specs/current-specs/data-model.md`, and affected contracts are updated in the same implementation cycle.

---

### Edge Cases

- What happens when a request still uses legacy `name` parameter instead of canonical `searchTerm`? The system should return a clear validation error and test-ui should surface exact correction guidance.
- What happens when `uniqueKey` is malformed or mismatched to definition state? The system should reject the request with a deterministic validation error.
- What happens when Iowa SOS HTML changes and extraction partially fails? The system should still return diagnosable errors with correlation and artifact references.
- What happens when Iowa requests hit concurrency limits? The system should return 503 with Retry-After and test-ui should display retry guidance.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The canonical execute-script request format MUST use `definition` and `parameters` fields; all specification examples MUST follow this format.
- **FR-002**: Iowa search MUST use definition `us-ia-business-search` with mandatory runtime parameter `searchTerm`.
- **FR-003**: Iowa drill-down MUST use definition `us-ia-entity-details` with mandatory runtime parameter `uniqueKey`.
- **FR-004**: The specification MUST remove parameter-name ambiguity by consistently using `searchTerm` for search and MUST NOT define alternate required search keys in normative requirements.
- **FR-005**: Entity naming MUST be consistent across spec, data model, and tasks for Iowa search and detail entities.
- **FR-006**: All execute-script responses MUST include canonical envelope fields `definition`, `correlationId`, `truncated`, and `data`. Search responses MAY additionally include metadata fields `results`, `totalCount`, `siteTotalCount`, `pagesScraped`, `searchTerm`, `state`, and `executionTime` where applicable.
- **FR-007**: Iowa detail responses MUST include extended entity fields: identifier, legal name, status, entity type, state of incorporation, modified, filing/effective/expiration dates, chapter/code, names list, registered agent, principal office, and certificate note.
- **FR-008**: Certificate limitation MUST be represented as a mandatory `certificateNote` field in Iowa detail responses.
- **FR-009**: Diagnostic behavior requirements MUST explicitly include screenshots and tracing checkpoints for page load, search results, pagination, detail pages, and error paths.
- **FR-010**: Local development behavior MUST explicitly require TLS-certificate-error tolerance for Iowa scraping execution.
- **FR-011**: Concurrency behavior MUST explicitly require a per-state IA limit of one concurrent request and 503 Retry-After semantics for excess requests.
- **FR-012**: Audit requirements MUST explicitly record definition slug per operation for `us-ia-business-search` and `us-ia-entity-details`.
- **FR-013**: test-ui behavior MUST be updated to use canonical request fields, support IA search/detail execution, and display normalized results plus error guidance.
- **FR-014**: Plan and task artifacts MUST use runtime-accurate definition storage paths and avoid conflicting source-structure descriptions.
- **FR-015**: The implementation cycle for this feature MUST include updating `specs/current-specs/` consolidated spec, data model, and impacted contracts.

### Key Entities *(include if feature involves data)*

- **ExecuteScriptCanonicalRequest**: Canonical request with `definition` and `parameters`; used by API consumers and test-ui.
- **IowaSearchResult**: Normalized Iowa search row with identifier, name, status, entityType, formationDate, uniqueKey, state, and optional Iowa-specific extensions.
- **IowaEntityDetail**: Extended Iowa detail object containing business profile, names, agent, principal office, dates, chapter/code, and `certificateNote`.
- **ExecuteScriptCanonicalEnvelope**: Unified response envelope with correlation metadata, truncation semantics, and canonical data payload shape.
- **CurrentSpecsSnapshot**: Consolidated project-level artifacts under `specs/current-specs/` representing current authoritative behavior.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of request/response examples in this feature spec use one canonical execute-script contract (`definition` + `parameters`) with no conflicting parameter names.
- **SC-002**: All critical/high/medium findings from the prior analysis are resolved in this feature’s specification artifacts before implementation starts.
- **SC-003**: test-ui can execute IA search and IA detail drill-down flows end-to-end using canonical payloads without manual payload edits.
- **SC-004**: For IA search/detail error cases (400, 503), test-ui displays actionable messages and retry guidance where applicable.
- **SC-005**: Constitution compliance check passes for global snapshot requirements with explicit update tasks for `specs/current-specs/`.

## Assumptions

- Existing `/api/v2/execute-script` endpoint remains the integration surface for IA/CO/WY.
- Existing scraper infrastructure and definition loader continue to read definitions from runtime `Infrastructure/Scraping/Definitions` output path.
- test-ui already has an execute-script workflow that can be extended rather than rebuilt.
- CO and WY behavior should remain backward compatible while IA alignment and contract consistency updates are introduced.
