# Research: Disable Remote Browser Caching

**Feature**: 037-no-remote-browser-cache  
**Date**: 2026-04-12

## Research Tasks

### R1: Current Remote Browser Lifecycle & Billing Impact

**Task**: Understand how the current pooled remote browser model creates Azure Playwright billing.

**Findings**:
- `GenericScriptScraper` holds a singleton `BrowserPool _remotePool` that caches an `IPlaywright` instance and `IBrowser` connection across requests.
- `EnsureBrowserAsync` reuses the cached browser until `ReuseBrowserForRequests` (default 25) is reached, then recycles (close + reconnect).
- The Kubernetes readiness probe (`/ready`) calls `ProbePoolAsync` which previously called `EnsureBrowserAsync` on the remote pool, creating sessions even with zero user traffic.
- Azure Playwright bills per-session based on connection duration. A cached browser stays connected for the lifetime of the pool cycle (25 requests or until it disconnects), generating continuous ~4-minute billable sessions from probe-triggered reconnections.

**Decision**: Replace pooled remote lifecycle with per-request connect/close.
**Rationale**: Per-session billing means any idle connection is wasted spend; the only cost-effective model is to connect only during active scraping.
**Alternatives considered**:
- Increase `ReuseBrowserForRequests` → Still leaves idle sessions between the last request and the next probe cycle; doesn't solve the root cause.
- Disable readiness probe remote probing only → Already done as a partial fix, but the pool still keeps sessions alive between requests.

---

### R2: Per-Request Playwright Runtime Lifecycle

**Task**: Determine whether `IPlaywright` (Node.js subprocess) should also be per-request for remote mode.

**Findings**:
- `Playwright.CreateAsync()` spawns a Node.js subprocess. Typical startup time: ~1-2 seconds.
- The user explicitly chose to dispose both the Playwright runtime AND IBrowser per-request (FR-008).
- For local mode, the Playwright runtime is kept alive in the `_localPool` — no change needed there.
- For remote mode, creating and disposing both per-request means each remote scrape has ~1-2s Playwright startup + up to 30s `ConnectAsync` budget.

**Decision**: Create and dispose both `IPlaywright` and `IBrowser` per-request for remote mode.
**Rationale**: User decision — eliminates any possibility of lingering Node.js processes or stale connection state between requests.
**Alternatives considered**:
- Keep `IPlaywright` alive, dispose only `IBrowser` → Slightly faster but leaves a Node.js process running between requests; user explicitly rejected this.

---

### R3: Concurrency Model for Per-Request Remote Sessions

**Task**: Determine how concurrent remote requests work without a shared pool.

**Findings**:
- Current concurrency is bounded by `_executionSemaphoreRemote` (3 concurrent).
- With per-request lifecycle, each request creates its own `IPlaywright` + `IBrowser` within the semaphore-protected section — no shared state to protect.
- The shared `_remotePool` becomes unnecessary for remote mode. The `_localPool` remains active.
- The simplest approach: in `ExecuteCoreAsync`, when the effective mode is "remote", create a local Playwright+browser, execute, and dispose — bypassing the `_remotePool` entirely.

**Decision**: Each remote request creates its own ephemeral Playwright+browser instance within the semaphore. The `_remotePool` field remains but is only meaningful for local mode (already separate via `_localPool`).
**Rationale**: The semaphore already limits concurrency; per-request instances provide complete isolation.
**Alternatives considered**:
- Pool of pre-connected remote browsers → Defeats the purpose (billing from idle connections).
- Remove `_remotePool` field entirely → Larger refactor with risk; keeping it dormant is safer.

---

### R4: Readiness Probe Behavior for Remote Mode

**Task**: Determine what `/ready` should report for remote mode after this change.

**Findings**:
- The readiness probe was already partially fixed (earlier in this conversation) to not call `EnsureBrowserAsync` for remote.
- With per-request lifecycle, the remote pool will never have a browser between requests, so `pool.HasBrowser` will always be false.
- The probe should report remote as "healthy" when the endpoint is configured (it will connect on-demand) — this matches the partial fix already applied.

**Decision**: Keep the readiness probe behavior as already implemented: report "healthy" when endpoint is configured, regardless of browser state.
**Rationale**: No connection should be made during probes. The configuration being present is sufficient signal.
**Alternatives considered**:
- Report "unknown" or "not-tested" → Adds complexity; "healthy" with a configured endpoint is the pragmatic choice.

---

### R5: Mid-Scrape Disconnect Retry Strategy

**Task**: Define the retry behavior when a remote browser disconnects during scraping.

**Findings**:
- Current code in `ExecuteCoreAsync` already has a catch block for `PlaywrightException` with "Target page, context or browser has been closed" — it calls `ForceRecycleBrowserAsync` then `EnsureBrowserAsync` for one retry.
- In the per-request model, a mid-scrape disconnect means the entire per-request Playwright+browser needs to be recreated and the scrape retried from the beginning.
- User confirmed: one reconnect-and-retry, then fail.

**Decision**: On mid-scrape disconnect, dispose the current Playwright+browser, create a new one, and retry the entire flow once. If the retry also fails, propagate the error and ensure cleanup.
**Rationale**: One retry handles transient Azure issues; more retries compound billing and latency.
**Alternatives considered**:
- No retry → Too brittle for a cloud service; transient disconnects are expected.
- Multiple retries → Compounds billing; diminishing returns after one retry.

---

### R6: Connection Timeout Configuration

**Task**: Determine the appropriate `ConnectAsync` timeout for per-request connections.

**Findings**:
- Current code uses `options.Value.RequestTimeoutSeconds * 1000f` (120s) as the `ConnectAsync` timeout — far too generous for a per-request model.
- User specified a 30-second connection budget.
- The 30-second budget should be applied to the `BrowserTypeConnectOptions.Timeout` parameter.
- The overall request timeout (120s) remains as the outer boundary via `RequestTimeoutMiddleware`.

**Decision**: Set `ConnectAsync` timeout to 30 seconds (30000ms) for remote per-request connections.
**Rationale**: 30s accommodates Azure cold starts while still failing fast enough to be useful.
**Alternatives considered**:
- Keep 120s → Excessive; a connection that takes >30s is likely broken.
- Use a separate config option → Unnecessary complexity; 30s is a good universal default.
