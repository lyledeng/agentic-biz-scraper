# Quickstart: PDF Proxy Streaming Endpoint

**Feature**: 011-pdf-proxy-streaming

## Prerequisites

- .NET 10 SDK
- Azure Blob Storage connection string with read access to the `diagnostics` container
- An existing entity detail result with uploaded PDFs (run an entity-details query first)

## Configuration

No new configuration required. The proxy endpoint reuses the existing `CloudStorage` settings:

```jsonc
{
  "Playwright": {
    "Diagnostics": {
      "CloudStorage": {
        "Enabled": true,                        // Must be true for proxy to work
        "ConnectionString": "<blob-conn-str>",  // Must have read + list permissions
        "ContainerName": "diagnostics"           // Default container
      }
    }
  }
}
```

## Run the API

```bash
cd src/BizScraper.Api
dotnet run
```

The API starts on `https://localhost:8443`.

## Test the Proxy Endpoint

### Step 1: Get entity details (creates PDFs in storage)

```bash
curl -k "https://localhost:8443/api/v1/entity-details?detailsUrl=https%3A%2F%2Fwyobiz.wyo.gov%2FBusiness%2FFilingDetails.aspx%3FeFNum%3D094114166207167038170018020254100209134082027099&state=WY"
```

Note the `storageUrl` values in the response — they now point to the proxy endpoint:

```json
{
  "certificate": {
    "storageUrl": "https://localhost:8443/api/v1/documents/{correlationId}/2017-000757675-certificateofgoodstanding.pdf"
  },
  "historyDocuments": [
    {
      "storageUrl": "https://localhost:8443/api/v1/documents/{correlationId}/2025-original-annual-report-11347360.pdf"
    }
  ]
}
```

### Step 2: Stream a document through the proxy

```bash
# View inline in browser (default)
curl -k "https://localhost:8443/api/v1/documents/{correlationId}/{filename}.pdf"

# Force download
curl -k "https://localhost:8443/api/v1/documents/{correlationId}/{filename}.pdf?download=true" -o output.pdf
```

### Step 3: Test conditional caching

```bash
# First request — returns 200 with ETag
curl -k -v "https://localhost:8443/api/v1/documents/{correlationId}/{filename}.pdf"
# Note the ETag header in the response

# Second request with If-None-Match — returns 304
curl -k -v -H 'If-None-Match: "{etag-from-previous}"' \
  "https://localhost:8443/api/v1/documents/{correlationId}/{filename}.pdf"
```

### Step 4: Test in the Angular UI

1. Open the test UI at `http://localhost:4200`
2. Search for a WY entity (e.g., "Wendy's Technology")
3. Click an entity to view details
4. Click a certificate or history document link
5. The PDF opens in a new tab via the API proxy

## Error Responses

| Status | Condition | Example |
|--------|-----------|---------|
| 400 | Invalid blob path (empty, `..`, forbidden chars) | `GET /api/v1/documents/../../etc/passwd` |
| 404 | Blob does not exist in the container | `GET /api/v1/documents/nonexistent/file.pdf` |
| 502 | Storage account unreachable after retries | Azure outage, connection string invalid |
| 504 | Download exceeds 30-second timeout | Very large file or slow network |

## Run Tests

```bash
# Unit tests
dotnet test tests/BizScraper.UnitTests --filter "FullyQualifiedName~Documents"

# Integration tests (requires Azurite or real storage)
dotnet test tests/BizScraper.IntegrationTests --filter "FullyQualifiedName~Documents"
```
