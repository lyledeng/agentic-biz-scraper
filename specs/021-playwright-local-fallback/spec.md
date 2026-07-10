# Feature Specification: Playwright Local Fallback

**Feature Branch**: `021-playwright-local-fallback`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "I want the ability to use either Playwright workspace or a local playwright instance."

## User Scenarios & Testing

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Configure Browser Mode via Configuration (Priority: P1)

As an operator, I want to configure whether the system uses a remote Azure Playwright workspace or a locally launched Chromium browser, so that I can deploy the same application image in environments with or without Azure Playwright workspace access.

**Why this priority**: This is the core capability — without a clear configuration-driven toggle, none of the other stories are possible.

**Independent Test**: Can be fully tested by setting a configuration value to "Local" and verifying the system launches a local browser, then setting it to "Remote" and verifying it connects to a remote workspace endpoint.

**Acceptance Scenarios**:

1. **Given** the browser mode is set to "Remote" and a valid `BrowserEndpoint` is configured, **When** a script execution request arrives, **Then** the system connects to the remote Azure Playwright workspace and executes the script.
2. **Given** the browser mode is set to "Local", **When** a script execution request arrives, **Then** the system launches a local Chromium instance and executes the script.
3. **Given** `BrowserEndpoint` is set to a malformed value (e.g., `http://invalid` instead of `wss://...`), **When** the application starts, **Then** startup fails with a validation error indicating `BrowserEndpoint` must start with `wss://` or `ws://`.

---

### User Story 2 - Health Check Reflects Active Browser Mode (Priority: P2)

As an operator, I want the readiness health check to validate connectivity for whichever browser mode is active, so that Kubernetes readiness probes accurately reflect the system's ability to process requests.

**Why this priority**: Health checks drive pod readiness in Kubernetes. A readiness probe that always checks remote connectivity would fail pods configured for local mode.

**Independent Test**: Can be tested by setting mode to "Local", calling `/ready`, and verifying it reports ready without requiring a remote endpoint.

**Acceptance Scenarios**:

1. **Given** browser mode is "Remote", **When** `/ready` is called, **Then** the probe validates remote workspace connectivity and returns the connection status.
2. **Given** browser mode is "Local", **When** `/ready` is called, **Then** the probe validates that a local browser can be launched and returns ready.
3. **Given** browser mode is "Remote" and the remote endpoint is unreachable, **When** `/ready` is called, **Then** the probe returns 503 Service Unavailable.

---

### User Story 3 - Consistent Browser Lifecycle Across Both Modes (Priority: P2)

As a developer, I want browser pooling, recycling, and diagnostics (tracing, screenshots) to work identically regardless of whether the browser is remote or local, so that I do not need to maintain separate code paths for each mode.

**Why this priority**: Ensuring feature parity between modes prevents bugs that only surface in one environment.

**Independent Test**: Can be tested by running the same script execution request against both modes and verifying identical screenshot capture, trace output, and browser recycling behavior.

**Acceptance Scenarios**:

1. **Given** the system is in "Local" mode, **When** a script is executed, **Then** screenshots and diagnostics are captured using the same mechanism as "Remote" mode.
2. **Given** the system is in either mode, **When** the browser usage count exceeds the configured `ReuseBrowserForRequests` threshold, **Then** the browser is recycled and a new instance is created.
3. **Given** the system is in "Local" mode, **When** the local browser process crashes, **Then** the next request detects the disconnected state and launches a fresh browser instance.

---

### User Story 4 - Observability of Active Browser Mode (Priority: P3)

As an operator, I want the active browser mode to be logged at startup and visible in health check responses, so that I can quickly verify which mode a running instance is operating in.

**Why this priority**: Operational visibility reduces debugging time when issues arise across environments.

**Independent Test**: Can be tested by checking startup logs for browser mode indication and verifying the `/ready` response includes the active mode.

**Acceptance Scenarios**:

1. **Given** the application starts, **When** the browser mode is resolved from configuration, **Then** the active mode ("Remote" or "Local") is logged at Information level.
2. **Given** the system is running, **When** `/ready` is called, **Then** the response body includes the active browser mode.

---

### Edge Cases

- What happens when `BrowserEndpoint` contains a malformed URL (e.g., `http://invalid` instead of `wss://...`)?  
  The system rejects the value at startup with a validation error indicating `BrowserEndpoint` must start with `wss://` or `ws://`.
- What happens when "Local" mode is configured but browser binaries are not installed in the container?  
  The Playwright `LaunchAsync` call fails and the error is surfaced through the readiness probe and request-level error handling.
- What happens when switching from "Remote" to "Local" mode via a configuration reload?  
  The existing pooled browser is recycled on its next usage check; the new browser is created using the updated mode on the next request.

## Requirements

### Functional Requirements

- **FR-001**: System MUST default to "Local" browser mode; remote mode MUST require explicit opt-in by populating `BrowserEndpoint`.
- **FR-002**: When mode is "Remote", the system MUST connect to the configured `BrowserEndpoint` using the configured `AccessToken`.
- **FR-003**: When mode is "Local", the system MUST launch a local headless Chromium browser, ignoring `BrowserEndpoint` and `AccessToken`.
- **FR-004**: When `BrowserEndpoint` is populated, the system MUST operate in "Remote" mode; when empty or absent, the system MUST operate in "Local" mode.
- **FR-005**: The readiness health check MUST validate the browser connection for the active mode — remote connectivity for "Remote", local launch capability for "Local".
- **FR-006**: Browser pooling, recycling (based on `ReuseBrowserForRequests`), and disconnection detection MUST behave consistently in both modes.
- **FR-007**: `PlaywrightBrowserManager` MUST be removed; the `/ready` health check MUST be refactored to use `GenericScriptScraper` as the single browser lifecycle manager.
- **FR-010**: The default concurrency limit MUST be increased from 1 to 3 and applied uniformly to both browser modes.
- **FR-008**: The active browser mode MUST be logged at application startup.
- **FR-009**: The readiness endpoint response MUST include the active browser mode.

### Key Entities

- **PlaywrightOptions**: Extended with browser mode detection based on `BrowserEndpoint` presence.
- **GenericScriptScraper**: The single browser lifecycle manager for both scraping and health checks.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Operators can switch between remote and local browser modes with a single configuration change and no code deployment.
- **SC-002**: All existing script definitions execute successfully in both browser modes with identical output.
- **SC-003**: Readiness probe correctly reports healthy/unhealthy status for the active browser mode within 5 seconds.
- **SC-004**: No regression in existing scraping functionality — all current test scenarios pass in both modes.
- **SC-005**: Active browser mode is immediately identifiable from startup logs and health check responses without SSH/debugging access.

## Clarifications

### Session 2026-04-01

- Q: Should local mode use different concurrency limits than remote mode? → A: Same concurrency limits for both modes; increase default from 1 to 3.
- Q: What should the default browser mode be when no explicit mode is configured? → A: Default to "Local"; remote mode requires explicit opt-in by populating BrowserEndpoint.
- Q: Should PlaywrightBrowserManager be updated or removed? → A: Remove PlaywrightBrowserManager; refactor health check to use GenericScriptScraper.

## Assumptions

- The container image already includes local Chromium browser binaries (confirmed: the Dockerfile uses the Playwright base image, which bundles browsers).
- `GenericScriptScraper.EnsureBrowserAsync` already contains conditional logic for remote connect vs. local launch; this feature formalizes and standardizes that behavior.
- `PlaywrightBrowserManager` will be removed as dead code; its health check responsibility moves to `GenericScriptScraper`.
- The default mode is "Local"; deployed environments opt into "Remote" by setting `BrowserEndpoint` (already done via Helm/Key Vault config).
- Local mode serves both local development and environments without Azure Playwright workspace access.
- Headless mode is used for local browser launch.
