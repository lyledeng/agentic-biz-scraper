# API Contract: Current Hardcopy Printout Endpoint

**Feature**: 015-de-hardcopy-printout
**Date**: 2026-03-27

## POST /api/v1/germany-search/current-hardcopy-printout

Retrieves the German "Aktueller Druck" (AD) document from the Handelsregister for a specific business registration, translates it to English via the FAB agent, generates an English PDF, and returns it.

### Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| `searchTerm` | body (JSON) | string | ✅ | Company name to search (e.g., "Claes und Rohde") |
| `registrationId` | body (JSON) | string | ✅ | Court and registration number (e.g., "Paderborn HRA 3059") |

**Example Request**:
```bash
curl -k -X POST "https://localhost:8443/api/v1/germany-search/current-hardcopy-printout" \
  -H "Content-Type: application/json" \
  -d '{"searchTerm":"Claes und Rohde","registrationId":"Paderborn HRA 3059"}' \
  --output hardcopy.pdf
```

**Request Body**:
```json
{
  "searchTerm": "Claes und Rohde",
  "registrationId": "Paderborn HRA 3059"
}
```

### Response 200 OK

**Content-Type**: `application/pdf`
**Body**: Binary PDF stream (English-translated document)

**Response Headers**:

| Header | Type | Description |
|--------|------|-------------|
| `X-Document-Url` | string | Blob storage URL of the translated English PDF |
| `X-Original-Document-Url` | string | Blob storage URL of the original German PDF |
| `X-Correlation-Id` | string | Correlation ID for audit/diagnostics traceability |
| `Content-Disposition` | string | `attachment; filename="de-hardcopy-paderborn-hra-3059.pdf"` |

### Response 400 Bad Request

Missing or empty required fields.

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more required fields are missing.",
  "errors": {
    "searchTerm": ["The searchTerm field is required."],
    "registrationId": ["The registrationId field is required."]
  }
}
```

### Response 404 Not Found

Registration ID not found in search results, or AD link is unavailable.

**Variant 1 — No matching row:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Registration Not Found",
  "status": 404,
  "detail": "No result matching registration ID 'Paderborn HRA 3059' found in Handelsregister results for search term 'Invalid Company'."
}
```

**Variant 2 — AD link unavailable:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Hardcopy Not Available",
  "status": 404,
  "detail": "The current hardcopy printout (AD) is not available for registration 'Paderborn HRA 3059'."
}
```

### Response 502 Bad Gateway

FAB agent returned an error or invalid response.

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Translation Service Error",
  "status": 502,
  "detail": "The document translation service is temporarily unavailable."
}
```

### Response 504 Gateway Timeout

FAB agent timed out.

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Translation Service Timeout",
  "status": 504,
  "detail": "The document translation service did not respond within the configured timeout."
}
```

### Response 503 Service Unavailable

Concurrent hardcopy request already in progress (semaphore full).

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Service Busy",
  "status": 503,
  "detail": "A Germany hardcopy printout request is already being processed. Please retry later.",
  "retryAfter": 30
}
```

**Response Header**: `Retry-After: 30`

### Response 413 Content Too Large

German PDF exceeds the configurable maximum document size limit.

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Document Too Large",
  "status": 413,
  "detail": "The German hardcopy printout exceeds the maximum allowed size."
}
```
