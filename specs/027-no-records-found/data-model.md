# Data Model: Show No Records Found in Both UI and API

**Feature Branch**: `027-no-records-found`
**Date**: 2026-04-03

---

## API Changes

### ExecuteScriptResponse (modified)

**File**: `src/BizScraper.Api/Features/ExecuteScript/Models/ExecuteScriptResponse.cs`

| Field | Type | Required | Serialized As | Description |
|-------|------|----------|---------------|-------------|
| Definition | string | ✓ | `definition` | Definition slug executed |
| CorrelationId | string | ✓ | `correlationId` | Request correlation ID |
| Truncated | bool | ✓ | `truncated` | Whether results were truncated |
| **ResultCount** | **int?** | | **`resultCount`** | **Number of search results returned. Present only for `*-business-search` definitions; omitted from JSON for all other definition types.** |
| Data | object? | | `data` | Execution result (shape varies by definition) |

**Serialization rule**: `ResultCount` uses `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` and `[JsonPropertyName("resultCount")]`. When null, the field is excluded from the serialized JSON output entirely (the key does not appear).

**Population rule**: In `ExecuteScriptHandler`, after `ApplyPostProcessingMapper` returns, if the result is `UnifiedSearchResult[]`, set `ResultCount = array.Length`. Otherwise leave null.

### Example Responses

**Business search with results** (`us-co-business-search`):
```json
{
  "definition": "us-co-business-search",
  "correlationId": "abc-123",
  "truncated": false,
  "resultCount": 3,
  "data": [
    { "name": "Acme Corp", "identifier": "123", "status": "Active", ... }
  ]
}
```

**Business search with zero results**:
```json
{
  "definition": "us-co-business-search",
  "correlationId": "abc-456",
  "truncated": false,
  "resultCount": 0,
  "data": []
}
```

**Entity details (non-search — resultCount omitted)**:
```json
{
  "definition": "us-co-entity-details",
  "correlationId": "abc-789",
  "truncated": false,
  "data": { ... }
}
```

---

## UI Changes

### ExecuteScriptResponse TypeScript interface (modified)

**File**: `src/test-ui/src/app/shared/models/execute-script.model.ts`

| Field | Type | Description |
|-------|------|-------------|
| definition | string | Definition slug executed |
| correlationId | string | Request correlation ID |
| truncated | boolean | Whether results were truncated |
| **resultCount?** | **number** | **Optional result count (present for search definitions only)** |
| data | unknown | Execution result |

### SearchComponent State Machine (modified)

**File**: `src/test-ui/src/app/features/search/search.component.ts`

New signals and tracking fields:

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `hasSearched` | `Signal<boolean>` | `false` | Set to `true` when `onSearch()` fires; never reset |
| `lastSearchTerm` | `string` | `''` | The most recent search term submitted |
| `lastSearchState` | `string` | `'CO'` | The most recent state code submitted |

### UI State Diagram

```
                    ┌──────────────┐
                    │   Initial    │  hasSearched=false
                    │  (prompt)    │  "Enter a business name..."
                    └──────┬───────┘
                           │ onSearch()
                           ▼
                    ┌──────────────┐
                    │  Loading...  │  loading=true
                    └──────┬───────┘
                           │ response received
              ┌────────────┼────────────┬───────────────┐
              ▼            ▼            ▼               ▼
     ┌────────────┐ ┌───────────┐ ┌──────────┐ ┌──────────────┐
     │  Results   │ │ No Records│ │ Truncated│ │    Error/    │
     │  (table)   │ │  Found    │ │  Warning │ │   Warning    │
     │ len > 0    │ │ len=0     │ │ truncated│ │              │
     └────────────┘ │ !warning  │ │ + len=0  │ └──────────────┘
                    └───────────┘ └──────────┘
```

### Template Conditional Blocks (search.component.html)

1. **Initial prompt** — `@if (!loading() && !hasSearched() && !error() && !warning())`
   - Dashed border box: "Enter a business name above and click Search to find registered entities."

2. **No records found** — `@if (!loading() && hasSearched() && results().length === 0 && !error() && !warning())`
   - `<notification-pds3 type="information">` with message: `No records found for "{lastSearchTerm}" in {STATE_DISPLAY_LABEL[lastSearchState]}`

3. **Results table** — existing `@if (!loading() && results().length > 0)` (unchanged)

4. **Error/Warning** — existing `<app-error-banner>` components (unchanged)

---

## No Database Changes

This feature does not modify any database tables, columns, or Liquibase changelogs. The `resultCount` is derived at runtime from the in-memory search result array.
