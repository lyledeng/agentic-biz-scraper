# [REMOVED] Contract: Entity Details

> **❌ REMOVED in feature 018-unified-entity-schema**: Use `POST /api/v2/execute-script`
> with definition `us-co-entity-details`, `us-wy-entity-details`, or
> `de-de-entity-details` instead.

**Endpoint**: `GET /api/v1/entity-details`
**Tag**: Entity Details
**Name**: GetEntityDetails
**Status**: Removed — endpoint deleted. Use `/api/v2/execute-script`.

## Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| detailsUrl | query | string | ✓ | Entity details page URL |
| state | query | string | ✓ | State code (CO or WY) |

## Response

**200 OK** — `EntityDetailResponse`

Same response shape as the execute-script equivalent.

## Migration

Replace:
```
GET /api/v1/entity-details?detailsUrl=https://...&state=CO
```

With:
```json
POST /api/v2/execute-script
{
  "definition": "us-co-entity-details",
  "parameters": { "uniqueKey": "<base64url-encoded key from search>" }
}
```

The `data` field in the execute-script response contains the
`EntityDetailResponse`.
