# Quickstart: Per-Definition Browser Mode Configuration

## Overview

This feature adds per-definition browser mode overrides to scraping definition JSON files. Definitions can declare whether they should execute on a local Chrome browser or the remote Azure Playwright workspace.

## How It Works

### 1. Add a `browser` section to any definition JSON

```json
{
  "schemaVersion": "1.0.0",
  "metadata": { ... },
  "browser": {
    "mode": "local",
    "channel": "chrome",
    "headless": true
  },
  "variables": [ ... ],
  "actions": [ ... ]
}
```

### 2. Browser Config Options

| Field | Values | Default | Description |
|-------|--------|---------|-------------|
| `mode` | `"local"` or `"remote"` | (required) | Which browser pool to use |
| `channel` | `"chrome"`, `"msedge"`, or `null` | `null` (bundled Chromium) | Local mode only — browser channel |
| `headless` | `true` or `false` | `true` | Local mode only — headless toggle |

### 3. Mode Resolution

1. If definition has `browser.mode` → use that mode
2. If definition has no `browser` section → use global default (remote if `BrowserEndpoint` configured, else local)
3. If definition requests remote but `BrowserEndpoint` is empty → fallback to local with warning

### 4. Verify via Health Check

```bash
curl -k https://localhost:8443/ready
```

Response:
```json
{
  "status": "Ready",
  "browserModes": {
    "local": { "status": "healthy", "channel": "chrome" },
    "remote": { "status": "healthy", "endpoint": "wss://..." }
  },
  "definitionsRequiringLocal": ["us-ia-business-search", "us-ia-entity-details"]
}
```

## Pre-configured Definitions

| Definition | Mode | Reason |
|-----------|------|--------|
| `us-ia-business-search` | Local + Chrome | Iowa SOS anti-bot detection |
| `us-ia-entity-details` | Local + Chrome | Iowa SOS anti-bot detection |
| All others | Global default (typically remote) | No anti-bot issues |

## Local Development

In local dev (no `BrowserEndpoint` configured), all definitions use local mode regardless of their `browser` section. The `browser.channel` setting still applies — Iowa definitions launch Chrome, others use bundled Chromium.

## Concurrency

Each browser pool has an independent concurrency limit of 3 (configurable via `ExecuteScript:ConcurrencyLimits:Default`). Total system-wide capacity is 6 concurrent executions (3 local + 3 remote).
