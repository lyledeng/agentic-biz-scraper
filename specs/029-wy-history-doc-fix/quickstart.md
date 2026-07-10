# Quickstart: WY Filing History Document Download Fix

**Feature**: 029-wy-history-doc-fix  
**Date**: 2026-04-03

## Prerequisites

- .NET 10 SDK
- Node.js 18+ (for test-ui)
- Azure Blob Storage connection string (via User Secrets or Key Vault)
- Playwright browsers installed (`pwsh -c "playwright install chromium"`)

## What Changed

### Backend (`JsonDrivenEntityDetailScraper.cs`)

After downloading each WY filing history document locally (existing behavior), the scraper now:

1. Uploads the document to Azure Blob Storage at `{correlationId}/{fileName}`
2. Builds a proxy URL: `{baseUrl}/api/v1/documents/{correlationId}/{fileName}`
3. Replaces the raw WY SOS `StorageUrl` with the proxy URL
4. On upload failure, records an error on the document (same isolation pattern as download failures)

Two new constructor dependencies: `IHttpContextAccessor` and `IConfiguration` (for `PathBase`).

### Frontend (`entity-details.component.html`)

Document open buttons upgraded from custom `doc-open-btn` to Jumpstart `button-field-pds3 type="secondary" size="small"` with `icon-pds3 name="wk-icon-file-pdf"`. This makes document links visually prominent in the entity details page.

## How to Test

### API (manual)

```bash
# 1. Start the API
dotnet run --project src/BizScraper.Api

# 2. Execute WY entity details for an entity with filing history
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition":"us-wy-entity-details","parameters":{"uniqueKey":"<encoded-key>"}}'

# 3. Verify response: historyDocuments[].storageUrl should contain
#    proxy URLs like /api/v1/documents/{correlationId}/filename.pdf
#    NOT raw wyobiz.wyo.gov URLs

# 4. Fetch a document via proxy URL
curl https://localhost:8443/api/v1/documents/{correlationId}/{filename}
# Should return application/pdf content
```

### Test UI

```bash
cd src/test-ui
npm install
npx ng serve
# Navigate to http://localhost:4200
# 1. Search for a WY business entity (e.g., "Wendy's")
# 2. Click on an entity to view details
# 3. Scroll to Filing History section
# 4. Click any "Filing Document" button — PDF should load in side panel
# 5. Verify buttons use Jumpstart styling (secondary, with PDF icon)
```

### Automated Tests

```bash
# Unit tests
dotnet test tests/BizScraper.UnitTests

# All tests
dotnet test
```

## Files Modified

| File | Change |
|------|--------|
| `src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs` | Add blob upload + proxy URL after local download |
| `src/test-ui/src/app/features/entity-details/entity-details.component.html` | Replace `doc-open-btn` with `button-field-pds3` |
| `src/test-ui/src/app/features/entity-details/entity-details.component.css` | Remove custom button styles, add Jumpstart overrides |
| `src/test-ui/src/app/features/entity-details/entity-details.component.ts` | Add Jumpstart component imports |
| `src/test-ui/src/app/features/entity-details/entity-details.component.spec.ts` | Update test selectors |
| `tests/BizScraper.UnitTests/.../JsonDrivenEntityDetailScraperTests.cs` | Add upload + proxy URL tests |

## No Infrastructure Changes

- No new dependencies
- No database migrations
- No Helm chart changes
- No CI/CD pipeline changes
- No new API endpoints
