# Feature Specification: Semantic Error Handling for Colorado Exceeded Record Count

**Feature Branch**: `012-co-record-count-error`  
**Created**: 2026-03-27  
**Status**: Draft  
**Input**: User description: "When the colorado 'record count exceeds', system should not retry. System should return this error in semantic form."

## Clarifications

### Session 2026-03-27

- Q: Should the no-retry/no-wrap mechanism apply to all `onMatch=throw` check-text matches (generic) or only `exceeded-record-count` (specific)? → A: All `onMatch=throw` check-text matches bypass retry — they are business-rule violations by definition.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Semantic Error Response for Too-Broad Search (Priority: P1)

When a user performs a Colorado business search with a term that is too broad (e.g., a single common letter), the Colorado Secretary of State website reports "Exceeded Record Count." The system must immediately stop processing (no retries) and return a clear, actionable error response that tells the user to refine their search term.

**Why this priority**: This is the core value of the feature — users currently receive a generic 500 error after wasting time on pointless retries. They have no idea what went wrong or how to fix it.

**Independent Test**: Can be fully tested by sending a broad search term (e.g., "a") to `GET /api/v1/business-search?searchTerm=a&state=CO` and verifying the response is a semantic 422 error with a clear message.

**Acceptance Scenarios**:

1. **Given** a user searches for a very broad term on the Colorado state, **When** the upstream CO SOS site returns "Exceeded Record Count", **Then** the system returns HTTP 422 with an RFC 9457 Problem Details response containing type `exceeded-record-count`, a human-readable title, and a detail message instructing the user to refine their search.
2. **Given** the same scenario, **When** the upstream site returns "Exceeded Record Count", **Then** the system does NOT retry the request — there is exactly one attempt.
3. **Given** a user searches for a broad term, **When** the system returns the 422 response, **Then** the response is returned within a few seconds (no retry delay accumulation).

---

### User Story 2 - No-Retry for Non-Retryable Scraping Errors (Priority: P2)

The scraping engine's retry pipeline must distinguish between transient errors (network failures, timeouts) that are worth retrying and deterministic business-rule errors (like "Exceeded Record Count") that will produce the same outcome on every retry. Non-retryable errors must bypass the retry pipeline entirely.

**Why this priority**: Retrying a deterministic error wastes compute resources, delays the user, and creates unnecessary load on the upstream site.

**Independent Test**: Can be tested by verifying that when a non-retryable business-rule error is thrown during scraping, the Polly retry pipeline does not re-execute the operation.

**Acceptance Scenarios**:

1. **Given** the scraping engine encounters an "Exceeded Record Count" check-text match, **When** the exception propagates to the retry pipeline, **Then** the pipeline does not retry and the exception surfaces immediately.
2. **Given** a transient error occurs during scraping (e.g., network timeout), **When** the exception propagates to the retry pipeline, **Then** the pipeline retries according to configured policy (existing behavior preserved).

---

### User Story 3 - Audit Trail Records Exceeded Record Count (Priority: P3)

When the exceeded record count error occurs, the audit trail must record the event with a specific status code so operators can identify how frequently users hit this limit and with which search terms.

**Why this priority**: Operational visibility into how often users encounter this error informs UX improvements and search guidance.

**Independent Test**: Can be tested by triggering the exceeded record count error and querying the audit trail for a record with status "ExceededRecordCount".

**Acceptance Scenarios**:

1. **Given** a search triggers the exceeded record count error, **When** the error is returned to the user, **Then** an audit trail entry is written with status "ExceededRecordCount", the search term, correlation ID, and the error detail message.

---

### Edge Cases

- What happens when a non-Colorado state search produces a similar text pattern? The error handling is scoped to flows that define the `exceeded-record-count` error type; other states are unaffected.
- What happens if the check-text pattern matches but the error type is different? All `onMatch=throw` check-text matches are treated as non-retryable business-rule violations regardless of error type. Each error type maps to its own semantic exception and HTTP response via the global exception handler.
- What happens if the upstream site changes the "Exceeded Record Count" text? The check-text pattern is defined in the flow JSON configuration file, not hard-coded. Updating the pattern is a configuration change.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST return HTTP 422 (Unprocessable Entity) with RFC 9457 Problem Details when the Colorado SOS site reports "Exceeded Record Count".
- **FR-002**: The Problem Details response MUST include `type: "https://BizScraper.local/errors/exceeded-record-count"`, `title: "Exceeded Record Count"`, and a `detail` message instructing the user to narrow their search.
- **FR-003**: System MUST NOT retry the search operation when the "Exceeded Record Count" condition is detected. The error must be treated as deterministic and non-retryable.
- **FR-004**: Any exception originating from a check-text action with `onMatch=throw` MUST propagate through the exception handling chain without being wrapped in a generic exception that masks its identity. All such exceptions are business-rule violations and are inherently non-retryable.
- **FR-005**: The retry pipeline MUST exclude all check-text business-rule exceptions (any `CheckTextMatchException`) from its retry predicate while continuing to retry transient errors.
- **FR-006**: The audit trail MUST record exceeded record count errors with status "ExceededRecordCount", preserving the search term, correlation ID, and error detail.
- **FR-007**: Existing retry behavior for transient errors (browser failures, timeouts) MUST remain unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A broad Colorado search returns a 422 response with "Exceeded Record Count" within 10 seconds (no accumulated retry delay).
- **SC-002**: Zero retries are attempted when the exceeded record count condition is detected — exactly one scraping attempt per request.
- **SC-003**: The error response is parseable as RFC 9457 Problem Details with content type `application/problem+json`.
- **SC-004**: Audit trail entries for exceeded record count errors have status "ExceededRecordCount" and include the user's search term.
- **SC-005**: All existing unit and integration tests continue to pass (no regression in transient error retry behavior).

## Assumptions

- The `exceeded-record-count` error type in the check-text action is the canonical signal for this condition, as defined in the Colorado business-search flow JSON configuration.
- The `ExceededRecordCountException` type and its handling in the global exception handler already exist in the codebase but are currently unreachable due to exception wrapping in the engine.
- The fix involves correcting the exception propagation path so that the existing error-handling infrastructure becomes functional.
- The 422 status code is appropriate because the request is syntactically valid but the search term is too broad to process — this is an unprocessable business constraint, not a server fault.
- Only the Colorado business search flow currently defines the "Exceeded Record Count" pattern; this feature does not need to account for other states.
