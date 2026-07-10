# Data Model: Windows VM Proxy for Chrome Headed Mode

**Feature**: 033-windows-headed-proxy  
**Date**: 2026-04-06

## Entities

### WindowsProxyOptions (NEW)

Configuration options for the Windows VM proxy endpoint.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| EndpointUrl | string | "" | Base URL of the Windows VM BizScraper API (e.g., `https://aegis.ilienonline.com/mvpoc/bizscrapper-api`) |
| TimeoutSeconds | int | 180 | Maximum wait time for the proxy response |

**Config section**: `WindowsProxy` in `appsettings.json`

```json
{
  "WindowsProxy": {
    "EndpointUrl": "",
    "TimeoutSeconds": 180
  }
}
```

**Computed property**: `IsConfigured => !string.IsNullOrWhiteSpace(EndpointUrl)`

**File**: `src/BizScraper.Api/Common/Configuration/WindowsProxyOptions.cs`

---

### BrowserConfig (EXISTING — extended behavior)

No schema change. The `Mode` property (already a string) gains a new valid value.

| Mode Value | Behavior |
|-----------|----------|
| `"local"` | Launch local browser (existing) |
| `"remote"` | Connect to Azure Playwright workspace (existing) |
| `"windows"` | Proxy request to Windows VM via `WindowsApiProxy` (NEW) |

**File**: `src/BizScraper.Api/Infrastructure/Scraping/Engine/Models/FlowDefinitionV2.cs` (unchanged)

---

### ReadinessHealthResponse (EXISTING — extended)

Add Windows proxy health status to the existing health response.

**New fields**:

| Field | Type | Description |
|-------|------|-------------|
| WindowsProxy | WindowsProxyStatus? | Health status of the Windows VM proxy (null if not configured) |
| DefinitionsRoutedToWindows | string[] | Slugs of definitions with `mode: "windows"` |

**WindowsProxyStatus record**:

| Field | Type | Description |
|-------|------|-------------|
| Status | string | `"healthy"`, `"unhealthy"`, or `"not-configured"` |
| Endpoint | string? | The configured Windows VM endpoint URL |
| Error | string? | Error message if unhealthy |

**File**: `src/BizScraper.Api/Features/HealthCheck/ReadinessHealthResponse.cs` (or wherever the existing record lives)

---

## Definition File Changes

### us-ia-business-search.json

```json
"browser": {
  "mode": "windows",
  "channel": "chrome",
  "headless": false
}
```

### us-ia-entity-details.json

```json
"browser": {
  "mode": "windows",
  "channel": "chrome",
  "headless": false
}
```

---

## Configuration Changes

### appsettings.json (new section)

```json
{
  "WindowsProxy": {
    "EndpointUrl": "",
    "TimeoutSeconds": 180
  }
}
```

### values.yaml (new section)

```yaml
windowsProxy:
  endpointUrl: ""
  timeoutSeconds: "180"
```

### values-assembly.yaml (new section)

```yaml
windowsProxy:
  endpointUrl: "https://aegis.ilienonline.com/mvpoc/bizscrapper-api"
  timeoutSeconds: "180"
```

### deployment.yaml (new env vars)

```yaml
- name: WindowsProxy__EndpointUrl
  value: {{ .Values.windowsProxy.endpointUrl | quote }}
- name: WindowsProxy__TimeoutSeconds
  value: {{ .Values.windowsProxy.timeoutSeconds | quote }}
```

---

## Routing Decision Flow

```
ExecuteScriptHandler.HandleAsync()
  │
  ├── Load definition by slug
  ├── Validate parameters
  │
  ├── Is definition.Browser.Mode == "windows"?
  │   ├── YES: Is WindowsProxyOptions.IsConfigured?
  │   │   ├── YES: Call WindowsProxyService.ForwardAsync()
  │   │   │         → Forward bearer token + correlation ID
  │   │   │         → Return response directly (skip local post-processing)
  │   │   └── NO:  Fall back to local pool (log warning)
  │   │            → Continue to GenericScriptScraper.ExecuteAsync()
  │   └── NO: Continue to GenericScriptScraper.ExecuteAsync()
  │
  ├── Execute locally (existing flow)
  ├── Apply post-processing mappers
  └── Return response
```

---

## State Diagram: Browser Mode Resolution

```
                    ┌──────────────────┐
                    │  Definition has   │
                    │  browser config?  │
                    └────────┬─────────┘
                       YES   │   NO
                    ┌────────┴─────────┐
                    │                  │
              ┌─────┴──────┐   ┌──────┴──────┐
              │ mode value │   │ Global      │
              └─────┬──────┘   │ default     │
                    │          │ (remote if  │
           ┌───────┼───────┐  │ endpoint)   │
           │       │       │  └─────────────┘
      "local"  "remote" "windows"
           │       │       │
           ▼       ▼       ▼
        Local    Remote   ┌──────────────┐
        Pool     Pool     │ WinProxy     │
                          │ configured?  │
                          └──────┬───────┘
                           YES   │   NO
                         ┌───────┴──────┐
                         ▼              ▼
                    WindowsProxyService  Local Pool
                                       (fallback)
```
