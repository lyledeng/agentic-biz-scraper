# Contract: WY Entity Details Certificate Section

**Definition**: `us-wy-entity-details`  
**Endpoint**: `POST /api/v2/execute-script`  
**Date**: 2026-03-31

---

## Request

```json
{
  "definition": "us-wy-entity-details",
  "parameters": {
    "uniqueKey": "<base64url-encoded US-WY key>"
  }
}
```

## Response — Entity in Good Standing

```json
{
  "definition": "us-wy-entity-details",
  "correlationId": "abc123",
  "truncated": false,
  "data": {
    "details": {
      "name": "Wendy's Technology, LLC",
      "identifier": "2017-000757675",
      "status": "Active",
      "formationDate": "06/13/2017",
      "entityType": "Limited Liability Company - Foreign",
      "jurisdiction": "Delaware",
      "standingTax": "Good",
      "standingRA": "Good",
      "standingOther": "Good"
    },
    "registeredAgent": {
      "name": "United Agent Group Inc.",
      "streetAddress": "5830 E 2nd StCasper, WY 82609 USA"
    },
    "certificate": {
      "available": true,
      "downloads": [
        {
          "label": "Certificate of Good Standing",
          "proxyUrl": "https://host/api/v1/documents/abc123/2017-000757675-certificateofgoodstanding.pdf",
          "fileName": "2017-000757675-certificateofgoodstanding.pdf"
        }
      ]
    },
    "parties": [],
    "documents": [
      {
        "title": "2025 Original Annual Report - 11347360",
        "date": "05/20/2025",
        "downloads": [
          {
            "label": "Filing Document",
            "proxyUrl": "https://host/api/v1/documents/abc123/2025-original-annual-report-11347360.pdf",
            "fileName": "2025-original-annual-report-11347360.pdf"
          }
        ]
      }
    ]
  }
}
```

## Response — Entity NOT in Good Standing

```json
{
  "definition": "us-wy-entity-details",
  "correlationId": "def456",
  "truncated": false,
  "data": {
    "details": {
      "name": "Wendy S. Jacobs, LLC",
      "identifier": "2015-000679087",
      "status": "Active",
      "standingTax": "Delinquent",
      "standingRA": "Good"
    },
    "registeredAgent": { "...": "..." },
    "certificate": {
      "available": false,
      "error": "Entity is not in good standing. Certificates may only be printed when the entity Standing is Good."
    },
    "parties": [],
    "documents": []
  }
}
```

## Response — Certificate Flow Timeout

```json
{
  "data": {
    "details": { "...": "..." },
    "certificate": null,
    "parties": [],
    "documents": []
  }
}
```

Certificate is `null` (not attempted or timed out) — entity details
still returned without interruption.

---

## V1 Endpoint Removal

### Removed Routes

| Former Route | Status |
|-------------|--------|
| `GET /api/v1/business-search` | Removed (404) |
| `GET /api/v1/entity-details` | Already removed (spec 018) |

### Retained 410 Gone Tombstones

| Route | Response |
|-------|----------|
| `POST /api/v1/execute-script` | 410 Gone — use `/api/v2/execute-script` |
| `GET /api/v1/definitions` | 410 Gone — use `/api/v2/definitions` |

### Retained Active Endpoints

| Route | Purpose |
|-------|---------|
| `POST /api/v2/execute-script` | All scraping operations |
| `GET /api/v2/definitions` | List available definitions |
| `GET /api/v1/documents/{blobPath}` | PDF proxy (no breaking change) |
