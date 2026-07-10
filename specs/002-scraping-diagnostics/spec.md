# Feature Specification: Scraping Diagnostics — Traces & Screenshots

**Feature Branch**: `002-scraping-diagnostics`  
**Created**: 2026-03-24  
**Status**: Draft  
**Input**: User description: "I want to capture traces and screenshots of the web scraping action"

## Clarifications

### Session 2026-03-24

- Q: What should the default tracing mode be when no configuration is explicitly set? → A: "always" (trace every request by default)
- Q: Should screenshot capture default to enabled or disabled? → A: Enabled (capture milestone screenshots unless explicitly disabled)
- Q: Should Playwright traces include embedded screenshots for timeline playback? → A: Yes, traces include periodic screenshots inside the .zip for self-contained debugging

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Retrieve Playwright Trace for a Failed Search (Priority: P1)

As an operator investigating a scraping failure, I want the system to automatically capture a Playwright trace of the entire browser session so that I can replay the trace in the Playwright Trace Viewer and diagnose what went wrong.

**Why this priority**: When scraping breaks (selectors change, the upstream site returns unexpected markup, timeouts occur), the trace file is the single most valuable diagnostic artifact because it captures DOM snapshots, network activity, and console logs in a replayable format.

**Independent Test**: Trigger a search that results in an error (e.g., upstream timeout). Confirm a `.zip` trace file is written to the configured output path. Open it in Playwright Trace Viewer and verify it replays the session.

**Acceptance Scenarios**:

1. **Given** a scraping request that fails with any exception, **When** the scraper completes the browser session, **Then** a Playwright trace archive (`.zip`) is persisted to the configured diagnostics output path with a filename that includes the correlation ID and timestamp.
2. **Given** a scraping request that fails, **When** the trace is saved, **Then** the trace file contains the full session from page navigation through the point of failure (DOM snapshots, network events, console output).
3. **Given** tracing is disabled via configuration, **When** a scraping request fails, **Then** no trace file is written and no performance overhead is incurred.

---

### User Story 2 — Retrieve Playwright Trace for a Successful Search (Priority: P2)

As an operator performing routine monitoring, I want the option to capture Playwright traces for successful searches so that I can proactively detect upstream site changes before they cause failures.

**Why this priority**: Proactive diagnostics are valuable but less urgent than failure diagnostics. Most teams enable this selectively in staging or for sampled production traffic.

**Independent Test**: Enable tracing for all requests via configuration. Perform a successful search. Confirm a trace file is written and is replayable.

**Acceptance Scenarios**:

1. **Given** tracing is configured to capture on all requests, **When** a search completes successfully, **Then** a trace archive is persisted to the configured output path.
2. **Given** tracing is configured to capture only on failure, **When** a search completes successfully, **Then** no trace file is written.

---

### User Story 3 — Capture Screenshots at Key Scraping Milestones (Priority: P2)

As an operator reviewing a scraping session, I want the system to capture screenshots at key milestones during the scraping workflow so that I can quickly see what the browser rendered at each step without replaying an entire trace.

**Why this priority**: Screenshots are lighter-weight than full traces and provide immediate visual confirmation of what the scraper saw. They complement traces for quick triage.

**Independent Test**: Enable screenshot capture via configuration. Perform a search. Confirm screenshots are saved for each configured milestone (e.g., after page load, after search submit, after each paginated page).

**Acceptance Scenarios**:

1. **Given** screenshot capture is enabled, **When** the scraper navigates to the search page, **Then** a screenshot is saved after the page reaches network-idle state.
2. **Given** screenshot capture is enabled, **When** the scraper submits the search form, **Then** a screenshot is saved after search results load.
3. **Given** screenshot capture is enabled and results span multiple pages, **When** the scraper navigates to the next page, **Then** a screenshot is saved after each paginated results page loads.
4. **Given** a scraping request fails, **When** the failure occurs, **Then** a screenshot is captured at the point of failure regardless of the screenshot configuration setting.
5. **Given** screenshot capture is disabled, **When** a search completes successfully, **Then** no screenshots are written.

---

### User Story 4 — Configure Diagnostics Behavior (Priority: P3)

As a system administrator, I want to control diagnostic capture behavior through configuration so that I can balance diagnostic detail against storage costs and performance impact.

**Why this priority**: Configuration flexibility is important for production readiness but depends on the core capture functionality being in place first.

**Independent Test**: Change configuration values (output path, tracing mode, screenshot toggle). Restart the application. Verify the new settings take effect without code changes.

**Acceptance Scenarios**:

1. **Given** the diagnostics output path is configured, **When** diagnostic artifacts are produced, **Then** they are written to the specified path.
2. **Given** a tracing mode of "on-failure" is configured, **When** a successful search completes, **Then** no trace is saved; **When** a failed search completes, **Then** a trace is saved.
3. **Given** a tracing mode of "always" is configured, **When** any search completes, **Then** a trace is saved.
4. **Given** a tracing mode of "off" is configured, **When** any search completes, **Then** no trace is saved.

---

### Edge Cases

- What happens when the diagnostics output directory does not exist? The system should create it automatically.
- What happens when the disk is full or the write fails? The system should log a warning and continue — diagnostic failures must never cause the primary scraping operation to fail.
- What happens when multiple concurrent requests generate diagnostics? Each request's artifacts must be isolated (unique filenames using correlation ID + timestamp).
- What happens when traces or screenshots accumulate over time? The system does not manage cleanup — this is an external operational concern (out of scope).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support Playwright tracing that captures DOM snapshots, network activity, console logs, and embedded periodic screenshots for a browser session. The trace `.zip` MUST be self-contained for debugging without requiring separate screenshot files.
- **FR-002**: System MUST support capturing screenshots as PNG images at key milestones during the scraping workflow (page load, search submit, each paginated page, and on failure).
- **FR-003**: System MUST persist diagnostic artifacts (traces and screenshots) to a configurable output directory on the local filesystem.
- **FR-004**: System MUST name diagnostic artifact files using a convention that includes the correlation ID and a UTC timestamp, making each file uniquely identifiable and sortable.
- **FR-005**: System MUST support three tracing modes configurable at startup: "off" (no tracing), "on-failure" (trace only when scraping fails), and "always" (trace every request). The default mode when no configuration is provided MUST be "always".
- **FR-006**: System MUST support enabling or disabling screenshot capture independently from tracing via a configuration toggle. The default when no configuration is provided MUST be enabled.
- **FR-007**: System MUST always capture a screenshot at the point of failure when an exception occurs during scraping, regardless of the screenshot configuration toggle.
- **FR-008**: System MUST NOT allow diagnostic capture failures (I/O errors, permission issues) to propagate as exceptions to the caller — the primary search response must still be returned.
- **FR-009**: System MUST log a warning when a diagnostic artifact fails to be saved.
- **FR-010**: System MUST create the diagnostics output directory if it does not already exist.
- **FR-011**: Tracing and screenshot capture MUST start before the first navigation and end after the last action in the browser context, ensuring the full session is captured.

### Key Entities

- **Diagnostic Artifact**: A file (trace `.zip` or screenshot `.png`) produced during a scraping session, identified by correlation ID and timestamp.
- **Tracing Mode**: A configuration value ("off", "on-failure", "always") that governs when Playwright traces are captured.
- **Scraping Milestone**: A defined point in the scraping workflow where a screenshot is taken (page load, search submit, paginated page load, failure).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can replay any failed scraping session by opening the captured trace file in a compatible trace viewer, seeing the full DOM state, network activity, and console logs.
- **SC-002**: Operators can visually inspect what the browser rendered at each scraping milestone via saved screenshots.
- **SC-003**: Diagnostic capture introduces no user-observable degradation to search response times when tracing mode is "off" and screenshots are disabled.
- **SC-004**: 100% of failed scraping requests produce at least a failure screenshot, regardless of configuration.
- **SC-005**: Diagnostic artifact filenames allow an operator to locate artifacts for a specific request using only the correlation ID.

## Assumptions

- The local filesystem is used for diagnostic artifact storage; cloud/blob storage is out of scope for this feature.
- Playwright's built-in tracing API is used — no custom trace format is needed.
- Playwright's built-in screenshot API is used for screenshot capture.
- The existing configuration section will be extended with new diagnostics settings (output path, tracing mode, screenshot toggle).
- Artifact retention and cleanup are managed externally (e.g., scheduled job, disk monitoring) and are out of scope.
- The diagnostics output path defaults to a `diagnostics` subdirectory relative to the application's content root if not explicitly configured.
- The default tracing mode is "always" and screenshot capture defaults to enabled; operators may override either setting for production environments where the performance/storage overhead is unacceptable.
