# API Contract: Generic Script Execution

**Feature**: 016-generic-script-execution  
**Date**: 2026-03-28  
**Base Path**: `/api/v1`

---

## POST /api/v1/execute-script

Execute a scraping flow definition by its slug.

### Request

**Content-Type**: `application/json`

```json
{
  "definition": "co-business-search",
  "parameters": {
    "searchTerm": "Wendys"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | `string` | Yes | The `definitionSlug` identifying which flow to execute. |
| `parameters` | `object` | Yes | Key-value pairs of runtime input variables. Keys must match `variables[source=runtime]` names in the definition. |

### Responses

#### 200 OK

```json
{
  "definition": "co-business-search",
  "correlationId": "0HN8Q1234ABC",
  "truncated": false,
  "data": [ ... ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `definition` | `string` | Echoed definition slug. |
| `correlationId` | `string` | Unique request correlation ID. |
| `truncated` | `boolean` | Whether results were truncated by pagination limits. |
| `data` | `any` | Definition-specific output. Shape varies by definition (array for search, object for entity details). |

**Response Header**: `X-Correlation-Id: <correlationId>`

#### 400 Bad Request — Missing Definition

```json
{
  "type": "https://BizScraper.local/errors/validation-error",
  "title": "Bad Request",
  "status": 400,
  "detail": "The 'definition' field is required and must not be empty."
}
```

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
  "detail": "Missing required parameters for definition 'co-business-search': searchTerm."
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

#### 502 Bad Gateway — Upstream/Scraping Error

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
  "detail": "Definition 'co-business-search' is currently at capacity. Please retry after the indicated interval."
}
```

**Response Header**: `Retry-After: 30`

#### 504 Gateway Timeout

```json
{
  "type": "https://BizScraper.local/errors/scraping-timeout",
  "title": "Gateway Timeout",
  "status": 504,
  "detail": "Flow execution for 'co-business-search' timed out."
}
```

---

## GET /api/v1/definitions

List all available flow definitions and their required parameters.

### Request

No request body. No query parameters.

### Responses

#### 200 OK

```json
[
  {
    "definitionSlug": "co-business-search",
    "name": "Colorado Business Search",
    "description": "Search Colorado Secretary of State business database and extract results",
    "state": "CO",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search for" }
    ]
  },
  {
    "definitionSlug": "co-entity-details",
    "name": "Colorado Entity Details",
    "description": "Extract entity details from Colorado SOS details page",
    "state": "CO",
    "requiredParameters": [
      { "name": "detailsUrl", "description": "Full URL to the entity detail page" }
    ]
  },
  {
    "definitionSlug": "de-business-search",
    "name": "Germany Business Search (Handelsregister)",
    "description": "Search German commercial register",
    "state": "DE",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Company name or keywords" }
    ]
  },
  {
    "definitionSlug": "wy-business-search",
    "name": "Wyoming Business Search",
    "description": "Search Wyoming Secretary of State business filings",
    "state": "WY",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search for" }
    ]
  },
  {
    "definitionSlug": "wy-entity-details",
    "name": "Wyoming Entity Details",
    "description": "Extract entity details from Wyoming business filing details page",
    "state": "WY",
    "requiredParameters": [
      { "name": "detailsUrl", "description": "Full URL to the entity detail page" }
    ]
  }
]
```

---

## Deprecated Endpoints

The following endpoints are marked deprecated in Swagger and will be removed in a future release. Use `POST /api/v1/execute-script` instead.

| Method | Route | Replacement Definition Slug |
|--------|-------|-----------------------------|
| GET | `/api/v1/business-search` | `co-business-search` or `wy-business-search` |
| GET | `/api/v1/entity-details` | `co-entity-details` or `wy-entity-details` |
| GET | `/api/v1/germany-search` | `de-business-search` |

**Note**: `POST /api/v1/germany-search/current-hardcopy-printout` and `GET /api/v1/documents/{blobPath}` are NOT deprecated — they have no equivalent in the generic endpoint (they stream binary PDF responses, which the generic endpoint does not support).
