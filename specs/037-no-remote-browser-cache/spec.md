# Feature Specification: Disable Remote Browser Caching

**Feature Branch**: `037-no-remote-browser-cache`  
**Created**: 2026-04-12  
**Status**: Draft  
**Input**: User description: "I do not want to cache remote playwright instances. For each remote playwright run, perform the web scrape action and then close. Caching keeps the remote instance online incurring recurring charges."

## Clarifications

### Session 2026-04-12

- Q: When a remote browser disconnects mid-scrape, what retry behavior should apply within the per-request lifecycle? → A: One reconnect-and-retry within the same request, then fail.
- Q: What is the acceptable per-request overhead budget for the remote ConnectAsync call? → A: 30-second connection timeout.
- Q: Should the Playwright runtime instance also be created and disposed per-request for remote mode, or only the IBrowser connection? → A: Both Playwright runtime and IBrowser are created and disposed per-request for remote mode.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Remote Browser Session Per-Request Lifecycle (Priority: P1)

As a system operator, I want each remote Playwright execution to connect a fresh browser session, perform the scraping action, and immediately disconnect/close the remote browser so that no idle Azure Playwright sessions remain billable between requests.

**Why this priority**: This is the core ask — eliminating recurring charges from cached remote browser instances that stay connected between requests.

**Independent Test**: Execute a remote-mode scraping definition (e.g., `us-co-business-search`), observe that the Azure Playwright workspace shows a session that starts at request time and ends immediately after the scrape completes. No lingering sessions between requests.

**Acceptance Scenarios**:

1. **Given** the system is configured in remote mode with a valid Azure Playwright endpoint, **When** a scraping request completes successfully, **Then** the remote browser connection is closed immediately after the response is produced.
2. **Given** the system is configured in remote mode, **When** no scraping requests are in-flight, **Then** zero active sessions exist on the Azure Playwright workspace.
3. **Given** a remote scraping request fails mid-execution, **When** the error is caught, **Then** the remote browser connection is still closed and no orphaned session remains.

---

### User Story 2 - Local Browser Pool Unchanged (Priority: P1)

As a developer, I want local browser pooling to remain exactly as it is today — pooled and reused across requests — so that local-mode performance and anti-detection behavior are not affected.

**Why this priority**: Local browser instances have no per-session billing, and pooling them provides performance and stealth benefits.

**Independent Test**: Execute a local-mode scraping definition multiple times in succession; verify the browser instance is reused up to the configured `ReuseBrowserForRequests` threshold before recycling.

**Acceptance Scenarios**:

1. **Given** a definition configured with `browser.mode = "local"`, **When** multiple requests are executed, **Then** the local browser is reused across requests up to the configured threshold.
2. **Given** the system is running with both local and remote definitions, **When** a local definition executes, **Then** no change in local pool behavior occurs compared to the current implementation.

---

### User Story 3 - Readiness Probe Does Not Create Remote Sessions (Priority: P2)

As a system operator, I want the Kubernetes readiness probe (`/ready`) to not trigger remote Azure Playwright sessions so that periodic health checks do not generate billable sessions.

**Why this priority**: Readiness probes fire every few seconds; if they create remote sessions, they generate continuous billing even with zero user traffic.

**Independent Test**: Deploy the application in remote mode with no user traffic; observe that no Azure Playwright sessions appear on the workspace dashboard.

**Acceptance Scenarios**:

1. **Given** the system is in remote mode with no in-flight scraping requests, **When** the readiness probe fires, **Then** no new Azure Playwright session is created.
2. **Given** no remote browser has ever connected, **When** the readiness probe fires, **Then** the remote mode reports healthy (it will connect on first real request).
3. **Given** a remote browser was previously connected and is now closed, **When** the readiness probe fires, **Then** the remote mode reports the last known connection state without reconnecting.

---

### Edge Cases

- What happens when two concurrent remote requests arrive? Each should get its own fresh session; concurrency is bounded by the existing semaphore (currently 3 concurrent remote executions).
- What happens if the remote browser disconnects mid-scrape? The system attempts exactly one reconnect-and-retry within the same request. If the retry also fails, the request fails and the session is closed. No browser is kept alive after the request.
- What happens if the Playwright `ConnectAsync` call fails? The request fails with the existing error handling; no stale session is left behind.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST connect to the Azure Playwright workspace at the start of each remote-mode scrape request and disconnect/close the remote browser immediately after the request completes.
- **FR-002**: System MUST NOT maintain a persistent remote browser pool between requests. The remote `BrowserPool` must not cache `IBrowser` instances across request boundaries.
- **FR-003**: System MUST continue to pool and reuse local browser instances exactly as today, governed by the existing `ReuseBrowserForRequests` threshold.
- **FR-004**: System MUST ensure the remote browser is closed even when the scraping action fails or throws an exception (guaranteed cleanup).
- **FR-005**: System MUST NOT create Azure Playwright sessions during readiness probe checks (`/ready` endpoint).
- **FR-006**: System MUST support concurrent remote requests (up to the existing semaphore limit) where each request independently connects and disconnects its own remote browser instance.
- **FR-007**: System MUST log the connect and disconnect lifecycle events for remote browser sessions for observability.
- **FR-008**: System MUST create and dispose both the Playwright runtime (Node.js subprocess) and the remote IBrowser connection per-request for remote mode; no Playwright process should persist between requests.

### Key Entities

- **BrowserPool**: Existing pool abstraction. For remote mode, transitions from a cached-session model to a per-request connect/disconnect model.
- **GenericScriptScraper**: The scraping engine that orchestrates browser lifecycle and flow execution. Must be updated to implement per-request remote sessions.
- **PlaywrightOptions**: Existing configuration. No new configuration properties are required; the behavior change applies universally to remote mode.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After deploying this change, zero idle Azure Playwright sessions exist between scraping requests.
- **SC-002**: Remote scraping requests complete successfully with the same functional behavior as today (search results, entity details, document downloads all work identically).
- **SC-003**: Local-mode scraping behavior is unchanged — browser reuse continues as before.
- **SC-004**: The Azure Playwright workspace billing shows sessions only during active scraping, with no 4-minute recurring idle sessions.
- **SC-005**: Readiness probe calls generate zero Azure Playwright sessions.

## Assumptions

- The Azure Playwright `ConnectAsync` call has a 30-second timeout budget per connection attempt; this accommodates cold starts and network variability while still failing fast on genuine connectivity issues.
- The existing concurrency semaphore (3 concurrent remote executions) provides sufficient request isolation; no shared browser state exists between concurrent requests.
- Local browser pooling (`ReuseBrowserForRequests = 25`) remains the correct strategy for local mode; only remote mode behavior changes.
- The Windows proxy mode is unaffected by this change; it uses a separate forwarding mechanism.
