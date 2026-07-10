# Feature Specification: Comprehensive Action Logging for Scraping Engine

**Feature Branch**: `026-action-logging`  
**Created**: 2026-04-02  
**Status**: Draft  
**Input**: User description: "I want to enhance the web scraping engine. Every action performed in the json file should be logged to the logger."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Action-Level Observability for Debugging (Priority: P1)

As a developer debugging a failing or misbehaving scraping flow, I need every action executed from a JSON definition to emit structured log entries so I can trace the exact sequence of operations, identify which action failed, and see the context-specific details of each step.

**Why this priority**: Without action-level logging, diagnosing production issues requires reproducing the failure locally. Comprehensive logging enables remote diagnosis and reduces mean time to resolution.

**Independent Test**: Run any scraping definition (e.g., `us-co-business-search`) and verify that every action in the JSON file produces a log entry with action-specific details visible in the console output.

**Acceptance Scenarios**:

1. **Given** a scraping definition with a `navigate` action, **When** the engine executes that action, **Then** a log entry is emitted containing the target URL and the wait condition.
2. **Given** a scraping definition with a `fill` action using a non-sensitive variable (e.g., `searchTerm` with `sensitive: false`), **When** the engine executes that action, **Then** a log entry is emitted containing the target selector description and the full resolved value.
3. **Given** a scraping definition with a `click` action, **When** the engine executes that action, **Then** a log entry is emitted containing the target selector description and any post-click wait condition.
4. **Given** a scraping definition with an `extract` action, **When** the engine executes that action, **Then** a log entry is emitted containing the script reference name and the variable the result is stored in.
5. **Given** a scraping definition with a `check-text` action, **When** the engine executes that action, **Then** a log entry is emitted containing the pattern being checked and whether a match was found.
6. **Given** a scraping definition with a `download` action, **When** the engine executes that action, **Then** a log entry is emitted containing the filename and the storage destination.
7. **Given** a scraping definition with a `loop` action, **When** the engine executes iterations, **Then** log entries are emitted for each iteration with the iteration number and aggregation status.
8. **Given** a scraping definition with a `screenshot` action, **When** the engine executes that action, **Then** a log entry is emitted containing the milestone name.
9. **Given** a scraping definition with a `wait-for-load` or `wait-for-condition` action, **When** the engine executes that action, **Then** a log entry is emitted containing the wait target and outcome.
10. **Given** a scraping definition with a `call-service` action, **When** the engine executes that action, **Then** a log entry is emitted containing the service name and the input/output variable names.

---

### User Story 2 - Consistent Log Correlation Across Actions (Priority: P2)

As an operations engineer reviewing logs from multiple concurrent scraping requests, I need every action log entry to include the correlation ID and flow identity so I can filter and reconstruct the execution timeline for a specific request.

**Why this priority**: Concurrent request tracing is critical in production but builds on top of the per-action logging from P1.

**Independent Test**: Execute two concurrent scraping requests and verify that log entries for each request can be isolated by filtering on correlation ID.

**Acceptance Scenarios**:

1. **Given** multiple concurrent scraping flows, **When** actions are logged, **Then** each log entry includes the correlation ID and definition name, enabling log filtering by request.
2. **Given** a loop action with nested sub-actions, **When** the nested actions are logged, **Then** they include the same correlation ID as the parent flow and indicate they are within a loop iteration.

---

### User Story 3 - Sensitive Data Protection in Logs (Priority: P2)

As a security-conscious developer, I need action logging to redact or omit sensitive values (e.g., form input contents, captcha solutions) so that logs do not inadvertently expose user data or secrets.

**Why this priority**: Logging must not compromise data privacy; sensitive values must never appear in plaintext in log output.

**Independent Test**: Run a scraping flow that fills a form field with sensitive data and verify the log entry contains the value length but not the actual value.

**Acceptance Scenarios**:

1. **Given** a `fill` action resolving a variable marked `sensitive: false`, **When** the action is logged, **Then** the log contains the full resolved value for debugging.
2. **Given** a `fill` action resolving a variable marked `sensitive: true`, **When** the action is logged, **Then** the log contains the character count of the value but not the value itself.
3. **Given** a `call-service` action solving a captcha, **When** the action is logged, **Then** the log contains the service name and result variable but not the captcha solution text.
4. **Given** variables marked as `sensitive: true` in the definition, **When** their values appear in action parameters, **Then** the logged output replaces the value with a redacted placeholder.

---

### Edge Cases

- What happens when an action has a null or missing `id` or `description`? Log entries should gracefully handle null fields with fallback indicators (e.g., `(no id)`, `(no description)`).
- What happens during retry attempts? Each retry should produce a log entry indicating the attempt number.
- What happens when a loop terminates early due to a condition? The termination should be logged with the reason.
- What happens when a `check-text` pattern matches and the `onMatch` behavior is `skip` vs `throw`? The log should indicate both the match and the resulting action.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST emit a structured log entry at `Information` level for every action executed from a JSON scraping definition, by promoting and enriching the existing lifecycle log methods (`ActionStarted`, `ActionCompleted`, `ActionSkipped`, `ActionFailed`) with action-specific details. There MUST be exactly one log line per action lifecycle event (no separate companion entries).
- **FR-002**: System MUST log action-specific contextual details per action type:
  - `navigate`: target URL, wait-until condition
  - `fill`: target selector description, resolved value (full when `sensitive: false`, character count when `sensitive: true`)
  - `click`: target selector description, wait-after condition
  - `extract`: script reference name, store-as variable name, post-processor name (if any)
  - `check-text`: search pattern, match result (found/not found), on-match behavior
  - `download`: link selector, filename pattern, storage destination path (in completion details)
  - `loop`: max iterations, aggregation variable (pre-execution); iteration number and termination reason (per-iteration handler logging)
  - `screenshot`: milestone name, force flag
  - `wait-for-load`: target load state
  - `wait-for-condition`: condition expression summary, timeout value, outcome (met/timed out)
  - `call-service`: service name, input variable, output variable
- **FR-003**: System MUST include the correlation ID in every action-level log entry to enable request-level log filtering.
- **FR-004**: System MUST NOT log sensitive data in plaintext. Redaction is governed by the `sensitive` flag on variables in the JSON definition: values resolved from variables marked `sensitive: true` MUST be redacted (represented by character count only); values from variables marked `sensitive: false` (e.g., search terms) MAY be logged in full for debuggability.
- **FR-005**: System MUST log retry attempts with the attempt number, delay, and action identity when action-level retries are triggered.
- **FR-006**: System MUST log when an action is skipped due to a condition evaluation, including the condition that was not met.
- **FR-007**: System MUST use source-generated log methods for all new log entries, consistent with the existing `[LoggerMessage]` pattern used throughout the codebase.
- **FR-008**: System MUST include elapsed execution time (in milliseconds) in every `ActionCompleted` log entry to enable per-action performance analysis without external profiling tools.

### Key Entities

- **Action Log Entry**: A structured log record containing action identity (ID, type, order), flow context (correlation ID, definition name), action-specific details, and a timestamp.
- **Scraping Definition**: The JSON file describing the sequence of actions to be executed by the engine, which serves as the source of all actions to be logged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of action types (all 11 handler types) emit at least one structured log entry per execution.
- **SC-002**: A developer can reconstruct the full action-by-action execution timeline of any scraping request from log output alone, without access to the source code.
- **SC-003**: No values from `sensitive: true` variables (captcha solutions, credentials) appear in log output; non-sensitive values (search terms) are logged in full.
- **SC-004**: Log entries for concurrent requests can be isolated by correlation ID with zero cross-contamination.
- **SC-005**: All new log methods use compile-time source generation (`[LoggerMessage]`), with zero runtime string formatting or interpolation in log calls.
- **SC-006**: Every `ActionCompleted` log entry includes elapsed milliseconds, enabling identification of actions exceeding expected duration thresholds.

## Clarifications

### Session 2026-04-02

- Q: Should new action-specific logs be separate entries alongside existing Debug lifecycle logs, or should the existing lifecycle logs be promoted and enriched? → A: Promote existing lifecycle logs (`ActionStarted`, `ActionCompleted`, `ActionSkipped`, `ActionFailed`) from `Debug` to `Information` and enrich them with action-specific details. One log line per action lifecycle event; no duplication.
- Q: Should `fill` values always be redacted (log length only), or should redaction respect the `sensitive` flag on the variable? → A: Respect the `sensitive` flag on the variable being filled. Log the resolved value in full when `sensitive: false`; redact to character count when `sensitive: true`.
- Q: Should the enriched `ActionCompleted` log include elapsed execution time for each action? → A: Yes. Include elapsed time in milliseconds in every `ActionCompleted` log entry to identify performance bottlenecks without external profiling.

## Assumptions

- The existing `EngineLogging` static partial class is the designated location for source-generated log methods for the scraping engine.
- The existing `ActionStarted`, `ActionCompleted`, `ActionSkipped`, and `ActionFailed` log methods will be promoted from `Debug` to `Information` level and enriched with action-specific details; they are the single logging surface per action lifecycle event (no separate detail logs alongside them).
- The `sensitive` flag on variables in the JSON definition is the sole authoritative indicator for redaction. `fill` values are NOT blanket-redacted; only values resolved from `sensitive: true` variables are redacted.
- The scraping engine's existing architecture (action handlers implementing `IActionHandler`) is the extension point for adding per-handler logging.
