# Data Model: Generic Script Execution Endpoint

**Feature**: 016-generic-script-execution  
**Date**: 2026-03-28

## Extended Entity: FlowMetadata

The existing `FlowMetadata` record gains one new field.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `DefinitionSlug` | `string` | Yes | Consumer-facing identifier used in `POST /api/v1/execute-script`. Must be unique across all loaded definitions. Convention: `{state}-{endpoint}` (e.g., `"co-business-search"`). |

All other `FlowMetadata` fields (`Id`, `Name`, `Description`, `State`, `Endpoint`, `CreatedAt`, `UpdatedAt`, `Author`, `Tags`) remain unchanged.

### JSON Representation

```json
"metadata": {
  "definitionSlug": "co-business-search",
  "id": "90ef5ca9-6e88-4e47-8761-fea52bd1134b",
  "name": "Colorado Business Search",
  "state": "CO",
  "endpoint": "business-search"
}
```

### Slug Values per Definition

| Definition File | `definitionSlug` |
|-----------------|------------------|
| `co-business-search.json` | `co-business-search` |
| `co-entity-details.json` | `co-entity-details` |
| `de-business-search.json` | `de-business-search` |
| `wy-business-search.json` | `wy-business-search` |
| `wy-entity-details.json` | `wy-entity-details` |

---

## New Entity: ExecuteScriptRequest

The request body for `POST /api/v1/execute-script`.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Definition` | `string` | Yes | The `definitionSlug` of the flow to execute. |
| `Parameters` | `Dictionary<string, object?>` | Yes | Key-value pairs for runtime variables required by the definition. |

### JSON Representation

```json
{
  "definition": "co-business-search",
  "parameters": {
    "searchTerm": "Wendys"
  }
}
```

---

## New Entity: ExecuteScriptResponse

The uniform response envelope returned by `POST /api/v1/execute-script`.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Definition` | `string` | Yes | The `definitionSlug` that was executed. |
| `CorrelationId` | `string` | Yes | Unique correlation identifier for this execution. |
| `Truncated` | `bool` | Yes | Whether results were truncated (from flow engine). |
| `Data` | `object?` | Yes | The raw output from the scraping flow (shape depends on definition). |

### JSON Representation

```json
{
  "definition": "co-business-search",
  "correlationId": "abc123def456",
  "truncated": false,
  "data": [
    {
      "documentNumber": "20181871974",
      "name": "WENDYS INTERNATIONAL LLC",
      "event": "Report",
      "status": "Good Standing",
      "form": "Foreign Limited Liability Company",
      "formationDate": "10/18/2018",
      "detailsUrl": "https://www.sos.state.co.us/biz/..."
    }
  ]
}
```

---

## New Entity: DefinitionInfo

Returned by the `GET /api/v1/definitions` discovery endpoint.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `DefinitionSlug` | `string` | Yes | Consumer-facing identifier. |
| `Name` | `string` | Yes | Human-readable name from `metadata.name`. |
| `Description` | `string?` | No | From `metadata.description`. |
| `State` | `string` | Yes | State/jurisdiction code. |
| `RequiredParameters` | `ParameterInfo[]` | Yes | List of required runtime parameters. |

### ParameterInfo

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Name` | `string` | Yes | Parameter name (variable name from definition). |
| `Description` | `string?` | No | From the variable's `description` field. |

### JSON Representation

```json
[
  {
    "definitionSlug": "co-business-search",
    "name": "Colorado Business Search",
    "description": "Search Colorado Secretary of State business database",
    "state": "CO",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search for" }
    ]
  }
]
```

---

## New Database Table: ScriptExecutionAuditEntries

Liquibase changeset `2.0.2603.6-create-script-execution-audit-table.sql`.

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `UNIQUEIDENTIFIER` | NOT NULL (PK) | Primary key, defaults to `NEWID()`. |
| `DefinitionSlug` | `NVARCHAR(100)` | NOT NULL | Which definition was executed. |
| `CorrelationId` | `NVARCHAR(100)` | NOT NULL | Request correlation ID. |
| `InputParameters` | `NVARCHAR(MAX)` | NULL | JSON representation of the `parameters` object. |
| `DurationMs` | `BIGINT` | NOT NULL | Execution duration in milliseconds. |
| `Status` | `NVARCHAR(50)` | NOT NULL | Outcome: Success, Error, Busy, ValidationError, etc. |
| `ErrorDetail` | `NVARCHAR(2000)` | NULL | Error message if failed. |
| `DiagnosticsUrl` | `NVARCHAR(2000)` | NULL | Cloud storage URL for diagnostics. |
| `CreatedAt` | `DATETIMEOFFSET` | NOT NULL | Timestamp, defaults to `SYSDATETIMEOFFSET()`. |

### Rollback

```sql
DROP TABLE dbo.ScriptExecutionAuditEntries;
```

---

## Relationship Diagram

```
ExecuteScriptRequest
  ├── definition ──► FlowMetadata.definitionSlug (lookup)
  └── parameters ──► VariableV2[source=runtime] (validation + population)

FlowDefinitionV2
  ├── metadata.definitionSlug (NEW, unique index in FlowDefinitionLoader)
  └── metadata.state + metadata.endpoint (existing index, unchanged)

ExecuteScriptResponse
  ├── definition ──► echoed from request
  ├── correlationId ──► system-generated
  ├── truncated ──► from FlowExecutionResult
  └── data ──► from FlowExecutionResult.Output

ScriptExecutionAuditEntries (database)
  ├── DefinitionSlug ──► from request
  └── InputParameters ──► JSON of request parameters
```
