# Contract: Execute Script (Updated for 027-no-records-found)

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

## Response

**200 OK — Business search (with results)**

```json
{
  "definition": "us-ia-business-search",
  "correlationId": "abc-123",
  "truncated": false,
  "resultCount": 3,
  "data": [
    {
      "name": "Acme Corp",
      "identifier": "12345",
      "status": "Active",
      "entityType": "LLC",
      "formationDate": "01/15/2020",
      "state": "IA",
      "uniqueKey": "eyJ..."
    }
  ]
}
```

**200 OK — Business search (zero results)**

```json
{
  "definition": "us-ia-business-search",
  "correlationId": "abc-456",
  "truncated": false,
  "resultCount": 0,
  "data": []
}
```

**200 OK — Entity details (resultCount omitted)**

```json
{
  "definition": "us-ia-entity-details",
  "correlationId": "abc-789",
  "truncated": false,
  "data": { }
}
```

**Response Headers**: `X-Correlation-Id`

### Response Fields

| Field | Type | Condition | Description |
|-------|------|-----------|-------------|
| definition | string | Always | Definition slug executed |
| correlationId | string | Always | Request correlation ID |
| truncated | bool | Always | Whether results were truncated |
| resultCount | int | Business search only | Number of results returned. Omitted entirely for non-search definitions. |
| data | object | Always | Execution result — `UnifiedSearchResult[]` for search, `UnifiedEntityDetailResponse` for details |

### Serialization Rules

- `resultCount` is present in JSON **only** for `*-business-search` definitions.
- For `*-entity-details` and all other definitions, the `resultCount` key does not appear in the response body.
- This is achieved via `JsonIgnoreCondition.WhenWritingNull` on the nullable `int?` property.

## Error Responses

| Status | Error Type | Cause |
|--------|-----------|-------|
| 400 | unknown-definition | Definition slug not found |
| 400 | missing-parameters | Required parameters not provided |
| 422 | exceeded-record-count | Search term too broad |
| 502 | scraping-error | Upstream scraping failure |
| 503 | service-busy | Concurrency limit reached (includes Retry-After header) |
| 504 | execution-timeout | Script execution timed out |
