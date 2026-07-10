# Contract: Execute Script

**Endpoint**: `POST /api/v2/execute-script`
**Tag**: Execute Script
**Name**: ExecuteScript

## Request

```json
{
  "definition": "us-ia-business-search",
  "parameters": {
    "searchTerm": "acme"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| definition | string | ✓ | Definition slug to execute |
| parameters | object | | Key-value runtime parameters |

### Available Definitions

| Slug | State | Description | Required Parameters |
|------|-------|-------------|---------------------|
| us-co-business-search | CO | Search Colorado SOS | searchTerm |
| us-ia-business-search | IA | Search Iowa SOS | searchTerm |
| us-wy-business-search | WY | Search Wyoming SOS | searchTerm |
| de-de-business-search | DE | Search German Handelsregister | searchTerm |
| us-co-entity-details | CO | Colorado entity details | uniqueKey |
| us-ia-entity-details | IA | Iowa entity details | uniqueKey |
| us-wy-entity-details | WY | Wyoming entity details | uniqueKey |
| de-de-entity-details | DE | Germany entity details | uniqueKey |

## Response

**200 OK**

```json
{
  "definition": "us-ia-business-search",
  "correlationId": "abc-123",
  "truncated": false,
  "resultCount": 5,
  "data": { }
}
```

**Response Headers**: `X-Correlation-Id`

The `data` field shape varies by definition:
- Business search definitions → `NormalizedSearchResponse`
- Entity detail definitions → `EntityDetailResponse`

The `resultCount` field is present only for business-search definitions (value is the length of the `UnifiedSearchResult[]` array). For non-search definitions (e.g. entity-details), the field is omitted from the JSON response via `JsonIgnoreCondition.WhenWritingNull`.

## Error Responses

| Status | Error Type | Cause |
|--------|-----------|-------|
| 400 | unknown-definition | Definition slug not found |
| 400 | missing-parameters | Required parameters not provided |
| 502 | scraping-error | Scraping operation failed |
| 502 | captcha-resolution-failed | CAPTCHA could not be solved |
| 502 | upstream-error | Upstream site error |
| 503 | service-busy | Browser pool exhausted (includes Retry-After) |
| 504 | execution-timeout | Operation timed out |
