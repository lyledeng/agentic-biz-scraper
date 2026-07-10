# API Contracts: WY Filing History Document Download Fix

**Feature**: 029-wy-history-doc-fix  
**Date**: 2026-04-03

## No Contract Changes

This feature does not introduce or modify any API contracts. All changes are internal to the scraping engine.

### Existing contracts remain unchanged

- **`POST /api/v2/execute-script`** — Request/response schema unchanged. The `historyDocuments[].storageUrl` field in the response will now contain proxy URLs instead of raw source URLs, but the field type (`string?`) and structure are identical.
- **`GET /api/v1/documents/{blobPath}`** — Proxy endpoint unchanged. History documents will be served through this existing endpoint using the same blob path format.

### Response shape (for reference)

The `historyDocuments` array within entity detail responses continues to follow the existing `DocumentEntry` schema:

```json
{
  "documents": [
    {
      "title": "2025 Original Annual Report - 11347360",
      "date": "05/20/2025",
      "downloads": [
        {
          "label": "Filing Document",
          "proxyUrl": "/api/v1/documents/{correlationId}/2025-original-annual-report-11347360.pdf",
          "fileName": "2025-original-annual-report-11347360.pdf",
          "error": null
        }
      ]
    }
  ]
}
```

The only observable difference is that `proxyUrl` will now contain a proxy URL (e.g., `/api/v1/documents/...`) instead of a raw WY SOS URL (e.g., `https://wyobiz.wyo.gov/Business/GetImages.aspx?sid=...`).
