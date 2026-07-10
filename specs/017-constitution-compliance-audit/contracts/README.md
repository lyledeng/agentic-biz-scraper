# Contracts: Constitution Compliance Audit

This feature does not introduce new API contracts. Instead, it creates
the `specs/current-specs/contracts/` folder containing documentation
of all existing API contracts.

## Contract Files to Create (in specs/current-specs/contracts/)

### Active Contracts

| Contract File | Endpoint | Method | Description |
|--------------|----------|--------|-------------|
| `execute-script.md` | `/api/v1/execute-script` | POST | Generic script execution — accepts definition slug + parameters |
| `definitions.md` | `/api/v1/definitions` | GET | Lists available scraping definitions and their required parameters |
| `document-proxy.md` | `/api/v1/documents/{blobPath}` | GET | Streams PDF/documents from Azure Blob Storage |
| `germany-hardcopy.md` | `/api/v1/germany-search/current-hardcopy-printout` | POST | Retrieves + translates German hardcopy printout |
| `health-check.md` | `/healthz` | GET | Health check endpoint |

### Deprecated Contracts

| Contract File | Endpoint | Method | Migration |
|--------------|----------|--------|-----------|
| `deprecated-business-search.md` | `/api/v1/business-search` | GET | Use `POST /api/v1/execute-script` with `co-business-search` or `wy-business-search` |
| `deprecated-entity-details.md` | `/api/v1/entity-details` | GET | Use `POST /api/v1/execute-script` with `co-entity-details` or `wy-entity-details` |
| `deprecated-germany-search.md` | `/api/v1/germany-search` | GET | Use `POST /api/v1/execute-script` with `de-business-search` |

## Execute-Script Contract (Primary)

### Request

```json
POST /api/v1/execute-script
Content-Type: application/json

{
  "definition": "co-business-search",
  "parameters": {
    "searchTerm": "Wendys"
  }
}
```

### Response

```json
200 OK
Content-Type: application/json

{
  "definition": "co-business-search",
  "correlationId": "f8eaff94843c473fba64337f38b409d2",
  "truncated": false,
  "data": [...]
}
```

### Available Definitions

| Slug | Required Parameters | Response Data Type |
|------|--------------------|--------------------|
| `co-business-search` | `searchTerm` | `NormalizedSearchResult[]` |
| `wy-business-search` | `searchTerm` | `NormalizedSearchResult[]` |
| `co-entity-details` | `detailsUrl` | `EntityDetailResponse` |
| `wy-entity-details` | `detailsUrl` | `EntityDetailResponse` |
| `de-business-search` | `searchTerm` | `GermanyBusinessResult[]` |

### Error Responses

| Status | Type | Condition |
|--------|------|-----------|
| 400 | `validation-error` | Unknown definition or missing required parameters |
| 422 | `exceeded-record-count` | CO search returned too many results |
| 500 | `unhandled-error` | Unexpected scraping failure |

## Document Proxy Contract

### Request

```
GET /api/v1/documents/{blobPath}
```

### Response

```
200 OK
Content-Type: application/pdf
Content-Disposition: inline

[PDF binary stream]
```

### Headers

| Header | Description |
|--------|-------------|
| `X-Correlation-Id` | Correlation ID from original request |
| `X-Document-Url` | Azure Blob Storage URL of the document |
