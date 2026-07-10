# Quickstart: WY History Document Cloud Storage

**Feature**: 008-wy-history-doc-storage  
**Date**: 2026-03-26

## Prerequisites

- .NET 10 SDK installed
- Azure Playwright Workspace configured (or local Playwright for dev)
- Azure Blob Storage (or Azurite for local development)
- Azure SQL Edge (or Docker for Testcontainers)

## Configuration

### appsettings.json (relevant sections)

```json
{
  "Playwright": {
    "Diagnostics": {
      "CloudStorage": {
        "Enabled": true,
        "ConnectionString": "",
        "ContainerName": "diagnostics"
      }
    }
  }
}
```

**Cloud upload toggle**: `Playwright:Diagnostics:CloudStorage:Enabled`
- `true`: Documents are downloaded, saved locally, uploaded to blob storage, and URLs replaced in response.
- `false` (default): Documents keep their original WY SOS URLs. No download or upload.

**Connection string**: Stored in Azure Key Vault for deployed environments; use .NET User Secrets for local development:
```bash
dotnet user-secrets set "Playwright:Diagnostics:CloudStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
```

## Verifying the Feature

### 1. Call WY entity details with an entity that has history documents

```bash
curl -k "https://localhost:8443/api/v1/entity-details?detailsUrl=https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=094114166207167038170018020254100209134082027099&state=WY"
```

### 2. Verify response `historyDocuments` have cloud storage URLs

```json
{
  "historyDocuments": [
    {
      "eventTitle": "2025 Original Annual Report - 11347360",
      "date": "05/20/2025",
      "storageUrl": "https://<account>.blob.core.windows.net/diagnostics/<correlationId>/2025-original-annual-report-11347360.pdf",
      "fileName": "2025-original-annual-report-11347360.pdf",
      "error": null
    }
  ]
}
```

### 3. Verify local files exist

Check the diagnostics output directory for per-request folders containing the downloaded PDFs:
```
diagnostics/{correlationId}/2025-original-annual-report-11347360.pdf
diagnostics/{correlationId}/2024-original-annual-report-09899282.pdf
```

### 4. Verify audit trail

Query the `EntityDetailAuditEntries` table. The `DocumentStorageUrl` column should contain the blob folder URL:
```sql
SELECT CorrelationId, DocumentStorageUrl, DiagnosticsUrl
FROM dbo.EntityDetailAuditEntries
WHERE StateCode = 'WY' ORDER BY CreatedAt DESC;
```

### 5. Verify disabled behavior

Set `CloudStorage:Enabled` to `false` and call the endpoint. The response should contain original WY SOS URLs (e.g., `https://wyobiz.wyo.gov/Business/GetImages.aspx?...`).

## Running Tests

```bash
# Unit tests
dotnet test tests/BizScraper.UnitTests

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/BizScraper.IntegrationTests
```

## Key Implementation Notes

- Document download uses `IBrowserContext.APIRequestContext.GetAsync()` to inherit session cookies
- Sequential download: one document at a time in listed order
- Content-type validation: only `application/pdf` responses are saved/uploaded
- Upload retries: 3 attempts with exponential backoff (reuses existing `AzureBlobStorageClient` pipeline)
- On upload failure: original WY SOS URL is retained as fallback, `error` field populated
- No additional screenshots during download phase — existing entity details screenshots are sufficient
