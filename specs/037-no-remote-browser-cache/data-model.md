# Data Model: Disable Remote Browser Caching

**Feature**: 037-no-remote-browser-cache  
**Date**: 2026-04-12

## Overview

This feature has no new persistent entities, database changes, or API contract changes. It modifies the internal behavioral model of how `GenericScriptScraper` manages browser lifecycle for remote mode.

## Behavioral Model Changes

### Browser Lifecycle by Mode

| Mode | Before (Current) | After (This Feature) |
|------|-------------------|----------------------|
| **Local** | Pooled `IBrowser` via `_localPool`, reused across requests up to `ReuseBrowserForRequests` threshold | **Unchanged** |
| **Remote** | Pooled `IBrowser` via `_remotePool`, reused across requests up to `ReuseBrowserForRequests` threshold | **Per-request**: create `IPlaywright` + `IBrowser`, execute, dispose both |
| **Windows** | Proxied to Windows VM via `IWindowsProxyService` | **Unchanged** |

### Remote Mode: Per-Request Lifecycle

```text
Request arrives
  ├── Acquire _executionSemaphoreRemote
  ├── Create IPlaywright (Playwright.CreateAsync)
  ├── Connect IBrowser (Chromium.ConnectAsync, timeout=30s)
  ├── Create BrowserContext + Page
  ├── Execute scraping flow
  ├── Post-flow: document downloads (using same browser context)
  ├── Dispose BrowserContext (await using)
  ├── Close + Dispose IBrowser
  ├── Dispose IPlaywright
  └── Release _executionSemaphoreRemote
Response returned
```

### Remote Mode: Mid-Scrape Disconnect Retry

```text
Scraping flow executing...
  └── PlaywrightException: browser closed
      ├── Dispose failed IPlaywright + IBrowser
      ├── Create new IPlaywright
      ├── Connect new IBrowser (timeout=30s)
      ├── Create new BrowserContext + Page
      ├── Retry scraping flow from beginning
      │   ├── Success → normal cleanup
      │   └── Failure → propagate error + cleanup
      └── Guaranteed: dispose IPlaywright + IBrowser in finally block
```

### Readiness Probe: Remote Mode

```text
GET /ready
  └── ProbePoolAsync(_remotePool)
      ├── If endpoint configured → report "healthy" (lazy connect on first real request)
      └── If endpoint not configured → report "not-configured"
      (NO browser connection is made)
```

## Entities Affected

### GenericScriptScraper (Modified)

- `_remotePool` field: retained but dormant for remote mode (only `_localPool` uses the pooling model)
- `ExecuteCoreAsync`: branching on effective mode — local uses pool, remote creates ephemeral instances
- `ProbePoolAsync`: remote branch already fixed to not call `EnsureBrowserAsync`

### BrowserPool (Unchanged)

No changes. The class continues to serve local mode. The `_remotePool` instance exists but its `SetBrowser`/`ClearBrowserAsync` methods will not be called for remote requests.

### PlaywrightOptions (Unchanged)

- `ReuseBrowserForRequests`: only governs local pool behavior
- `RequestTimeoutSeconds`: remains as the outer request timeout; remote `ConnectAsync` uses a fixed 30-second budget
- No new configuration properties added

## State Transitions

### Remote Browser State (Per-Request)

```text
[No Browser] ──request──→ [Connecting] ──success──→ [Active] ──complete──→ [Disposed]
                               │                        │
                               └──fail──→ [Disposed]    └──disconnect──→ [Retry Once]
                                                                              │
                                                                   ┌──success──┤
                                                                   │           └──fail──→ [Disposed + Error]
                                                                   └──→ [Disposed]
```

### Local Browser State (Unchanged)

```text
[No Browser] ──first request──→ [Active (usage=1)]
[Active] ──request──→ [Active (usage++)]
[Active (usage >= threshold)] ──request──→ [Recycle] ──→ [Active (usage=1)]
[Active (disconnected)] ──request──→ [Recycle] ──→ [Active (usage=1)]
```
