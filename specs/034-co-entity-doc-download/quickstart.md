# Quickstart: CO Entity Certified Document Download

**Feature**: 034-co-entity-doc-download

## Prerequisites

- .NET 10 SDK
- Azure Playwright Workspaces access token (or local Chrome via headed proxy)
- Azure Blob Storage connection string (for certified document PDF upload)
- Existing CO entity details definition (`us-co-entity-details.json`) in place

## No New Configuration

This feature adds actions to the existing `us-co-entity-details.json` definition and post-processing to `GenericScriptScraper`. No new configuration keys are required. Existing blob storage and Playwright settings are reused.

## Run the API

```bash
cd src/BizScraper.Api
dotnet run
```

The API starts on `https://localhost:8443`.

## Test the Feature

Use the generic script execution endpoint with the CO entity details definition:

```bash
# Entity with certified document copies available (SCHOLER WENDYS COLORADO LLC)
curl -k -X POST "https://localhost:8443/api/v1/execute-script" \
  -H "Content-Type: application/json" \
  -d '{
    "definition": "us-co-entity-details",
    "parameters": {
      "detailsUrl": "https://www.sos.state.co.us/biz/BusinessEntityDetail.do?queueMode=PQ&masterFileId=20201222644"
    }
  }'
```

### Expected Response (with certified documents)

```json
{
  "definition": "us-co-entity-details",
  "correlationId": "<guid>",
  "data": {
    "details": {
      "name": "SCHOLER WENDYS COLORADO LLC",
      "identifier": "20201222644",
      "status": "Good Standing",
      "entityType": "Limited Liability Company",
      "jurisdiction": "Colorado"
    },
    "registeredAgent": { "name": "...", "streetAddress": "...", "mailingAddress": null },
    "certificate": {
      "available": true,
      "downloads": [
        {
          "label": "Certificate",
          "proxyUrl": "/api/v1/documents/<blobPrefix>/20201222644-certificateofgoodstanding.pdf",
          "fileName": "20201222644-certificateofgoodstanding.pdf",
          "error": null
        },
        {
          "label": "Certified Documents",
          "proxyUrl": "/api/v1/documents/<blobPrefix>/20201222644-alldocuments.pdf",
          "fileName": "20201222644-alldocuments.pdf",
          "error": null
        }
      ],
      "error": null
    },
    "parties": null,
    "documents": null
  }
}
```

### When "Get certified copies of documents" link is absent

The `certificate.downloads` array will contain only the certificate entry (if available). If neither certificate nor certified documents are present, the `certificate` field will be `null`.

## Diagnostics

Screenshots are saved to the diagnostics folder/blob container with milestones:
- `04-certified-copies-list` — certified copies selection page
- `05-after-certified-docs-extract` — certificate print page with embedded PDF viewer

## Run Tests

```bash
# Unit tests
dotnet test tests/BizScraper.UnitTests

# Integration tests (requires Playwright + blob storage)
dotnet test tests/BizScraper.IntegrationTests
```
