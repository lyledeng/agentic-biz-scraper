# QuickStart: Fix Iowa Search and Test UI

**Feature**: 024-ia-search-testui-fix  
**API Version**: v2 (execute-script)  
**Date**: 2026-04-01  

---

## Overview

This feature fixes the non-functional Iowa business search and entity details by deploying complete v2 script definitions and extending the unified entity models with Iowa-specific fields.

**What changed**:
- Stub definitions replaced with complete v2 definitions (search: 11 actions, details: 16+ actions)
- `DetailSection` extended with `chapterCode`, `certificateNote`, `iowaNames` (nullable)
- New `IowaNameEntry` record for structured names list
- `MapIaDetails` updated to populate new explicit fields
- Test UI entity detail view renders Iowa-specific sections
- Global specification snapshot (`specs/current-specs/`) updated

---

## Verify Iowa Search

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "definition": "us-ia-business-search",
  "parameters": {
    "searchTerm": "Acme"
  }
}
```

### Expected Response

```json
{
  "definition": "us-ia-business-search",
  "correlationId": "...",
  "truncated": false,
  "data": [
    {
      "identifier": "457975",
      "name": "Acme Inc",
      "status": "Active",
      "entityType": "Legal",
      "formationDate": null,
      "uniqueKey": "eyJzdGF0ZSI6IklBIiwi...",
      "state": "IA"
    }
  ]
}
```

---

## Verify Iowa Entity Details

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "definition": "us-ia-entity-details",
  "parameters": {
    "uniqueKey": "<uniqueKey from search result>"
  }
}
```

### Expected Response (with new Iowa fields)

```json
{
  "definition": "us-ia-entity-details",
  "correlationId": "...",
  "data": {
    "details": {
      "name": "Acme Incorporated",
      "identifier": "457975",
      "status": "Active",
      "entityType": "Legal",
      "formationDate": "2005-03-15",
      "formedIn": "IA",
      "chapterCode": "CODE 489 DOMESTIC LIMITED LIABILITY COMPANY",
      "certificateNote": "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)",
      "iowaNames": [
        {
          "name": "Acme Incorporated",
          "type": "Legal",
          "status": "Active",
          "modified": false
        },
        {
          "name": "Acme Inc",
          "type": "Fictitious",
          "status": "Active",
          "modified": true
        }
      ]
    },
    "registeredAgent": {
      "name": "John Smith",
      "streetAddress": "123 Main St, Des Moines, IA 50309",
      "mailingAddress": null
    },
    "certificate": {
      "available": false,
      "downloads": null,
      "error": null
    },
    "parties": [],
    "documents": null
  }
}
```

---

## Verify Test UI

1. Navigate to test UI at `https://<host>/mvpoc/bizscraper-ui`
2. Select **Iowa (IA)** from the state dropdown
3. Enter a search term (e.g., "Farm") and click Search
4. Verify results appear in the grid with identifier, name, status, entityType columns
5. Click a result row to drill down
6. Verify the detail view shows:
   - Standard fields: name, identifier, status, entity type, formation date
   - **Iowa-specific**: Chapter/Code row, Certificate Note banner, Iowa Names table

---

## Regression Check

Verify existing states are unaffected:

```bash
# Colorado search
curl -X POST localhost:5000/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition":"us-co-business-search","parameters":{"searchTerm":"Wendys"}}'

# Wyoming search
curl -X POST localhost:5000/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition":"us-wy-business-search","parameters":{"searchTerm":"Wendys"}}'
```

Both should return results identical to pre-fix behavior. The new nullable Iowa fields (`chapterCode`, `certificateNote`, `iowaNames`) will be `null` for non-Iowa entities.

---

## Error Scenarios

| Scenario | Expected |
|----------|----------|
| Iowa SOS unreachable | 503 with diagnostic screenshots |
| Empty search results | 200 with empty `data[]`, `truncated: false` |
| Invalid uniqueKey | 400 with descriptive error |
| Concurrent IA request | 503 with `Retry-After` header |
| CAPTCHA/bot detection | 429 with diagnostic screenshots |
