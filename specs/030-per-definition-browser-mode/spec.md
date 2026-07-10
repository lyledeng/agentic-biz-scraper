# Feature Specification: Per-Definition Browser Mode Configuration

**Feature Branch**: `030-per-definition-browser-mode`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "I want the ability to either use a remote playwright workspace or local playwright instance with chrome. Some web sites have anti-bot measures, which detect playwright running in headless chromium mode. IA should run with local playwright. Rest should run via Azure Playwright workspaces. Configuration should be at a json file level for full flexibility"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Override Browser Mode per Definition (Priority: P1)

As an operator, I want each scraping definition JSON file to optionally declare its own browser mode (local or remote), so that definitions targeting sites with anti-bot detection can use a local Chrome browser while all others continue using the remote Azure Playwright workspace.

**Why this priority**: This is the core capability. Without per-definition browser mode overrides, the system is locked into a single global mode, which prevents handling sites that detect headless Chromium (e.g., Iowa SOS) alongside sites that work fine remotely.

**Independent Test**: Can be fully tested by adding a browser configuration block to the Iowa definition JSON specifying local mode with Chrome channel, executing a search, and verifying the system launches a local Chrome instance for that request — while a Colorado search in the same deployment still uses the remote workspace.

**Acceptance Scenarios**:

1. **Given** a definition JSON contains a browser override set to local mode with Chrome channel, **When** that definition is executed, **Then** the system launches a local Chrome browser instance for that specific request.
2. **Given** a definition JSON contains a browser override set to remote mode, **When** that definition is executed, **Then** the system connects to the configured remote Azure Playwright workspace.
3. **Given** a definition JSON has no browser configuration block, **When** that definition is executed, **Then** the system uses the global default browser mode (determined by existing configuration).
4. **Given** two definitions are executed concurrently — one configured for local mode and one for remote mode, **When** both requests are processed, **Then** each uses its own browser mode independently without interference.

---

### User Story 2 - Iowa Runs on Local Chrome to Bypass Anti-Bot Detection (Priority: P1)

As an operator, I want the Iowa business search definition to run on a local Playwright instance using the Chrome channel (not headless Chromium), so that the Iowa Secretary of State website does not block the scraper due to anti-bot measures that detect Playwright's default headless Chromium.

**Why this priority**: Iowa is a known state where anti-bot measures actively block headless Chromium. Without this, Iowa searches fail entirely in the remote Azure Playwright workspace.

**Independent Test**: Can be tested by executing an Iowa business search and verifying successful results are returned without being blocked by anti-bot measures.

**Acceptance Scenarios**:

1. **Given** the Iowa definition specifies local mode with Chrome channel, **When** a business search is executed for Iowa, **Then** the search completes successfully and returns business entity results.
2. **Given** the Iowa definition specifies local mode with Chrome channel, **When** a business search is executed, **Then** the browser launched is Chrome (not the bundled headless Chromium).

---

### User Story 3 - Non-Iowa Definitions Default to Remote Workspace (Priority: P2)

As an operator, I want definitions that do not override browser mode to continue using the remote Azure Playwright workspace, so that the majority of definitions benefit from cloud-hosted browser infrastructure without any configuration changes.

**Why this priority**: Most definitions work correctly on the remote workspace. This story ensures backward compatibility — existing definitions continue working without modification.

**Independent Test**: Can be tested by executing a Colorado or Wyoming search and verifying the system connects to the remote Azure Playwright workspace.

**Acceptance Scenarios**:

1. **Given** a definition JSON has no browser override and the global mode is remote, **When** that definition is executed, **Then** the system connects to the remote workspace as before.
2. **Given** a definition JSON has no browser override and the global mode is local, **When** that definition is executed, **Then** the system uses local mode as the fallback.

---

### User Story 4 - Operator Adds Browser Override to Any Definition (Priority: P2)

As an operator, I want to add or modify the browser configuration block in any definition JSON file to change its browser mode, so that I can respond to new anti-bot detections or infrastructure changes without code changes.

**Why this priority**: New sites may introduce anti-bot measures at any time. Operators need the flexibility to switch any definition to local mode via configuration alone.

**Independent Test**: Can be tested by adding a browser block to a previously remote-only definition, executing it, and verifying it now uses local mode.

**Acceptance Scenarios**:

1. **Given** an operator adds a browser override block to a definition JSON, **When** the application processes that definition, **Then** it uses the overridden browser mode.
2. **Given** an operator removes the browser override block from a definition JSON, **When** the application processes that definition, **Then** it falls back to the global default browser mode.

---

### User Story 5 - Health Check Reflects Per-Definition Browser Modes (Priority: P3)

As an operator, I want the readiness health check to reflect that the system may use both local and remote browsers, so that probes accurately represent the system's capacity to process requests across all configured modes.

**Why this priority**: Kubernetes readiness probes must account for the possibility that some definitions require local browsers while others require remote connectivity.

**Independent Test**: Can be tested by calling `/ready` when the system has definitions configured for both modes and verifying the response reflects both capabilities.

**Acceptance Scenarios**:

1. **Given** the system has definitions requiring both local and remote modes, **When** `/ready` is called, **Then** the response indicates the system can serve requests in both modes.
2. **Given** the remote endpoint is unreachable but local mode is functional, **When** `/ready` is called, **Then** the response returns `200 OK` with a JSON body indicating local is healthy and remote is unhealthy; the pod remains in the Kubernetes ready pool.

---

### Edge Cases

- What happens when a definition specifies local mode with Chrome channel but Chrome is not installed in the container? The browser launch fails with an error indicating the requested channel is unavailable, and the error is surfaced in the response and diagnostics.
- What happens when a definition specifies remote mode but no global `BrowserEndpoint` is configured? The system falls back to local mode for that definition and logs a warning that the remote endpoint is not available.
- What happens when the browser configuration block in a definition JSON contains an invalid or unrecognized mode value? The system rejects the definition at load time with a validation error identifying the invalid browser mode.
- What happens if the same Chrome browser instance is shared across multiple local-mode definitions? Browser pooling and recycling apply independently per browser mode — local and remote browsers are managed as separate pools.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Each definition JSON file MUST support an optional browser configuration section that can override the global browser mode for that definition.
- **FR-002**: The browser configuration section MUST support specifying the mode as "local" or "remote".
- **FR-003**: The browser configuration section MUST support specifying the browser channel (e.g., "chrome", "msedge") for local mode.
- **FR-004**: The browser configuration section MUST support specifying whether to run headless or headed for local mode.
- **FR-005**: When a definition has no browser configuration section, the system MUST use the global default browser mode (determined by existing `BrowserEndpoint` configuration).
- **FR-006**: When a definition specifies local mode, the system MUST launch a local browser using the specified channel and headless settings, regardless of the global `BrowserEndpoint` configuration.
- **FR-007**: When a definition specifies remote mode but no global `BrowserEndpoint` is configured, the system MUST fall back to local mode and log a warning.
- **FR-008**: The Iowa business search definition (`us-ia-business-search`) MUST be configured with local mode and Chrome channel.
- **FR-009**: The Iowa entity details definition (`us-ia-entity-details`) MUST be configured with local mode and Chrome channel.
- **FR-010**: All other existing definitions MUST remain without a browser override, preserving current behavior.
- **FR-011**: Browser instances MUST be pooled separately for local-mode and remote-mode executions to prevent cross-mode interference.
- **FR-015**: Each browser pool MUST have its own independent concurrency limit (default 3 per pool), allowing up to 6 total concurrent executions system-wide. Local-mode requests MUST NOT consume remote-mode concurrency slots and vice versa.
- **FR-012**: The system MUST validate the browser configuration section when loading definitions and reject invalid values with a clear error message.
- **FR-013**: The readiness health check MUST return `200 OK` with a JSON body showing per-mode health status (e.g., local: healthy, remote: unhealthy). The pod MUST remain in the Kubernetes ready pool as long as at least one browser mode is healthy.
- **FR-014**: The active browser mode used for each execution MUST be logged, including whether it was overridden by the definition or inherited from the global default.

### Key Entities

- **Browser Configuration (per-definition)**: An optional section within a definition JSON that specifies browser mode ("local" or "remote"), browser channel (e.g., "chrome"), and headless preference. Overrides the global default for that definition.
- **Browser Pool**: Separate managed pools of browser instances — one for local-mode executions and one for remote-mode executions — each with independent lifecycle (recycling, usage counting, disconnection recovery). Both pools share the same global `ReuseBrowserForRequests` threshold for recycling.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Iowa business searches complete successfully without being blocked by anti-bot measures, using a local Chrome browser.
- **SC-002**: Non-Iowa definitions continue to execute via the remote Azure Playwright workspace with no configuration changes and no regressions.
- **SC-003**: Operators can change any definition's browser mode by editing its JSON file alone — no code deployment required.
- **SC-004**: Concurrent requests using different browser modes (one local, one remote) complete independently without failures or cross-mode interference.
- **SC-005**: All existing scraping test scenarios pass without modification for definitions that do not specify a browser override.
- **SC-006**: The readiness probe accurately reflects the system's ability to serve requests across all configured browser modes within 5 seconds.

## Clarifications

### Session 2026-04-04

- Q: How should concurrency limits be partitioned across the two browser pools (local + remote)? → A: Independent limit of 3 per pool (6 total system-wide); local requests do not consume remote slots and vice versa.
- Q: Should additional stealth launch arguments be applied for local-mode anti-bot bypass? → A: Standard stealth args (e.g., --disable-blink-features=AutomationControlled) applied by default for all local-mode launches; current behavior is confirmed sufficient.
- Q: When one browser mode is unhealthy and the other is healthy, what HTTP status should /ready return? → A: Return 200 OK with per-mode health breakdown in the response body; pod stays in Kubernetes ready pool so working definitions can still be served.
- Q: Should each browser pool have its own recycling threshold or share the global value? → A: Both pools share the same global ReuseBrowserForRequests threshold (default 25); no per-pool override needed.

## Assumptions

- Chrome (or Chromium with Chrome channel) is available in the container image or the host environment where local mode is used. The existing Playwright base image in the Dockerfile may need a Chrome channel install step.
- The existing global `PlaywrightOptions` and `BrowserEndpoint` configuration will continue to serve as the default for definitions that do not specify a browser override.
- Feature 021 (Playwright Local Fallback) is already implemented, providing the foundational local/remote switching mechanism. This feature extends that mechanism to be per-definition rather than global.
- Anti-bot detection on the Iowa SOS site is triggered specifically by headless Chromium's browser fingerprint; running with the Chrome channel plus standard stealth launch arguments (e.g., `--disable-blink-features=AutomationControlled`) is confirmed sufficient to avoid detection. No additional per-definition custom args are needed.
- Headed mode (non-headless) is not required for bypassing anti-bot detection — headless Chrome channel is sufficient. If headed mode is needed, it can be configured via the browser configuration section.
- Browser pooling will use at most two pools (one local, one remote). A more granular per-channel pool is not needed for the current set of definitions.
- The definition JSON schema version remains at "1.0.0" — the new browser section is additive and backward-compatible.
