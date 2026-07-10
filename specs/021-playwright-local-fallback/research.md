# Research: Playwright Local Fallback

**Feature**: 021-playwright-local-fallback  
**Date**: 2026-04-01

## R-001: Browser Mode Detection Strategy

**Task**: Determine how to detect local vs. remote mode from configuration.

**Decision**: Use the presence/absence of `PlaywrightOptions.BrowserEndpoint` as the mode signal. Add a computed property `IsRemoteMode` that returns `true` when `BrowserEndpoint` is non-empty.

**Rationale**: `GenericScriptScraper.EnsureBrowserAsync` already contains this conditional:
```csharp
if (!string.IsNullOrWhiteSpace(endpoint))
    _browser = await _playwright.Chromium.ConnectAsync(endpoint, ...);
else
    _browser = await _playwright.Chromium.LaunchAsync();
```
Formalizing the check into a property on `PlaywrightOptions` makes it reusable for health checks, startup logging, and diagnostics without duplicating the condition.

**Alternatives considered**:
- Explicit `BrowserMode` enum in configuration: Rejected — violates YAGNI. The endpoint presence is unambiguous and already the de facto signal. Adding a separate enum creates a configuration surface where two settings can contradict each other.
- Environment variable `PLAYWRIGHT_MODE`: Rejected — adds a non-standard config key outside the options pattern. The existing `Playwright__BrowserEndpoint` via Helm is sufficient.

## R-002: PlaywrightBrowserManager Removal Scope

**Task**: Identify all references to `PlaywrightBrowserManager` and determine safe removal.

**Decision**: Remove `PlaywrightBrowserManager.cs` and `PlaywrightEntityDetailScraper.cs` (orphaned v1 code superseded by JSON engine in spec 006). Migrate reusable static helpers to `GenericScriptScraper` or a shared utility.

**References found (20 matches)**:

| Location | Action |
|----------|--------|
| `Program.cs` line 86: `AddSingleton<PlaywrightBrowserManager>()` | DELETE registration |
| `HealthCheckEndpoints.cs` line 15: injects `PlaywrightBrowserManager` | REFACTOR to use `GenericScriptScraper` |
| `PlaywrightEntityDetailScraper.cs` line 12: constructor dependency | DELETE file (superseded by `co-entity-details.json` flow, per spec 006 T068) |
| `ScrapingDiagnosticsTracingTests.cs`: 5 references | MIGRATE or DELETE — diagnostics methods stay with PlaywrightBrowserManager removal; may need to move static helpers |
| `ScrapingDiagnosticsScreenshotTests.cs`: 4 references | Same as above |
| `DiagnosticsOptionsTests.cs`: 3 references | Uses `PlaywrightBrowserManager.ResolveDiagnosticsOutputPath` static helper — extract to standalone utility |

**Migration plan for diagnostic helpers**:
- `BuildDiagnosticArtifactPath`, `BuildDiagnosticArtifactBlobPath`, `ResolveDiagnosticsOutputPath`, `SanitizeFileNameSegment` are pure static methods used by tests and the manager itself.
- Option A: Move to a `DiagnosticsPathHelper` static class — keeps tests working with minimal changes.
- Option B: Leave tests referencing `PlaywrightBrowserManager` temporarily, delete in single PR.
- **Chosen**: Option A — move static helpers to `DiagnosticsPathHelper` to cleanly decouple from the removed class.

## R-003: Health Check Refactoring Pattern

**Task**: Determine how the `/ready` endpoint validates browser connectivity after `PlaywrightBrowserManager` removal.

**Decision**: Add a `CanConnectAsync(CancellationToken)` method to `GenericScriptScraper` that attempts `EnsureBrowserAsync` and returns `bool`. The `/ready` endpoint injects `GenericScriptScraper` instead of `PlaywrightBrowserManager`.

**Current `/ready` implementation**:
```csharp
async Task<IResult> (PlaywrightBrowserManager browserManager, CancellationToken ct) =>
{
    var isReady = await browserManager.CanConnectAsync(ct);
    return isReady ? Results.Ok(new { status = "Ready" })
                   : Results.Problem(statusCode: 503, ...);
}
```

**New implementation** adds browser mode to the response:
```csharp
async Task<IResult> (GenericScriptScraper scraper, IOptions<PlaywrightOptions> opts, CancellationToken ct) =>
{
    var isReady = await scraper.CanConnectAsync(ct);
    var mode = opts.Value.IsRemoteMode ? "Remote" : "Local";
    return isReady ? Results.Ok(new { status = "Ready", browserMode = mode })
                   : Results.Problem(statusCode: 503, ...);
}
```

**Rationale**: `GenericScriptScraper` already manages the browser lifecycle for all scraping. Having the health check use the same instance ensures the probe validates the actual browser that serves requests.

**For Local mode**: `CanConnectAsync` calls `EnsureBrowserAsync` which calls `LaunchAsync`. If browser binaries are missing, `LaunchAsync` throws `PlaywrightException`, and the probe returns 503.

## R-004: Concurrency Limit Increase

**Task**: Determine impact of raising default concurrency from 1 to 3.

**Decision**: Change `appsettings.json` value `ExecuteScript:ConcurrencyLimits:Default` from `1` to `3`.

**Rationale**: The concurrency limit controls the `SemaphoreSlim` per definition slug. With browser reuse at 25 requests, 3 concurrent requests per slug is safe for both modes:
- **Local mode**: Each concurrent request gets its own `BrowserContext` on the shared `IBrowser` — Chromium handles this well.
- **Remote mode**: Azure Playwright workspaces support concurrent contexts on a single connection.

**Risk**: Higher memory usage per pod. Mitigated by existing resource limits (512Mi–1Gi) and the 25-request browser recycling threshold.

## R-005: Startup Configuration Validation

**Task**: Determine validation rules for browser mode configuration.

**Decision**: Add `ValidateDataAnnotations()` and custom validation to the `PlaywrightOptions` binding in `Program.cs`:
- If `BrowserEndpoint` is non-empty (Remote mode), validate it starts with `wss://` or `ws://`.
- No validation needed for Local mode (empty endpoint = local).

**Rationale**: Fail-fast on misconfiguration per constitution Design Standards (Configuration). A typo in `BrowserEndpoint` should surface at startup, not on first request.

## R-006: Helm Deployment Changes

**Task**: Determine how to expose `BrowserEndpoint` in Helm deployment.

**Decision**: Add `Playwright__BrowserEndpoint` as an environment variable in `deployment.yaml`, sourced from `values.yaml`. Default to empty string (Local mode). Staging/production overrides set the `wss://` endpoint.

**Current state**: `Playwright__BrowserEndpoint` is NOT set in `deployment.yaml` — the pod falls back to `appsettings.json` which has a hardcoded `wss://` URL. This means all deployments currently run in Remote mode by default.

**New state**: 
- `appsettings.json`: `BrowserEndpoint` set to `""` (Local mode default).
- `values.yaml`: `playwright.browserEndpoint: ""` (Local mode default).
- `deployment.yaml`: `Playwright__BrowserEndpoint` env var from values (overrides appsettings).
- Staging/production: Override via Helm values or external config to set the `wss://` endpoint.

This aligns with FR-001 (default to Local) and FR-004 (explicit opt-in for Remote).
