# Quickstart: Generic Script Execution Endpoint

**Feature**: 016-generic-script-execution  
**Date**: 2026-03-28

## Prerequisites

- BizScraper API running locally (port 8443)
- At least one JSON flow definition loaded (e.g., `co-business-search.json`)

## 1. Execute a Business Search

```bash
curl -k -X POST https://localhost:8443/api/v1/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "co-business-search", "parameters": {"searchTerm": "Wendys"}}'
```

**Expected response** (200 OK):
```json
{
  "definition": "co-business-search",
  "correlationId": "0HN8Q...",
  "truncated": false,
  "data": [
    {
      "documentNumber": "20181871974",
      "name": "WENDYS INTERNATIONAL LLC",
      "event": "Report",
      "status": "Good Standing",
      "form": "Foreign Limited Liability Company",
      "formationDate": "10/18/2018",
      "detailsUrl": "https://..."
    }
  ]
}
```

## 2. Execute Entity Details

```bash
curl -k -X POST https://localhost:8443/api/v1/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "co-entity-details", "parameters": {"detailsUrl": "https://www.sos.state.co.us/biz/BusinessEntityDetail.do?..."}}'
```

## 3. Execute Germany Search

```bash
curl -k -X POST https://localhost:8443/api/v1/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "de-business-search", "parameters": {"searchTerm": "Rohde"}}'
```

## 4. List Available Definitions

```bash
curl -k https://localhost:8443/api/v1/definitions
```

**Expected response** (200 OK):
```json
[
  {
    "definitionSlug": "co-business-search",
    "name": "Colorado Business Search",
    "state": "CO",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search for" }
    ]
  },
  ...
]
```

## 5. Error Scenarios

### Unknown definition
```bash
curl -k -X POST https://localhost:8443/api/v1/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "unknown", "parameters": {}}'
```
**Expected**: 400 with `"No definition found matching 'unknown'."`

### Missing required parameter
```bash
curl -k -X POST https://localhost:8443/api/v1/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "co-business-search", "parameters": {}}'
```
**Expected**: 400 with `"Missing required parameters for definition 'co-business-search': searchTerm."`

## 6. Test UI

Navigate to `http://localhost:4200`. All search and entity-details operations now go through the generic endpoint. The UI behavior is identical — only the underlying API call has changed.
