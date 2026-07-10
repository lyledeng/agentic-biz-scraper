# Quickstart: Recorder-Friendly JSON Schema (V2)

**Feature**: 014-recorder-friendly-schema | **Date**: 2026-03-27

## What Changed

The JSON flow definition schema has been upgraded from v1 to v2. All 5 existing definitions have been migrated to v2 format. The engine now supports:

- **Multi-selector targeting** — Actions use `target` with multiple selectors ranked by confidence instead of a single flat `selector`. The engine tries selectors in descending confidence order with a 5-second per-selector timeout.
- **Named scripts** — JavaScript functions are defined once in a `scripts` section and referenced by name via `scriptRef` from actions.
- **Rich metadata** — Definitions include `metadata` (id, name, description, author, tags) for cataloging and diagnostics.
- **Environment config** — `environment` section controls viewport size, locale, global timeout, per-selector timeout, and per-action retry policy.
- **Enriched variables** — Variables support `defaultValue`, `sensitive` (log redaction), and `description`.
- **Compound conditions** — `and`/`or` condition types for complex boolean logic in conditions and terminateWhen clauses.
- **Action IDs** — Each action has a unique `id` for unambiguous error messages and logging.
- **Error messages** — `check-text` actions support `errorMessage` with `${variable}` interpolation.
- **Output schema validation** — Optional `output.schema` (JSON Schema subset) provides advisory validation warnings.

## Creating a V2 Definition

### Minimal V2 Definition

```json
{
  "schemaVersion": "1.0.0",
  "metadata": {
    "id": "my-unique-id",
    "name": "My Flow",
    "state": "XX",
    "endpoint": "my-endpoint"
  },
  "variables": [
    { "name": "searchTerm", "source": "runtime", "required": true }
  ],
  "actions": [
    {
      "id": "action-000",
      "order": 0,
      "type": "navigate",
      "url": "https://example.com",
      "waitUntil": "networkidle"
    }
  ],
  "output": {
    "variableName": "results",
    "type": "string"
  }
}
```

### Using Multi-Selector Targets

Replace flat `selector` with `target` containing multiple selectors:

```json
{
  "id": "action-001",
  "order": 1,
  "type": "click",
  "description": "Click search button",
  "target": {
    "selectors": [
      { "strategy": "css",   "value": "#searchBtn",        "confidence": 0.9 },
      { "strategy": "role",  "value": "button",             "confidence": 0.6 },
      { "strategy": "xpath", "value": "//button[@id='searchBtn']", "confidence": 0.5 }
    ],
    "description": "Search submit button"
  }
}
```

The engine resolves selectors in descending confidence order. Each selector probe has a 5-second timeout (configurable via `environment.selectorTimeoutMs`).

### Using Named Scripts

Define scripts once, reference everywhere:

```json
{
  "scripts": {
    "extractResults": {
      "language": "javascript",
      "description": "Extract table rows",
      "body": "() => { /* ... */ return JSON.stringify(rows); }"
    }
  },
  "actions": [
    {
      "id": "action-005",
      "order": 5,
      "type": "extract",
      "scriptRef": "extractResults",
      "storeAs": "results"
    }
  ]
}
```

### Using Compound Conditions

```json
{
  "type": "and",
  "conditions": [
    { "type": "element-exists", "selector": "#resultsTable" },
    { "type": "not", "condition": { "type": "text-contains", "text": "No results found" } }
  ]
}
```

```json
{
  "type": "or",
  "conditions": [
    { "type": "element-exists", "selector": ".next-page" },
    { "type": "element-exists", "selector": "a[rel='next']" }
  ]
}
```

### Using Error Messages with Variable Interpolation

```json
{
  "id": "action-004",
  "type": "check-text",
  "pattern": "Exceeded Record Count",
  "onMatch": "throw",
  "errorType": "exceeded-record-count",
  "errorMessage": "Search term '${searchTerm}' returned too many results."
}
```

### Using Environment Configuration

```json
{
  "environment": {
    "viewport": { "width": 1280, "height": 720 },
    "locale": "en-US",
    "timeout": 30000,
    "selectorTimeoutMs": 5000,
    "retries": {
      "maxAttempts": 3,
      "delayMs": 1000,
      "backoffMultiplier": 2
    }
  }
}
```

### Using Sensitive Variables

```json
{
  "variables": [
    {
      "name": "apiToken",
      "source": "runtime",
      "required": true,
      "sensitive": true,
      "description": "Authentication token (redacted in logs)"
    }
  ]
}
```

### Output Schema Validation

```json
{
  "output": {
    "variableName": "results",
    "type": "BusinessEntityResult[]",
    "schema": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["name", "status"],
        "properties": {
          "name":   { "type": "string" },
          "status": { "type": "string" }
        }
      }
    }
  }
}
```

When the output doesn't match the schema, a warning is logged but execution continues normally.

## Migration Notes

- All 5 existing v1 definitions have been mechanically migrated to v2 format.
- V1 runtime support has been removed after migration verification.
- Flat `selector` fields were converted to single-entry `target` objects with `strategy: "css"` and `confidence: 1.0`.
- Inline `javascript` was preserved as-is (not moved to `scripts` section in mechanical migration).
- Action `id` and `order` fields were auto-generated sequentially.

## Running Tests

```bash
# Run all unit tests (includes v2 model, target resolver, script resolver, etc.)
dotnet test tests/BizScraper.UnitTests

# Run integration tests (validates v2 definitions load correctly)
dotnet test tests/BizScraper.IntegrationTests
```
