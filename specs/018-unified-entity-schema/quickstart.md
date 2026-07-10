# Quickstart: Unified Entity Schema

**Feature**: 018-unified-entity-schema  
**Date**: 2026-03-29

## What Changed

This feature introduces breaking changes to the BizScraper API:

1. **API version bump**: `POST /api/v2/execute-script` and `GET /api/v2/definitions` replace v1 equivalents
2. **Definition slug rename**: All slugs follow `{country}-{state}-{action}` format
3. **Unified search results**: All search definitions return the same flat array shape with `uniqueKey` replacing `detailsUrl`
4. **Unified entity details**: All entity-details definitions return the same envelope with `details`, `registeredAgent`, `certificate`, `parties`, `documents`
5. **DE entity details**: New `de-de-entity-details` definition replaces standalone hardcopy endpoint
6. **Deprecated endpoints removed**: `business-search`, `entity-details`, `germany-search`, `hardcopy-printout` all gone

## Search (any jurisdiction)

```bash
# Colorado search
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-co-business-search", "parameters": {"searchTerm": "Wendys"}}'

# Wyoming search  
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-wy-business-search", "parameters": {"searchTerm": "Wendys"}}'

# Germany search
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "de-de-business-search", "parameters": {"searchTerm": "Rohde"}}'
```

All three return the same shape:
```json
{
  "definition": "us-co-business-search",
  "correlationId": "...",
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
      "uniqueKey": "eyJzdGF0ZSI6IlVTLUNPIiwi..."
    }
  ]
}
```

## Entity Details (using uniqueKey from search)

```bash
# Use the uniqueKey from any search result
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-co-entity-details", "parameters": {"uniqueKey": "eyJzdGF0ZSI6IlVTLUNPIiwi..."}}'
```

Response contains five nullable sections:
```json
{
  "definition": "us-co-entity-details",
  "correlationId": "...",
  "truncated": false,
  "data": {
    "details": { "name": "...", "identifier": "...", "status": "...", "..." : "..." },
    "registeredAgent": { "name": "...", "streetAddress": "...", "mailingAddress": null },
    "certificate": { "available": true, "downloads": [...], "error": null },
    "parties": null,
    "documents": null
  }
}
```

## Germany Document Download (entity details pattern)

```bash
# DE entity details includes hardcopy document downloads
curl -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "de-de-entity-details", "parameters": {"uniqueKey": "eyJzdGF0ZSI6IkRFLURFIiwi..."}}'
```

Response includes both original and translated PDFs:
```json
{
  "data": {
    "details": { "name": "Rohde & Schwarz GmbH", "identifier": "Amtsgericht München HRB 73851", "..." : "..." },
    "registeredAgent": null,
    "certificate": null,
    "parties": null,
    "documents": [
      {
        "title": "Current Hardcopy Printout",
        "date": null,
        "downloads": [
          { "label": "Original", "proxyUrl": "/api/v1/documents/abc123/de-hardcopy-rohde-schwarz-original.pdf", "fileName": "...", "error": null },
          { "label": "Translated", "proxyUrl": "/api/v1/documents/abc123/de-hardcopy-rohde-schwarz.pdf", "fileName": "...", "error": null }
        ]
      }
    ]
  }
}
```

## Access Document PDFs

```bash
# PDFs served through proxy (unchanged from v1)
curl https://localhost:8443/api/v1/documents/{correlationId}/{filename}.pdf
```

## List Available Definitions

```bash
curl https://localhost:8443/api/v2/definitions
```

## Migration from v1

| v1 Route | v2 Equivalent |
|----------|---------------|
| `GET /api/v1/business-search?state=CO&name=X` | `POST /api/v2/execute-script` with `us-co-business-search` |
| `GET /api/v1/entity-details?detailsUrl=X&state=CO` | `POST /api/v2/execute-script` with `us-co-entity-details` + `uniqueKey` |
| `GET /api/v1/germany-search?name=X` | `POST /api/v2/execute-script` with `de-de-business-search` |
| `POST /api/v1/germany-search/current-hardcopy-printout` | `POST /api/v2/execute-script` with `de-de-entity-details` |
| `POST /api/v1/execute-script` | `POST /api/v2/execute-script` (same body, new slugs) |
| `GET /api/v1/definitions` | `GET /api/v2/definitions` |
| `GET /api/v1/documents/{blobPath}` | Unchanged — stays at v1 |
