# Feature Specification: Windows VM Proxy for Chrome Headed Mode

**Feature Branch**: `033-windows-headed-proxy`  
**Created**: 2026-04-06  
**Status**: Draft  
**Input**: User description: "I want the ability to use the BizScraper.Api deployed on Windows VM for chrome / headed scenario. The primary API runs on AKS, and when it encounters chrome / headed scrapping slug, it forwards the api to Windows VM. The API token that AKS receives should be forwarded."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Transparent Proxy to Windows VM for Headed Chrome Definitions (Priority: P1)

An operator configures the AKS-hosted BizScraper API with the network address and authentication credentials of a Windows VM running the same BizScraper API. When a user (or the test-UI) submits a scraping request for a definition that specifies `"mode": "windows"` in its browser configuration, the AKS API automatically forwards the entire request to the Windows VM. The user receives results identically to any other scraping request — the proxy is invisible to them.

**Why this priority**: This is the core capability. Without transparent forwarding, headed Chrome definitions cannot leverage a Windows display environment for anti-bot bypass, and all existing callers (test-UI, external systems) would need modification.

**Independent Test**: Submit a scraping request for a definition with `headless: false` (e.g., Iowa business search) via the test-UI or direct API call against the AKS instance. Verify the request is forwarded to the Windows VM, executed there, and the response is returned through the AKS API with correct data and correlation ID.

**Acceptance Scenarios**:

1. **Given** the AKS API is configured with a Windows VM endpoint and a definition has `"mode": "windows"`, **When** a user submits an execute-script request for that definition, **Then** the AKS API forwards the request to the Windows VM and returns the Windows VM's response to the caller.
2. **Given** the AKS API is configured with a Windows VM endpoint and a definition has `"mode": "local"` or `"mode": "remote"` (or no browser override), **When** a user submits an execute-script request, **Then** the AKS API handles the request locally using existing browser pools (no forwarding occurs).
3. **Given** the AKS API forwards a request, **When** the Windows VM returns a successful response, **Then** the correlation ID from the original request is preserved end-to-end.

---

### User Story 2 - Token Forwarding for Authenticated Requests (Priority: P1)

When the AKS API forwards a request to the Windows VM, it must include the original caller's authentication token. The Windows VM validates this forwarded token using the same Entra ID configuration to ensure the caller is authorized. This preserves the end-to-end security chain without requiring separate credentials for the Windows VM.

**Why this priority**: Security is non-negotiable. The Windows VM must not accept unauthenticated requests, and the forwarding mechanism must not break the existing Entra ID trust chain.

**Independent Test**: Submit an authenticated request to the AKS API for a headed definition. Verify the Windows VM receives and validates the forwarded Entra ID token and executes the request. Submit the same request without authentication and verify the AKS API returns a 401 before any forwarding occurs.

**Acceptance Scenarios**:

1. **Given** an authenticated user submits a headed definition request to the AKS API, **When** the request is forwarded, **Then** the Windows VM receives the original bearer token and validates it successfully.
2. **Given** an unauthenticated request is submitted for a headed definition, **When** the AKS API processes it, **Then** the request is rejected with 401 Unauthorized before any forwarding occurs.
3. **Given** the forwarded token has expired or is invalid, **When** the Windows VM validates it, **Then** the Windows VM returns 401 which the AKS API propagates to the caller.

---

### User Story 3 - Graceful Fallback When Windows VM Is Unavailable (Priority: P2)

When the Windows VM is unreachable (network failure, VM stopped, timeout), the AKS API must return a clear, informative error to the caller rather than silently failing or falling back to local headless execution (which would defeat the purpose of headed mode for anti-bot bypass).

**Why this priority**: Operational resilience and clear diagnostics are important for production reliability, but the system cannot meaningfully function in headed mode without the Windows VM, so a graceful error is more appropriate than a silent fallback.

**Independent Test**: Stop the Windows VM or block network connectivity, then submit a headed definition request. Verify the AKS API returns a 503 Service Unavailable with a descriptive error and appropriate retry header.

**Acceptance Scenarios**:

1. **Given** the Windows VM is unreachable, **When** a user submits a headed definition request, **Then** the AKS API returns 503 with a message indicating the Windows execution environment is unavailable.
2. **Given** the Windows VM responds but with a timeout, **When** the request exceeds the configured proxy timeout, **Then** the AKS API returns 504 Gateway Timeout to the caller.
3. **Given** the Windows VM returns an error response (e.g., 500), **When** the AKS API receives it, **Then** the error is propagated to the caller with the original error details.

---

### User Story 4 - Health Check Reports Windows VM Status (Priority: P2)

The existing `/ready` health check endpoint reports the connectivity status of the Windows VM alongside the existing local and remote browser pool statuses. Operators can determine at a glance whether the full system — including the Windows proxy — is operational. The health check also lists which definitions are routed to the Windows VM.

**Why this priority**: Operational visibility is essential for monitoring and troubleshooting, but the core proxy functionality is more critical.

**Independent Test**: Call the `/ready` endpoint and verify it includes a `windowsProxy` section showing status, endpoint, and a list of definitions routed to the Windows VM.

**Acceptance Scenarios**:

1. **Given** the Windows VM endpoint is configured and reachable, **When** the health check runs, **Then** the response includes `windowsProxy.status: "healthy"` and the Windows VM endpoint URL.
2. **Given** the Windows VM endpoint is configured but unreachable, **When** the health check runs, **Then** the response includes `windowsProxy.status: "unhealthy"` and the health check still returns 200 (the pod remains ready since other modes still work).
3. **Given** no Windows VM endpoint is configured, **When** the health check runs, **Then** the `windowsProxy` section shows `status: "not-configured"`.
4. **Given** definitions with `"mode": "windows"` exist, **When** the health check runs, **Then** the response lists those definition slugs under `definitionsRoutedToWindows`.

---

### User Story 5 - Windows VM Handles Post-Processing Independently (Priority: P3)

The Windows VM instance of BizScraper API is a complete, self-contained execution environment. When it executes a headed Chrome scraping request, it performs all post-processing steps (screenshot uploads to cloud storage, document downloads, filing history uploads) using its own configuration. The AKS API does not re-process or duplicate any post-processing for proxied requests.

**Why this priority**: This avoids duplicate processing and keeps the proxy layer thin. The Windows VM is a full BizScraper instance, not a partial execution delegate.

**Independent Test**: Execute a headed definition request that generates screenshots and documents. Verify screenshots appear in cloud storage with the correct folder structure and correlation ID, and are not duplicated.

**Acceptance Scenarios**:

1. **Given** a headed definition request is proxied to the Windows VM, **When** the Windows VM executes and uploads screenshots to cloud storage, **Then** the screenshots are accessible via the AKS API's existing proxy endpoints.
2. **Given** the Windows VM completes execution with document downloads, **When** the response is returned through the AKS API, **Then** document proxy URLs in the response are valid and resolvable through the AKS API.

### Edge Cases

- What happens when the Windows VM endpoint is configured but the network path between AKS and the VM is blocked? The AKS API returns 503 with a descriptive error after the connection timeout.
- What happens when a definition changes from `headless: true` to `headless: false` while requests are in-flight? In-flight requests complete with their original routing decision; subsequent requests use the updated configuration.
- What happens when the Windows VM returns a response larger than expected (e.g., very large scraping result)? The proxy should stream or buffer within the configured timeout; excessively large responses are bounded by the existing request size limits.
- What happens when the AKS API has no Windows VM configured but encounters a `"mode": "windows"` definition? The definition falls back to the local browser pool (existing behavior — runs Chrome locally, which may not bypass anti-bot measures but does not fail).
- What happens when the forwarded token is for a client credential flow (system identity) rather than a user identity? Both flows are supported since the Windows VM validates against the same Entra ID tenant and accepts the same "BizScraperAccess" policy.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST detect when a definition's browser configuration specifies `"mode": "windows"` and a Windows VM endpoint is configured, and route the request to the Windows VM instead of executing locally. The `mode` value `"windows"` is a new addition to the existing `"local"` and `"remote"` modes.
- **FR-002**: System MUST forward the caller's original bearer token to the Windows VM in the `Authorization` header when proxying requests.
- **FR-003**: System MUST forward the `X-Correlation-Id` header to the Windows VM to preserve end-to-end request tracing.
- **FR-004**: System MUST return the Windows VM's response to the caller without modification — no URL rewriting is required since both API instances share the same blob storage and document proxy URLs use relative paths resolvable against the caller's API host.
- **FR-005**: System MUST return 503 Service Unavailable with a `Retry-After` header when the Windows VM is unreachable (connection refused, DNS failure). No automatic retry is attempted.
- **FR-006**: System MUST return 504 Gateway Timeout when the Windows VM connection succeeds but the response exceeds the configured proxy timeout (default 180s). No automatic retry is attempted because headed scraping is non-idempotent and retrying risks duplicate side effects (document uploads, captcha solves).
- **FR-007**: System MUST propagate error responses from the Windows VM (4xx, 5xx) to the caller with the original error details preserved.
- **FR-008**: System MUST include Windows VM connectivity status in the `/ready` health check response, including the list of definitions routed to the Windows VM.
- **FR-009**: System MUST log each proxied request with the definition slug, correlation ID, and destination endpoint for operational visibility.
- **FR-010**: System MUST fall back to the local browser pool when a definition has `"mode": "windows"` but no Windows VM endpoint is configured.
- **FR-011**: System MUST NOT perform post-processing (screenshot uploads, document downloads) for proxied requests, as the Windows VM handles these independently.
- **FR-012**: System MUST support configuring the Windows VM endpoint, proxy timeout, and retry policy through application configuration.
- **FR-013**: System MUST communicate with the Windows VM exclusively over HTTPS. The Windows VM has a CA-signed TLS certificate; the proxy client uses default certificate validation (no self-signed trust overrides required).

### Key Entities

- **Windows Proxy Configuration**: The endpoint URL and timeout settings for the Windows VM. Determines whether Windows proxying is available and how long to wait for responses.
- **Browser Configuration (existing, extended)**: The per-definition `browser` section in definition JSON files. The `mode` property now supports three values: `"local"`, `"remote"`, and `"windows"`. The `"windows"` mode triggers forwarding to the Windows VM when the proxy endpoint is configured. The `headless` and `channel` properties remain independent — they control display mode and browser channel on whichever execution environment runs the request.
- **Proxied Request**: A request that the AKS API forwards to the Windows VM rather than executing locally. Carries the original bearer token, correlation ID, and full request payload.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can execute headed Chrome definitions through the AKS API and receive results within 3 minutes (inclusive of proxy overhead), with no difference in response shape compared to locally-executed requests.
- **SC-002**: 100% of proxied requests preserve the caller's authentication token and correlation ID end-to-end.
- **SC-003**: When the Windows VM is unavailable, the caller receives a clear 503 error within the configured timeout (default 180 seconds) — no requests hang indefinitely.
- **SC-004**: The `/ready` endpoint accurately reports Windows VM connectivity status, updating within 30 seconds of a connectivity change.
- **SC-005**: Existing definitions that use headless mode or Azure Playwright continue to route and execute identically to their pre-feature behavior — zero regression.
- **SC-006**: *(Post-deployment observation, not a release gate)* Anti-bot bypass success rate for Iowa search (`us-ia-business-search`) improves when using headed Chrome on Windows VM compared to headless Chrome on AKS. Measured by comparing scraping success rates over a one-week sample post-deployment.

## Clarifications

### Session 2026-04-06

- Q: Should the AKS API rewrite document proxy URLs in proxied responses (since they originate from the Windows VM)? → A: No rewrite needed — both APIs share the same blob storage; proxy URLs use relative paths resolvable against the AKS API.
- Q: What should happen when the Windows VM is actively processing but approaches the proxy timeout? → A: No retry on timeout — return 504 immediately after 180s. Headed scraping is non-idempotent (browser state, captcha solves, document uploads), so retrying risks duplicate side effects.
- Q: Audit trails for headed-Chrome executions only exist on the Windows VM's local SQLite — not queryable from AKS. Acceptable? → A: Acceptable — audit records live on whichever instance executed the request; consolidation deferred to a future feature if needed.
- Q: Should the AKS-to-Windows-VM connection require TLS, and what certificate model? → A: Require HTTPS — the Windows VM has a valid CA-signed TLS certificate (not self-signed). No custom certificate trust configuration needed on the AKS proxy client.
- Q: Should the routing signal be `headless: false` or an explicit browser mode value? → A: Add explicit `"mode": "windows"` to the browser config. This decouples routing from the headless flag — a definition can run headed locally for debugging without being routed to the VM, and the operator's intent is explicit.

## Assumptions

- The Windows VM runs the same BizScraper API codebase, configured for local-only browser mode with Chrome installed and `headless: false` support. Provisioning the Windows VM (OS, Chrome installation, network configuration) is handled outside this feature.
- The Windows VM is network-reachable from the AKS cluster (e.g., via VNet peering, private endpoint, or public IP with firewall rules). Network setup is handled outside this feature.
- The Windows VM has a valid CA-signed TLS certificate. The AKS proxy client uses standard HTTPS with default certificate validation — no self-signed trust configuration required.
- The Windows VM has access to the same Azure Blob Storage account for screenshot and document uploads, using its own connection string from Key Vault or local configuration.
- The Windows VM validates incoming bearer tokens against the same Entra ID tenant and app registration as the AKS API instance.
- The Windows VM is stateless — definitions are bundled in the deployed image. No shared database is required between AKS and Windows instances.
- Headed Chrome on Windows consumes more resources than headless Chrome. The Windows VM should be configured with lower concurrency limits (1-2 concurrent executions) to avoid resource contention.
- The test-UI requires no changes — routing is entirely transparent at the API layer.
