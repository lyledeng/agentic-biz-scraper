# Health Check Response Contract

**Feature**: 021-playwright-local-fallback  
**Date**: 2026-04-01

## GET /ready

Readiness probe for Kubernetes. Validates browser connectivity for the active mode.

### Response 200 OK

```json
{
  "status": "Ready",
  "browserMode": "Local"
}
```

| Field | Type | Values | Description |
|-------|------|--------|-------------|
| `status` | `string` | `"Ready"` | Fixed value when healthy |
| `browserMode` | `string` | `"Local"` \| `"Remote"` | Active browser mode (FR-009) |

### Response 503 Service Unavailable (ProblemDetails)

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.4",
  "title": "Not Ready",
  "status": 503,
  "detail": "Browser connectivity check failed (mode: Remote)."
}
```

| Field | Type | Description |
|-------|------|-------------|
| `detail` | `string` | Includes active browser mode for diagnostics |

## GET /healthz

Liveness probe. Unchanged — returns 200 OK with `{ "status": "Healthy" }`.

---

## Startup Log Contract

At application startup, the active browser mode is logged at `Information` level (FR-008):

```
Browser mode: Local (no BrowserEndpoint configured)
```
or
```
Browser mode: Remote (endpoint: wss://eastus.api.playwright.microsoft.com/...)
```
