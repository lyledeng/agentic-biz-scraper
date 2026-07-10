# Quickstart: Windows VM Proxy for Chrome Headed Mode

**Feature**: 033-windows-headed-proxy  
**Date**: 2026-04-06

## Prerequisites

- .NET 10 SDK
- Chrome installed locally (for headed mode testing)
- Access to the BizScraper solution

## Local Development Setup

### 1. Configure the Windows Proxy (optional)

The proxy is disabled by default. To test locally, you can run two instances of the API — one as the "AKS" proxy caller, one as the "Windows VM" target.

**Instance A (proxy caller)** — `appsettings.Development.json`:
```json
{
  "WindowsProxy": {
    "EndpointUrl": "https://localhost:8444/mvpoc/bizscraper-api",
    "TimeoutSeconds": 180
  }
}
```

**Instance B (Windows VM target)** — run on port 8444 with no proxy configured:
```bash
dotnet run --project src/BizScraper.Api --urls https://localhost:8444
```

### 2. Update a Definition to Use Windows Mode

Edit `src/BizScraper.Api/Infrastructure/Scraping/Definitions/us-ia-business-search.json`:

```json
"browser": {
  "mode": "windows",
  "channel": "chrome",
  "headless": false
}
```

### 3. Test the Proxy Flow

```bash
# Against Instance A (the proxy):
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-entra-token>" \
  -d '{"definition": "us-ia-business-search", "parameters": {"searchTerm": "Apple"}}'
```

Instance A will forward the request to Instance B, which executes the scraping.

### 4. Verify Health Check

```bash
curl https://localhost:8443/ready
```

Expected response includes:
```json
{
  "status": "Ready",
  "browserModes": { ... },
  "windowsProxy": {
    "status": "healthy",
    "endpoint": "https://localhost:8444/mvpoc/bizscraper-api"
  },
  "definitionsRoutedToWindows": ["us-ia-business-search", "us-ia-entity-details"]
}
```

### 5. Test Without Proxy (Fallback)

Set `WindowsProxy:EndpointUrl` to empty. Definitions with `mode: "windows"` will fall back to the local browser pool.

## Deployment (Assembly Environment)

The CI pipeline handles deployment automatically:

1. **AKS API**: Deployed to `https://devcaas-az.ilienonline.com/mvpoc/bizscraper-api/` with `WindowsProxy:EndpointUrl` pointing to the Windows VM.
2. **Windows VM**: Deployed to `https://aegis.ilienonline.com/mvpoc/bizscraper-api` on ZUSCUDILNAEGIS. SQLite DB is synced from the AKS PVC during deployment.

## Configuration Reference

| Config Key | Default | Description |
|-----------|---------|-------------|
| `WindowsProxy:EndpointUrl` | `""` | Windows VM base URL. Empty = proxy disabled |
| `WindowsProxy:TimeoutSeconds` | `180` | Proxy response timeout in seconds |
