# Contract: Business Search API

**Feature**: 010-test-ui (consumed by Angular UI)
**Source**: `GET /api/v1/business-search`
**Version**: Existing endpoint (no changes required for this feature)

## Request

```
GET /api/v1/business-search?name={term}&state={state}
```

| Parameter | Type | Required | Values |
|-----------|------|----------|--------|
| `name` | `string` | ✅ | Non-empty search term |
| `state` | `string` | ✅ | `CO` or `WY` |

## Response

**200 OK** — `application/json`

```json
[
  {
    "name": "Wendy's Technology, LLC",
    "identifier": "2019-000123456",
    "status": "Active",
    "entityType": "Limited Liability Company",
    "formationDate": "2019-03-15",
    "state": "WY",
    "detailsUrl": "https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=..."
  }
]
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `name` | `string` | ✅ | Display in Name column |
| `identifier` | `string` | ✅ | Display in Identifier column |
| `status` | `string` | ✅ | Display in Status column |
| `entityType` | `string` | ✅ | Display in Entity Type column |
| `formationDate` | `string \| null` | ❌ | ISO 8601 or formatted date |
| `state` | `string` | ✅ | `CO` or `WY` |
| `detailsUrl` | `string` | ✅ | Passed to entity-details call on row click |

**Empty results**: Returns `[]` (empty array). UI displays "No results found."

**4xx/5xx errors**: UI displays user-friendly error message. Raw error body is not rendered.

## TypeScript Interface (UI consumer)

```typescript
// src/test-ui/src/app/shared/models/business-search-result.model.ts
export interface BusinessSearchResult {
  name: string;
  identifier: string;
  status: string;
  entityType: string;
  formationDate: string | null;
  state: string;
  detailsUrl: string;
}
```
