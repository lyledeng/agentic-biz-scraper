# Quickstart: Germany Business Entity Search

**Feature**: 013-germany-business-search  
**Date**: 2026-03-27

## Prerequisites

- .NET 10 SDK installed
- Node.js 20+ (for test-ui)
- Playwright browsers installed (`pwsh bin/Debug/net10.0/playwright.ps1 install`)
- Azure Playwright workspace access token (for remote browser) OR local Playwright browsers

## Run the API

```bash
cd src/BizScraper.Api
dotnet run
```

The API starts on `https://localhost:8443` (HTTPS only).

## Test the Germany Search Endpoint

### Single result

```bash
curl -k "https://localhost:8443/api/v1/germany-search?name=Rohde%20%26%20Schwarz%20Group%20Services%20GmbH"
```

Expected: 1 result with CompanyName "Rohde & Schwarz Group Services GmbH", RegisteredOffice "Leipzig", Status "currently registered".

### Multiple results with exceeded-hits warning

```bash
curl -k "https://localhost:8443/api/v1/germany-search?name=Rohde"
```

Expected: Up to 100 results across 10 pages, with `warning` field populated.

### No results

```bash
curl -k "https://localhost:8443/api/v1/germany-search?name=xyznonexistentcompany99999"
```

Expected: Empty `results` array, `totalCount: 0`.

### Validation error

```bash
curl -k "https://localhost:8443/api/v1/germany-search?name="
```

Expected: 400 Bad Request with Problem Details.

## Run Unit Tests

```bash
cd tests/BizScraper.UnitTests
dotnet test --filter "FullyQualifiedName~GermanySearch"
```

## Run Integration Tests

```bash
cd tests/BizScraper.IntegrationTests
dotnet test --filter "FullyQualifiedName~GermanySearch"
```

## Run Test-UI

```bash
cd src/test-ui
npm install
ng serve
```

Open `http://localhost:4200`, navigate to the Germany Search tab, enter "Rohde" and click Search.

## Key Files

| File | Purpose |
|------|---------|
| `src/BizScraper.Api/Features/GermanySearch/` | Feature vertical slice (endpoint, handler, models, queries) |
| `src/BizScraper.Api/Infrastructure/Scraping/Definitions/de-business-search.json` | JSON-driven scraping flow definition |
| `deploy/liquibase/changelog/ddl/2.0.2603.4-add-warning-column-to-search-audit.sql` | Database migration |
| `tests/BizScraper.UnitTests/Features/GermanySearch/` | Unit tests |
| `tests/BizScraper.IntegrationTests/Features/GermanySearch/` | Integration tests |

## Configuration

### appsettings.json additions

```json
{
  "Playwright": {
    "GermanySearchUrl": "https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml"
  }
}
```

### appsettings.Development.json

TLS certificate errors are automatically ignored in dev via the existing `IgnoreHTTPSErrors = true` in `BrowserNewContextOptions`.
