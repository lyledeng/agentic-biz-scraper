# API Contract: Execute Script v2

**Feature**: 018-unified-entity-schema  
**Date**: 2026-03-29  
**Version**: `2.0.2603.0` (MAJOR bump — breaking change: v2 prefix, unified response shapes, new definition slugs)

---

## POST /api/v2/execute-script

Execute a scraping flow definition by its slug. Returns unified response shapes.

### Request

**Content-Type**: `application/json`

```json
{
  "definition": "us-co-business-search",
  "parameters": {
    "searchTerm": "Wendys"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | `string` | Yes | Definition slug in `{country}-{state}-{action}` format. |
| `parameters` | `object` | Yes | Key-value pairs of runtime variables. |

### Available Definitions

| Slug | Parameters | Response `data` Shape |
|------|------------|----------------------|
| `us-co-business-search` | `searchTerm` | `UnifiedSearchResult[]` |
| `us-wy-business-search` | `searchTerm` | `UnifiedSearchResult[]` |
| `de-de-business-search` | `searchTerm` | `UnifiedSearchResult[]` |
| `us-co-entity-details` | `uniqueKey` | `UnifiedEntityDetailResponse` |
| `us-wy-entity-details` | `uniqueKey` | `UnifiedEntityDetailResponse` |
| `de-de-entity-details` | `uniqueKey` | `UnifiedEntityDetailResponse` |

### Response — 200 OK (Search)

```json
{
  "definition": "us-co-business-search",
  "correlationId": "f8eaff94843c473fba64337f38b409d2",
  "truncated": false,
  "data": [
    {
      "name": "Wendy's of Limon",
      "identifier": "20201849877",
      "status": "Effective",
      "entityType": "FPC",
      "formationDate": "09/30/2020",
      "state": "CO",
      "event": "Trade Name",
      "uniqueKey": "eyJzdGF0ZSI6IlVTLUNPIiwicGFyYW1zIjp7InF1aXRCdX...",
      "standingTax": null,
      "standingRA": null,
      "registeredOffice": null
    }
  ]
}
```

### Response — 200 OK (Entity Details — WY example)

```json
{
  "definition": "us-wy-entity-details",
  "correlationId": "a1b2c3d4e5f6",
  "truncated": false,
  "data": {
    "details": {
      "name": "Wendy's Technology, LLC",
      "identifier": "2017-000757675",
      "status": "Active",
      "formationDate": "06/13/2017",
      "entityType": "Limited Liability Company - Foreign",
      "jurisdiction": "Delaware",
      "principalAddress": "One Dave Thomas Blvd, Dublin, OH 43017 USA",
      "mailingAddress": "One Dave Thomas Blvd, Dublin, OH 43017 USA",
      "subStatus": "Current",
      "standingTax": "Good",
      "standingRA": "Good",
      "standingOther": "Good",
      "inactiveDate": null,
      "termOfDuration": "Perpetual",
      "formedIn": "Delaware",
      "latestAnnualReportYear": "11347360 / 2025",
      "annualReportExempt": "No",
      "licenseTaxPaid": "$60.00"
    },
    "registeredAgent": {
      "name": "United Agent Group Inc.",
      "streetAddress": "5830 E 2nd St, Casper, WY 82609 USA",
      "mailingAddress": null
    },
    "certificate": null,
    "parties": [
      {
        "name": "John Smith",
        "role": "Manager",
        "organization": null,
        "address": "123 Main St, Dublin, OH 43017"
      }
    ],
    "documents": [
      {
        "title": "2025 Original Annual Report - 11347360",
        "date": "05/20/2025",
        "downloads": [
          {
            "label": "Filing Document",
            "proxyUrl": "/api/v1/documents/a1b2c3d4e5f6/2025-original-annual-report-11347360.pdf",
            "fileName": "2025-original-annual-report-11347360.pdf",
            "error": null
          }
        ]
      }
    ]
  }
}
```

### Response — 200 OK (Entity Details — DE example)

```json
{
  "definition": "de-de-entity-details",
  "correlationId": "f33b2bc256794e0f",
  "truncated": false,
  "data": {
    "details": {
      "name": "Rohde & Schwarz Group Services GmbH",
      "identifier": "Amtsgericht München HRB 73851",
      "status": "aktuell",
      "formationDate": null,
      "entityType": null,
      "jurisdiction": null,
      "principalAddress": null,
      "mailingAddress": null,
      "registeredOffice": "München"
    },
    "registeredAgent": null,
    "certificate": null,
    "parties": null,
    "documents": [
      {
        "title": "Current Hardcopy Printout",
        "date": null,
        "downloads": [
          {
            "label": "Original",
            "proxyUrl": "/api/v1/documents/f33b2bc256794e0f/de-hardcopy-rohde-schwarz-original.pdf",
            "fileName": "de-hardcopy-rohde-schwarz-original.pdf",
            "error": null
          },
          {
            "label": "Translated",
            "proxyUrl": "/api/v1/documents/f33b2bc256794e0f/de-hardcopy-rohde-schwarz.pdf",
            "fileName": "de-hardcopy-rohde-schwarz.pdf",
            "error": null
          }
        ]
      }
    ]
  }
}
```

### Response — 200 OK (DE Entity Details — Translation Failure)

```json
{
  "definition": "de-de-entity-details",
  "correlationId": "f33b2bc256794e0f",
  "truncated": false,
  "data": {
    "details": { "..." : "..." },
    "registeredAgent": null,
    "certificate": null,
    "parties": null,
    "documents": [
      {
        "title": "Current Hardcopy Printout",
        "date": null,
        "downloads": [
          {
            "label": "Original",
            "proxyUrl": "/api/v1/documents/f33b2bc256794e0f/de-hardcopy-rohde-schwarz-original.pdf",
            "fileName": "de-hardcopy-rohde-schwarz-original.pdf",
            "error": null
          },
          {
            "label": "Translated",
            "proxyUrl": null,
            "fileName": "de-hardcopy-rohde-schwarz.pdf",
            "error": "FAB translation service returned HTTP 503. Original document available."
          }
        ]
      }
    ]
  }
}
```

### Error Responses

#### 400 Bad Request — Unknown Definition

```json
{
  "type": "https://BizScraper.local/errors/unknown-definition",
  "title": "Bad Request",
  "status": 400,
  "detail": "No definition found matching 'xyz-unknown'."
}
```

#### 400 Bad Request — Missing Required Parameters

```json
{
  "type": "https://BizScraper.local/errors/missing-parameters",
  "title": "Bad Request",
  "status": 400,
  "detail": "Missing required parameters for definition 'us-co-business-search': searchTerm."
}
```

#### 422 Unprocessable Entity — Exceeded Record Count

```json
{
  "type": "https://BizScraper.local/errors/exceeded-record-count",
  "title": "Exceeded Record Count",
  "status": 422,
  "detail": "The search term is too broad. The CO SOS site reported: Exceeded Record Count."
}
```

#### 502 Bad Gateway — Scraping Error

```json
{
  "type": "https://BizScraper.local/errors/scraping-error",
  "title": "Scraping Error",
  "status": 502,
  "detail": "Flow 'Colorado Business Search' failed: <reason>"
}
```

#### 503 Service Unavailable — Concurrency Limit

```json
{
  "type": "https://BizScraper.local/errors/service-busy",
  "title": "Service Busy",
  "status": 503,
  "detail": "Definition 'us-co-business-search' is currently at capacity."
}
```

**Response Header**: `Retry-After: 30`

#### 504 Gateway Timeout

```json
{
  "type": "https://BizScraper.local/errors/scraping-timeout",
  "title": "Gateway Timeout",
  "status": 504,
  "detail": "Flow execution for 'us-co-business-search' timed out."
}
```

---

## GET /api/v2/definitions

List all available flow definitions and their required parameters.

### Response — 200 OK

```json
[
  {
    "definitionSlug": "us-co-business-search",
    "name": "Colorado Business Search",
    "description": "Search Colorado SOS business database",
    "state": "CO",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search for" }
    ]
  },
  {
    "definitionSlug": "us-co-entity-details",
    "name": "Colorado Entity Details",
    "description": "Retrieve entity details from Colorado SOS",
    "state": "CO",
    "requiredParameters": [
      { "name": "uniqueKey", "description": "Opaque key from search results" }
    ]
  },
  {
    "definitionSlug": "us-wy-business-search",
    "name": "Wyoming Business Search",
    "state": "WY",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search for" }
    ]
  },
  {
    "definitionSlug": "us-wy-entity-details",
    "name": "Wyoming Entity Details",
    "state": "WY",
    "requiredParameters": [
      { "name": "uniqueKey", "description": "Opaque key from search results" }
    ]
  },
  {
    "definitionSlug": "de-de-business-search",
    "name": "Germany Business Search",
    "state": "DE",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Company name or keywords" }
    ]
  },
  {
    "definitionSlug": "de-de-entity-details",
    "name": "Germany Entity Details",
    "state": "DE",
    "requiredParameters": [
      { "name": "uniqueKey", "description": "Opaque key from search results" }
    ]
  }
]
```

---

## Deprecated v1 Routes

### POST /api/v1/execute-script → 410 Gone

```json
{
  "type": "https://BizScraper.local/errors/gone",
  "title": "Gone",
  "status": 410,
  "detail": "This endpoint has moved to /api/v2/execute-script."
}
```

### GET /api/v1/definitions → 410 Gone

```json
{
  "type": "https://BizScraper.local/errors/gone",
  "title": "Gone",
  "status": 410,
  "detail": "This endpoint has moved to /api/v2/definitions."
}
```

### GET /api/v1/business-search → 404 Not Found

Route removed entirely.

### GET /api/v1/entity-details → 404 Not Found

Route removed entirely.

### GET /api/v1/germany-search → 404 Not Found

Route removed entirely.

### POST /api/v1/germany-search/current-hardcopy-printout → 404 Not Found

Route removed entirely.

---

## Unchanged Endpoint

### GET /api/v1/documents/{blobPath}

No changes. Continues to serve PDFs from blob storage with the same contract as feature 011.
