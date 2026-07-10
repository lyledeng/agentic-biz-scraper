# Data Model: Playwright Local Fallback

**Feature**: 021-playwright-local-fallback  
**Date**: 2026-04-01

## Entity Changes

### PlaywrightOptions (Modified)

**File**: `src/BizScraper.Api/Common/Configuration/PlaywrightOptions.cs`

| Property | Type | Default | Change | Description |
|----------|------|---------|--------|-------------|
| `BrowserEndpoint` | `string` | `""` | DEFAULT CHANGED from `wss://...` hardcoded | Remote endpoint; empty = Local mode |
| `AccessToken` | `string` | `""` | No change | Azure Playwright access key |
| `IsRemoteMode` | `bool` (computed) | N/A | **NEW** | `=> !string.IsNullOrWhiteSpace(BrowserEndpoint)` |
| `ReuseBrowserForRequests` | `int` | `25` | No change | Browser recycling threshold |
| `RequestTimeoutSeconds` | `int` | `120` | No change | Connection timeout |

### GenericScriptScraper (Modified)

**File**: `src/BizScraper.Api/Infrastructure/Scraping/Engine/GenericScriptScraper.cs`

| Member | Type | Change | Description |
|--------|------|--------|-------------|
| `CanConnectAsync(CancellationToken)` | `Task<bool>` | **NEW** | Health check probe — calls `EnsureBrowserAsync`, returns `_browser.IsConnected` |
| `IsRemoteMode` | `bool` (property) | **NEW** | Delegates to `PlaywrightOptions.IsRemoteMode` |
| `EnsureBrowserAsync(CancellationToken)` | existing | No change | Already has local/remote branching |

### DiagnosticsPathHelper (New)

**File**: `src/BizScraper.Api/Infrastructure/Scraping/DiagnosticsPathHelper.cs`

Static utility class extracted from `PlaywrightBrowserManager` to preserve test-covered diagnostic path logic.

| Method | Signature | Origin |
|--------|-----------|--------|
| `BuildDiagnosticArtifactPath` | `static string (string outputDir, string correlationId, DateTimeOffset timestamp, string milestone, string extension)` | `PlaywrightBrowserManager.BuildDiagnosticArtifactPath` |
| `BuildDiagnosticArtifactBlobPath` | `static string (string correlationId, string artifactPath)` | `PlaywrightBrowserManager.BuildDiagnosticArtifactBlobPath` |
| `ResolveDiagnosticsOutputPath` | `static string (string outputPath, string contentRootPath)` | `PlaywrightBrowserManager.ResolveDiagnosticsOutputPath` |
| `SanitizeFileNameSegment` | `static string (string value)` | `PlaywrightBrowserManager.SanitizeFileNameSegment` |

### Deleted Entities

| Entity | File | Reason |
|--------|------|--------|
| `PlaywrightBrowserManager` | `Infrastructure/Scraping/PlaywrightBrowserManager.cs` | Superseded by `GenericScriptScraper` (FR-007) |
| `PlaywrightEntityDetailScraper` | `Infrastructure/Scraping/PlaywrightEntityDetailScraper.cs` | Orphaned v1 code; superseded by `co-entity-details.json` (spec 006) |

## Configuration Changes

### appsettings.json

| Key | Old Value | New Value | Reason |
|-----|-----------|-----------|--------|
| `Playwright:BrowserEndpoint` | `"wss://eastus.api..."` | `""` | FR-001: default to Local |
| `ExecuteScript:ConcurrencyLimits:Default` | `1` | `3` | FR-010: increase concurrency |

### Helm values.yaml

| Key | Value | Description |
|-----|-------|-------------|
| `playwright.browserEndpoint` | `""` | **NEW**: empty = Local mode; override for Remote |

### Helm deployment.yaml

| Env Var | Source | Description |
|---------|--------|-------------|
| `Playwright__BrowserEndpoint` | `{{ .Values.playwright.browserEndpoint }}` | **NEW**: overrides appsettings.json |

## State Transitions

```
Browser Lifecycle (unchanged for both modes):
  [No Browser] --EnsureBrowserAsync()--> [Connected/Launched]
  [Connected/Launched] --usage < threshold--> [Reused]
  [Connected/Launched] --usage >= threshold OR !IsConnected--> [Recycled] --EnsureBrowserAsync()--> [Connected/Launched]
  [Connected/Launched] --DisposeAsync()--> [Disposed]
```

## Validation Rules

| Rule | Scope | Trigger |
|------|-------|---------|
| `BrowserEndpoint` must start with `wss://` or `ws://` when non-empty | Startup | `ValidateDataAnnotations` / custom validator |
| `ReuseBrowserForRequests` must be > 0 | Startup | Existing (no change) |
