# Contract: Germany Hardcopy Printout

**Endpoint**: `POST /api/v1/execute-script` with definition `de-hardcopy-printout`
**Legacy Endpoint**: `POST /api/v1/germany-search/current-hardcopy-printout` [DEPRECATED]
**Tag**: Germany Search
**Name**: GetCurrentHardcopyPrintout

## Request (via execute-script)

```json
{
  "definition": "de-hardcopy-printout",
  "parameters": {
    "searchTerm": "Deutsche Bank",
    "registrationId": "HRB 12345"
  }
}
```

## Request (legacy endpoint)

```json
{
  "searchTerm": "Deutsche Bank",
  "registrationId": "HRB 12345"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| searchTerm | string | ✓ | Company search term |
| registrationId | string | ✓ | Registration ID from search results |

## Response

**200 OK** — Translated PDF file stream

| Header | Value |
|--------|-------|
| Content-Type | application/pdf |
| X-Document-Url | URL of translated document in blob storage |
| X-Original-Document-Url | URL of original German document |
| X-Correlation-Id | Request correlation ID |

## Error Responses

| Status | Error Type | Cause |
|--------|-----------|-------|
| 400 | missing-parameters | Required parameters not provided |
| 404 | registration-not-found | Registration ID not found |
| 404 | hardcopy-not-available | No printout available |
| 413 | document-too-large | PDF exceeds size limit |
| 502 | translation-service-error | FAB translation failed |
| 503 | service-busy | Browser pool exhausted |
| 504 | translation-service-timeout | Translation timed out |
