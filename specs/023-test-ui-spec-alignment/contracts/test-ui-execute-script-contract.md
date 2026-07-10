# test-ui Execute-Script Contract

## Request Contract

test-ui must send requests to `/api/v2/execute-script` using only canonical fields:

```json
{
  "definition": "us-ia-business-search",
  "parameters": {
    "searchTerm": "acme"
  }
}
```

Detail drill-down request:

```json
{
  "definition": "us-ia-entity-details",
  "parameters": {
    "uniqueKey": "..."
  }
}
```

## Response Contract Expectations

test-ui must parse canonical envelope fields:

- `definition`
- `correlationId`
- `truncated`
- `data`

For Iowa details, UI must render `certificateNote` when present.

For validation or capacity errors, UI must show:

- status code
- returned error title/detail
- retry guidance when `Retry-After` header is present

## Compatibility Rules

- test-ui should not send `scriptName`.
- test-ui should not rely on legacy `name` search key.
- test-ui model interfaces should centralize request/response types in `shared/models`.
