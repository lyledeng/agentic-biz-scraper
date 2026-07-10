# Quickstart: Test-UI And Spec Consistency Alignment

## Goal

Validate canonical execute-script contracts end-to-end in API and test-ui while addressing prior consistency findings.

## Prerequisites

- API project available at `src/BizScraper.Api`
- test-ui project available at `src/test-ui`
- Definitions include Iowa search/detail slugs

## 1) Start API

```powershell
dotnet run --project .\src\BizScraper.Api\
```

## 2) Start test-ui

```powershell
cd .\src\test-ui\
npm install
npm run start
```

## 3) Validate Canonical Search Request

Use either test-ui or raw HTTP request:

```http
POST /api/v2/execute-script
Content-Type: application/json

{
  "definition": "us-ia-business-search",
  "parameters": {
    "searchTerm": "acme"
  }
}
```

Expected:

- Response includes `definition`, `correlationId`, `truncated`, `data`
- Search payload includes normalized Iowa result rows with `uniqueKey`

## 4) Validate Canonical Detail Request

```http
POST /api/v2/execute-script
Content-Type: application/json

{
  "definition": "us-ia-entity-details",
  "parameters": {
    "uniqueKey": "<from-search-result>"
  }
}
```

Expected:

- Extended detail payload returned
- `certificateNote` present for Iowa details

## 5) Validate Error Handling In test-ui

- Submit invalid request (missing required parameter)
- Trigger or simulate busy response (503)
- Confirm test-ui displays:
  - HTTP status
  - error detail
  - retry guidance when `Retry-After` is present

## 6) Snapshot Update Workflow

After implementation completion, update:

- `specs/current-specs/spec.md`
- `specs/current-specs/data-model.md`
- impacted files in `specs/current-specs/contracts/`

## Acceptance Validation Checklist

- Canonical payload keys only (`definition`, `parameters`)
- No legacy `scriptName` usage in test-ui
- No legacy required `name` parameter in canonical docs/contracts
- Cross-state v2 responses remain consumable by shared client model
- Constitution snapshot update requirement is satisfied
