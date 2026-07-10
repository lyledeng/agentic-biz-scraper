# Contract: Unified Search Result Schema

**Feature**: 018-unified-entity-schema  
**Date**: 2026-03-29  
**Version**: `2.0.2603.0`

## UnifiedSearchResult

All search definitions (`*-business-search`) return `data` as a flat array of this shape.

```json
{
  "name": "string (required)",
  "identifier": "string (required)",
  "status": "string (required)",
  "entityType": "string | null",
  "formationDate": "string | null",
  "state": "string (required) — CO | WY | DE",
  "event": "string | null",
  "uniqueKey": "string (required) — base64url-encoded opaque token",
  "standingTax": "string | null — WY only",
  "standingRA": "string | null — WY only",
  "registeredOffice": "string | null — DE only"
}
```

## UnifiedEntityDetailResponse

All entity-details definitions (`*-entity-details`) return `data` as this shape.

```json
{
  "details": {
    "name": "string (required)",
    "identifier": "string (required)",
    "status": "string (required)",
    "formationDate": "string | null",
    "entityType": "string | null",
    "jurisdiction": "string | null",
    "principalAddress": "string | null",
    "mailingAddress": "string | null",
    "periodicReportMonth": "string | null — CO",
    "subStatus": "string | null — WY",
    "standingTax": "string | null — WY",
    "standingRA": "string | null — WY",
    "standingOther": "string | null — WY",
    "inactiveDate": "string | null — WY",
    "termOfDuration": "string | null — WY",
    "formedIn": "string | null — WY",
    "latestAnnualReportYear": "string | null — WY",
    "annualReportExempt": "string | null — WY",
    "licenseTaxPaid": "string | null — WY",
    "registeredOffice": "string | null — DE"
  },
  "registeredAgent": {
    "name": "string | null",
    "streetAddress": "string | null",
    "mailingAddress": "string | null"
  },
  "certificate": {
    "available": "boolean",
    "downloads": [
      {
        "label": "string",
        "proxyUrl": "string | null",
        "fileName": "string",
        "error": "string | null"
      }
    ],
    "error": "string | null"
  },
  "parties": [
    {
      "name": "string",
      "role": "string",
      "organization": "string | null",
      "address": "string | null"
    }
  ],
  "documents": [
    {
      "title": "string",
      "date": "string | null",
      "downloads": [
        {
          "label": "string — 'Filing Document' | 'Original' | 'Translated'",
          "proxyUrl": "string | null — relative path to /api/v1/documents/{blobPath}",
          "fileName": "string",
          "error": "string | null"
        }
      ]
    }
  ]
}
```

**Nullable sections**: `registeredAgent`, `certificate`, `parties`, `documents` are each `null` when not applicable for the jurisdiction.

## Mapping by Jurisdiction

| Section | US-CO | US-WY | DE-DE |
|---------|-------|-------|-------|
| `details` | Full | Full + WY extensions | Basic (name, identifier, status, registeredOffice) |
| `registeredAgent` | Populated | Populated | `null` |
| `certificate` | Populated (if available) | Populated (if available) | `null` |
| `parties` | `null` | Populated | `null` |
| `documents` | `null` | History filings (1 download each) | Hardcopy (2 downloads: Original + Translated) |
