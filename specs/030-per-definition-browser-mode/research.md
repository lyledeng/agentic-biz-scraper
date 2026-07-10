# Research: Per-Definition Browser Mode Configuration

**Feature**: 030-per-definition-browser-mode  
**Date**: 2026-04-04

## R-001: Dual Browser Pool Architecture

**Decision**: Extract browser lifecycle into a `BrowserPool` class; `GenericScriptScraper` manages two instances — one for local mode, one for remote mode.

**Rationale**: The current `GenericScriptScraper` holds a single `_browser`, `_playwright`, `_browserLock`, and `_usageCount`. Supporting two simultaneous browser modes requires duplicating these fields. Extracting them into a `BrowserPool` class encapsulates the lifecycle (connect/launch, recycle, dispose) and allows `GenericScriptScraper` to hold two pool instances keyed by mode.

**Alternatives Considered**:
- **Two `GenericScriptScraper` singletons**: Rejected — would require injecting both via DI and routing at the handler level. Too invasive; the handler doesn't know about browser modes.
- **Single pool that reconnects per-request**: Rejected — defeats the purpose of pooling. Reconnecting for every Iowa request would add 2–5s overhead.
- **Dictionary of pools (per-channel)**: Rejected — YAGNI. Only two modes needed: local and remote. A dictionary adds unnecessary abstraction for the current 8 definitions.

## R-002: BrowserConfig Placement in Definition JSON

**Decision**: Add an optional top-level `browser` property on `FlowDefinitionV2` (sibling to `metadata`, `environment`, `variables`, `actions`).

**Rationale**: The `browser` config controls which pool a definition routes to — it's an execution strategy, not an environment setting (viewport, locale, etc.). Placing it as a top-level property keeps it discoverable and separates concerns from `FlowEnvironment`.

**Alternatives Considered**:
- **Nested under `environment`**: Rejected — `FlowEnvironment` controls page-level settings (viewport, user agent, locale). Browser mode is an infrastructure-level setting that determines which pool to use. Mixing them conflates two concerns.
- **External mapping file (slug → mode)**: Rejected — user explicitly requested "configuration at a JSON file level for full flexibility". An external mapping file would be a second config surface to maintain.

## R-003: Effective Mode Resolution

**Decision**: Three-step resolution:
1. If definition has `browser.mode = "local"` → use local pool
2. If definition has `browser.mode = "remote"` → use remote pool (if `BrowserEndpoint` configured; else warn and fallback to local)
3. If definition has no `browser` section → use global default (remote if `BrowserEndpoint` set, else local)

**Rationale**: This preserves full backward compatibility (step 3) while giving definitions explicit control (steps 1–2). The fallback in step 2 prevents runtime failures when remote endpoint is not configured.

**Alternatives Considered**:
- **Strict mode (fail if remote requested but no endpoint)**: Rejected — fragile in local dev environments where `BrowserEndpoint` is typically empty.
- **Per-definition endpoint override**: Rejected — YAGNI. All remote definitions share the same Azure Playwright workspace. Per-definition endpoints would add complexity for no current need.

## R-004: Concurrency Implementation

**Decision**: Each `BrowserPool` has its own `SemaphoreSlim` with a capacity of 3 (configurable via `ExecuteScript:ConcurrencyLimits:Default`). The semaphore is held during the entire browser context lifecycle for a request.

**Rationale**: Independent semaphores prevent local-mode Iowa requests from starving remote-mode requests and vice versa. The existing `SemaphoreSlim(1, 1)` in `GenericScriptScraper._browserLock` is a browser-creation lock (not a concurrency limiter). The actual concurrency limiter is the `_executionSemaphore` — this needs to be split into two.

**Alternatives Considered**:
- **Single shared semaphore of 6**: Rejected — doesn't prevent one mode from monopolizing all slots. A burst of 6 Iowa requests would block all remote requests.

## R-005: Health Check Per-Mode Breakdown

**Decision**: The `/ready` endpoint probes both pools independently and returns a JSON object with per-mode status. Returns `200 OK` if at least one pool is healthy; `503` only if all pools are unhealthy.

**Rationale**: Keeping the pod in the Kubernetes ready pool when at least one mode works ensures definitions using the healthy mode can still be served. The JSON body gives operators visibility into which mode is degraded.

**Alternatives Considered**:
- **Only probe the modes actually used by loaded definitions**: More precise but adds complexity (scanning all definitions at startup to determine required modes). Deferred — can be added later if needed.

## R-006: Chrome Channel Availability in Container

**Decision**: No Dockerfile changes needed. Chrome stable is already installed in the container image (`bizscraper-dockerfile` lines 15–21: `apt install google-chrome-stable`), and `Playwright__BrowserChannel=chrome` is already set as an ENV default.

**Rationale**: Confirmed by reading the current Dockerfile. The Playwright noble base image includes Chromium browsers, and the Dockerfile adds Google Chrome stable on top. The `BrowserChannel` environment variable is already set, though for per-definition mode it's the definition JSON that controls the channel, not the global env var.

**Alternatives Considered**: N/A — no change needed.

## R-007: Backward Compatibility

**Decision**: The `browser` property in `FlowDefinitionV2` is nullable (`BrowserConfig?`). When null or absent in JSON, the system uses the global default. All 6 non-Iowa definitions remain unchanged.

**Rationale**: The JSON schema change is purely additive. Existing definitions without a `browser` section deserialize with `Browser = null`, triggering the fallback to global config. No migration of existing JSON files needed (except Iowa).

**Alternatives Considered**: N/A — this is the only reasonable approach for backward compatibility.
