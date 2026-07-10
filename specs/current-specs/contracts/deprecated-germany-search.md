# [DEPRECATED] Contract: Germany Search & Hardcopy Printout

> **⚠️ DEPRECATED**: Use `POST /api/v1/execute-script` with definition
> `de-business-search` or `de-hardcopy-printout` instead.

## Germany Search

**Endpoint**: `GET /api/v1/germany-search`
**Tag**: Germany Search
**Name**: SearchGermanyBusinessEntities
**Status**: Deprecated — will be removed in a future version.

### Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| name | query | string | ✓ | Company name to search |

### Response

**200 OK** — `GermanySearchResponse`

Same response shape as the execute-script equivalent.

### Migration

Replace:
```
GET /api/v1/germany-search?name=Deutsche+Bank
```

With:
```json
POST /api/v1/execute-script
{
  "definition": "de-business-search",
  "parameters": { "searchTerm": "Deutsche Bank" }
}
```

---

## Germany Hardcopy Printout

**Endpoint**: `POST /api/v1/germany-search/current-hardcopy-printout`
**Tag**: Germany Search
**Name**: GetCurrentHardcopyPrintout
**Status**: Deprecated — will be removed in a future version.

### Request

```json
{
  "searchTerm": "Deutsche Bank",
  "registrationId": "HRB 12345"
}
```

### Response

**200 OK** — PDF file with translation headers.

### Migration

Replace:
```json
POST /api/v1/germany-search/current-hardcopy-printout
{ "searchTerm": "Deutsche Bank", "registrationId": "HRB 12345" }
```

With:
```json
POST /api/v1/execute-script
{
  "definition": "de-hardcopy-printout",
  "parameters": { "searchTerm": "Deutsche Bank", "registrationId": "HRB 12345" }
}
```
