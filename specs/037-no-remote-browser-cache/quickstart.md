# Quickstart: Disable Remote Browser Caching

**Feature**: 037-no-remote-browser-cache  
**Date**: 2026-04-12

## Verification Steps

### 1. Build and Run Locally (Local Mode)

```bash
# Build
dotnet build src/BizScraper.Api/BizScraper.Api.csproj

# Run in local mode (no BrowserEndpoint configured)
dotnet run --project src/BizScraper.Api
```

Verify local pool behavior is unchanged:
- Execute multiple search requests via test-ui or curl
- Confirm browser reuse via logs (no "Connecting remote browser" messages)

### 2. Run Unit Tests

```bash
dotnet test tests/BizScraper.UnitTests
```

Verify new tests pass for:
- Remote per-request lifecycle (connect → execute → dispose)
- Local pool behavior unchanged
- Readiness probe does not create remote sessions

### 3. Verify Remote Mode (Azure Playwright Workspace)

```bash
# Set remote endpoint (user secrets or env vars)
dotnet user-secrets set "Playwright:BrowserEndpoint" "wss://eastus.api.playwright.microsoft.com/playwrightworkspaces/<id>/browsers"
dotnet user-secrets set "Playwright:AccessToken" "<token>"
```

Execute a scraping request and verify on the Azure Playwright workspace dashboard:
- Session starts when request begins
- Session ends when request completes
- No lingering sessions between requests

### 4. Verify Readiness Probe

```bash
curl -k https://localhost:8443/ready
```

Expected: Remote mode reports "healthy" without creating an Azure Playwright session.

### 5. Verify Concurrent Requests

Fire 3 simultaneous requests to a remote-mode definition:
- Each should create its own Azure Playwright session
- All 3 sessions should close after their respective requests complete
- A 4th concurrent request should wait for a semaphore slot

## Key Log Messages to Watch

| Log Level | Message Pattern | Meaning |
|-----------|----------------|---------|
| Information | "Connecting remote browser for request {CorrelationId}" | Per-request connection starting |
| Information | "Remote browser closed for request {CorrelationId}" | Per-request cleanup complete |
| Warning | "Remote browser disconnected mid-scrape, retrying once" | Mid-scrape retry triggered |
| Information | "Browser mode resolved: remote (source: ...)" | Mode resolution unchanged |
