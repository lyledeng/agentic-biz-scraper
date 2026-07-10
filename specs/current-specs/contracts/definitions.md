# Contract: Definitions Listing

**Endpoint**: `GET /api/v2/definitions`
**Tag**: Execute Script
**Name**: ListDefinitions

## Request

No parameters required.

## Response

**200 OK**

```json
[
  {
    "definitionSlug": "us-ia-business-search",
    "name": "Iowa Business Search",
    "description": "Search Iowa Secretary of State business database and extract results",
    "state": "IA",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search" }
    ]
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| definitionSlug | string | Slug identifier |
| name | string | Display name |
| description | string? | Description |
| state | string | State code (CO, IA, MO, WY, DE) |
| requiredParameters | ParameterInfo[] | Required runtime parameters |
