# Data Model: Per-Definition Browser Mode Configuration

**Feature**: 030-per-definition-browser-mode  
**Date**: 2026-04-04

## New Types

### BrowserConfig

An optional section within a definition JSON that overrides the global browser mode for that specific definition.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `mode` | `string` (enum: `"local"`, `"remote"`) | Yes | — | Browser mode for this definition |
| `channel` | `string?` | No | `null` | Browser channel for local mode (e.g., `"chrome"`, `"msedge"`). Ignored for remote mode. When null, uses bundled Chromium. |
| `headless` | `bool` | No | `true` | Whether to run in headless mode for local launches. Ignored for remote mode. |

**Validation rules**:
- `mode` MUST be `"local"` or `"remote"` (case-insensitive during deserialization).
- If `mode` is `"remote"` and global `BrowserEndpoint` is not configured, the system falls back to local mode and logs a warning.
- `channel` is only meaningful when `mode` is `"local"`. It is silently ignored for remote mode.
- `headless` is only meaningful when `mode` is `"local"`. It is silently ignored for remote mode.

### BrowserPool

Internal class encapsulating browser lifecycle for a single mode.

| Field | Type | Description |
|-------|------|-------------|
| `Mode` | `string` | `"local"` or `"remote"` |
| `Playwright` | `IPlaywright?` | Playwright instance for this pool |
| `Browser` | `IBrowser?` | Current browser instance |
| `BrowserLock` | `SemaphoreSlim` | Serializes browser creation/recycling within this pool |
| `UsageCount` | `int` | Requests served by current browser instance |

**Lifecycle**: Each pool independently tracks usage and recycles the browser after `ReuseBrowserForRequests` (global, default 25) uses. Pools are created lazily on first use. A pool that is never used (e.g., no definitions request local mode) is never created.

## Modified Types

### FlowDefinitionV2

Add optional `Browser` property:

```
FlowDefinitionV2
├── SchemaVersion: string
├── Metadata: FlowMetadata
├── Environment: FlowEnvironment?
├── Browser: BrowserConfig?           ← NEW
├── Variables: IReadOnlyList<VariableV2>
├── Actions: IReadOnlyList<FlowActionV2>
├── Scripts: IReadOnlyDictionary<string, ScriptDefinition>?
└── Output: OutputDeclarationV2
```

### GenericScriptScraper

Replace single browser fields with dual pool:

```
GenericScriptScraper (singleton, IAsyncDisposable)
├── _localPool: BrowserPool              ← NEW (replaces _browser, _playwright, _browserLock, _usageCount)
├── _remotePool: BrowserPool             ← NEW
├── _executionSemaphoreLocal: SemaphoreSlim(3, 3)   ← NEW (replaces single semaphore)
├── _executionSemaphoreRemote: SemaphoreSlim(3, 3)  ← NEW
├── ExecuteAsync(definition, params, correlationId, ct)
│   ├── ResolveEffectiveMode(definition) → "local" | "remote"
│   ├── AcquirePool(effectiveMode) → BrowserPool
│   ├── EnsureBrowserAsync(pool, ct)
│   ├── [create context, execute flow, cleanup]
│   └── return FlowExecutionResult
├── CanConnectAsync(ct) → ReadinessHealthResponse   ← MODIFIED (per-mode)
└── DisposeAsync()                       ← MODIFIED (dispose both pools)
```

### ReadinessHealthResponse

Current: `{ status: "Ready", browserMode: "Remote" }`

New:
```json
{
  "status": "Ready",
  "browserModes": {
    "local": { "status": "healthy", "channel": "chrome" },
    "remote": { "status": "healthy", "endpoint": "wss://..." }
  },
  "definitionsRequiringLocal": ["us-ia-business-search", "us-ia-entity-details"],
  "definitionsRequiringRemote": []
}
```

When at least one mode is healthy → HTTP 200. When all unhealthy → HTTP 503.

## Effective Mode Resolution Algorithm

```
ResolveEffectiveMode(definition):
  1. if definition.Browser is not null:
       if definition.Browser.Mode == "local":
         return "local"
       if definition.Browser.Mode == "remote":
         if global BrowserEndpoint is configured:
           return "remote"
         else:
           log warning "Remote mode requested but no BrowserEndpoint configured; falling back to local"
           return "local"
  2. else (no browser override):
       if global BrowserEndpoint is configured:
         return "remote"
       else:
         return "local"
```

## Definition JSON Changes

### Iowa Business Search (`us-ia-business-search.json`)

Add top-level `browser` section:
```json
{
  "browser": {
    "mode": "local",
    "channel": "chrome",
    "headless": true
  }
}
```

### Iowa Entity Details (`us-ia-entity-details.json`)

Same browser section as above.

### All Other Definitions (6 files)

No changes. `browser` property absent → global default.

## Entity Relationship

```
FlowDefinitionV2 ──has-optional──► BrowserConfig
                                      │
                                      ▼
                              ResolveEffectiveMode()
                                      │
                              ┌───────┴───────┐
                              ▼               ▼
                         BrowserPool      BrowserPool
                         (local)          (remote)
                              │               │
                              ▼               ▼
                         IBrowser          IBrowser
                         (Chrome)          (Azure PW)
```
