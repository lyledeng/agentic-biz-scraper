# Quickstart: Entity Details Endpoint

**Feature**: 004-entity-details-endpoint

## Prerequisites

- .NET 10 SDK
- Azure Playwright Workspaces access token (or local Playwright install)
- Azure Blob Storage connection string (optional — for certificate upload + diagnostics cloud storage)
- SQL Server connection string (optional — for audit trail persistence)

## Configuration

Add the following to `appsettings.Development.json` or environment variables:

```jsonc
{
  "Playwright": {
    "BrowserEndpoint": "wss://<region>.api.playwright.microsoft.com/playwrightworkspaces/<id>/browsers",
    "AccessToken": "<your-access-token>",
    // ...existing search-related settings...
    "Diagnostics": {
      "CloudStorage": {
        "Enabled": true,                        // Required for certificate PDF upload
        "ConnectionString": "<blob-conn-str>",
        "ContainerName": "diagnostics"
      }
    }
  },
  "ConnectionStrings": {
    "AuditTrail": "<sql-connection-string>"   // Optional — skips audit write if empty
  },
  "EntityDetails": {
    "MaxConcurrentRequests": 3                 // Default: 3 concurrent entity detail requests
  }
}
```

## Run the API

```bash
cd src/CososWebScraping.Api
dotnet run
```

The API starts on `https://localhost:8443`.

## Test the Endpoint

```bash
# Retrieve entity details (Good Standing entity — has certificate)
curl -k "https://localhost:8443/api/v1/entity-details?detailsUrl=https%3A%2F%2Fwww.sos.state.co.us%2Fbiz%2FBusinessEntityDetail.do%3FqueueMode%3DPQ%26masterFileId%3D20051063025"

# Retrieve entity details (Delinquent entity — no certificate)
curl -k "https://localhost:8443/api/v1/entity-details?detailsUrl=https%3A%2F%2Fwww.sos.state.co.us%2Fbiz%2FBusinessEntityDetail.do%3FqueueMode%3DPQ%26masterFileId%3D20181871974"
```

### Expected Response (Good Standing)

```json
{
  "details": {
    "name": "ACME CORPORATION",
    "status": "Good Standing",
    "formationDate": "11/02/2005",
    "idNumber": "20051063025",
    "form": "Corporation",
    "periodicReportMonth": "November",
    "jurisdiction": "Colorado",
    "principalOfficeStreetAddress": "123 Main St, Denver, CO 80202",
    "principalOfficeMailingAddress": "PO Box 456, Denver, CO 80201"
  },
  "registeredAgent": {
    "name": "John Smith",
    "streetAddress": "456 Oak Ave, Denver, CO 80203",
    "mailingAddress": "456 Oak Ave, Denver, CO 80203"
  },
  "certificate": {
    "available": true,
    "storageUrl": "https://account.blob.core.windows.net/diagnostics/<correlationId>/20051063025-certificateofgoodstanding.pdf",
    "fileName": "20051063025-certificateofgoodstanding.pdf",
    "error": null
  }
}
```

### Error Responses

| Status | Condition | Example |
|--------|-----------|---------|
| 400 | Invalid/disallowed URL (SSRF protection) | Non-HTTPS, wrong host, wrong path pattern |
| 502 | CO SOS site unreachable or unexpected page structure | Upstream timeout, HTML change |
| 503 | Concurrent request limit reached | Retry-After: 30 header included |

## Run Tests

```bash
# Unit tests
dotnet test tests/CososWebScraping.UnitTests

# Integration tests (requires Docker for Testcontainers SQL Server)
dotnet test tests/CososWebScraping.IntegrationTests

# E2E tests (requires Playwright access + live CO SOS site)
dotnet test tests/CososWebScraping.EndToEndTests
```

## Database Migration

```bash
cd deploy/liquibase
liquibase update --changelog-file=masterfiles/master-1.0.2603.0.yaml
```

The migration creates `dbo.EntityDetailAuditEntries` with rollback support:
```bash
liquibase rollback-count 1 --changelog-file=masterfiles/master-1.0.2603.0.yaml
```
