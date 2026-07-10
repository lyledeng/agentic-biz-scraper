# [REMOVED] Contract: Business Search

> **❌ REMOVED in feature 020-wy-cert-good-standing-fix**: Use `POST /api/v2/execute-script`
> with definition `us-co-business-search` or `us-wy-business-search` instead.

**Endpoint**: `GET /api/v1/business-search`
**Tag**: Business Search
**Name**: SearchBusinessEntities
**Status**: Removed — endpoint deleted. Use `/api/v2/execute-script`.

## Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| state | query | string | ✓ | State code (CO or WY) |
| name | query | string | ✓ | Business name to search |

## Response

**200 OK** — `NormalizedSearchResponse`

Same response shape as the execute-script equivalent.

## Migration

Replace:
```
GET /api/v1/business-search?state=CO&name=acme
```

With:
```json
POST /api/v2/execute-script
{
  "definition": "us-co-business-search",
  "parameters": { "searchTerm": "acme" }
}
```

The `data` field in the execute-script response contains the
`NormalizedSearchResponse`.
