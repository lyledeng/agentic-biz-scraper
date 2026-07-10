# Quickstart: Show No Records Found in Both UI and API

**Feature Branch**: `027-no-records-found`
**Date**: 2026-04-03

---

## Prerequisites

- .NET 10 SDK
- Node.js 20+ with npm
- API running locally (`dotnet run` from `src/BizScraper.Api`)
- Test UI running locally (`npm start` from `src/test-ui`)

---

## Verifying the API Change

### 1. Search with results (expect `resultCount` field)

```bash
curl -s -X POST http://localhost:5000/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-co-business-search", "parameters": {"searchTerm": "Acme"}}' \
  | jq '.resultCount, .truncated, (.data | length)'
```

Expected: `resultCount` matches the array length in `data`.

### 2. Search with zero results (expect `resultCount: 0`)

```bash
curl -s -X POST http://localhost:5000/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-co-business-search", "parameters": {"searchTerm": "ZZZZNONEXISTENT99999"}}' \
  | jq '.resultCount, .truncated, .data'
```

Expected: `resultCount: 0`, `data: []`.

### 3. Entity details (expect `resultCount` absent)

```bash
curl -s -X POST http://localhost:5000/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{"definition": "us-co-entity-details", "parameters": {"uniqueKey": "<valid-key>"}}' \
  | jq 'keys'
```

Expected: Response keys are `["correlationId", "data", "definition", "truncated"]` — no `resultCount`.

---

## Verifying the UI Change

### 1. Initial state (no search performed)

1. Navigate to the search page.
2. Verify the prompt: "Enter a business name above and click Search to find registered entities."
3. This is the dashed-border empty state.

### 2. No records found

1. Enter `ZZZZNONEXISTENT99999` in the search field.
2. Select any state (e.g., Colorado).
3. Click **Search**.
4. Verify a blue/neutral informational notification appears: `No records found for "ZZZZNONEXISTENT99999" in Colorado (US)`.
5. Verify the initial prompt is NOT shown.

### 3. Results found (transition from no-records)

1. After seeing "No records found," enter `Acme` and click **Search**.
2. Verify the results table appears and the notification disappears.

### 4. Truncated with zero mapped results

This edge case is hard to trigger manually. It requires a search where the upstream site returns data (`truncated: true`) but the post-processing mapper produces an empty array. If triggered, verify a warning appears: "Search may have incomplete results. Please try again or refine your search."

---

## Running Tests

### API unit tests

```bash
cd tests/BizScraper.UnitTests
dotnet test --filter "FullyQualifiedName~ExecuteScriptHandlerTests"
```

### Angular component tests

```bash
cd src/test-ui
npx ng test --include="**/search.component.spec.ts" --watch=false
```
