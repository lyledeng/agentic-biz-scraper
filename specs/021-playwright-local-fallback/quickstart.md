# Quickstart: Playwright Local Fallback

**Feature**: 021-playwright-local-fallback

## Running in Local Mode (default)

No additional configuration needed. The default `appsettings.json` ships with an empty `BrowserEndpoint`, which activates Local mode.

```bash
# From repo root
cd src/BizScraper.Api
dotnet run
```

Verify with:
```bash
curl -k https://localhost:8443/ready
# → {"status":"Ready","browserMode":"Local"}
```

## Running in Remote Mode

Set `BrowserEndpoint` and `AccessToken` to use an Azure Playwright workspace:

```bash
# Via environment variables
export Playwright__BrowserEndpoint="wss://eastus.api.playwright.microsoft.com/playwrightworkspaces/<id>/browsers"
export Playwright__AccessToken="<your-access-key>"
dotnet run
```

Or in `appsettings.Development.json`:
```json
{
  "Playwright": {
    "BrowserEndpoint": "wss://eastus.api.playwright.microsoft.com/playwrightworkspaces/<id>/browsers",
    "AccessToken": "<your-access-key>"
  }
}
```

Verify with:
```bash
curl -k https://localhost:8443/ready
# → {"status":"Ready","browserMode":"Remote"}
```

## Running in Docker (Local Mode)

The Docker image uses `mcr.microsoft.com/playwright/dotnet:v1.58.0-noble` which includes Chromium binaries. Local mode works out of the box:

```bash
docker build -f src/bizscraper-dockerfile -t bizscraper:local .
docker run -p 8443:8443 bizscraper:local
```

## Concurrency

Default concurrency is now 3 (up from 1). Override per definition slug:

```json
{
  "ExecuteScript": {
    "ConcurrencyLimits": {
      "Default": 3,
      "co-search": 5
    }
  }
}
```

## Verifying the Change

1. **Startup log** should show: `Browser mode: Local (no BrowserEndpoint configured)`
2. **Health check** should return `browserMode: "Local"` or `"Remote"`
3. **Script execution** should work identically in both modes
