# QuickStart: Missouri Business Entity Search API

**Feature**: 031-mo-entity-search  
**API Version**: v2 (execute-script)  
**Date**: 2026-04-04  

---

## Overview

Missouri business entity search is integrated into the unified v2 `/api/v2/execute-script` endpoint. This endpoint accepts JSON-driven script definitions and returns structured entity data.

**Key Features**:
- 🔍 Search by business name ("Starting With" match on MO SOS)
- 📄 Paginated results (automatic pagination through ASP.NET postback pages)
- 🔗 Drill-down to entity details (General Info, Principal Address, Filings)
- 📎 Filing PDF download to Azure Blob Storage with proxy URLs
- 🧩 Previous name collapsing into parent entity `previousNames` array
- ⚠️ Truncation detection (1,051 result cap)
- 🧪 Full diagnostic trace logging with screenshots

---

## Base Endpoint

```
POST /api/v2/execute-script
Content-Type: application/json
```

**Host**: Depends on deployment (local: `http://localhost:5000`, Azure AKS: `https://api.service.azure.com`)

---

## Example 1: Search for Missouri Entities

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "definition": "us-mo-business-search",
  "parameters": {
    "searchTerm": "Smith"
  }
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | string | Yes | Must be exactly `"us-mo-business-search"` |
| `parameters.searchTerm` | string | Yes | Business name to search (1-200 characters) |

### Response

```json
{
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "definition": "us-mo-business-search",
  "state": "MO",
  "executionTimeMs": 3200,
  "data": [
    {
      "name": "Smith Enterprises LLC",
      "identifier": "LC0012345",
      "entityType": "Limited Liability Company",
      "status": "Good Standing",
      "formationDate": "2020-01-15",
      "registeredAgentName": "John Smith",
      "previousNames": [],
      "uniqueKey": "eyJzdGF0ZSI6Ik1PIiwiaWQiOiI3ODkwMTIifQ",
      "state": "MO"
    },
    {
      "name": "Smith & Associates Inc.",
      "identifier": "00234567",
      "entityType": "General Business Corporation",
      "status": "Good Standing",
      "formationDate": "2015-06-20",
      "registeredAgentName": "Jane Smith",
      "previousNames": ["Smith Consulting Inc.", "Smith Group Inc."],
      "uniqueKey": "eyJzdGF0ZSI6Ik1PIiwiaWQiOiI0NTY3ODkifQ",
      "state": "MO"
    }
  ],
  "totalCount": 2,
  "siteTotalCount": 2,
  "truncated": false,
  "pagesScraped": 1,
  "searchTerm": "Smith",
  "diagnosticsUrl": "https://storage.azure.com/diagnostics/a1b2c3d4-e5f6-7890-abcd-ef1234567890/"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `correlationId` | string | Trace ID for this operation (for debugging) |
| `definition` | string | Script definition used: `"us-mo-business-search"` |
| `state` | string | State code: `"MO"` |
| `executionTimeMs` | integer | Total execution time in milliseconds |
| `data` | array | Array of `MissouriBusinessSearchResult` objects |
| `data[].name` | string | Business entity name |
| `data[].identifier` | string | Charter number from MO SOS |
| `data[].entityType` | string | Entity type (e.g., "Limited Liability Company") |
| `data[].status` | string | Entity status (e.g., "Good Standing") |
| `data[].formationDate` | string \| null | Formation/creation date (ISO 8601) |
| `data[].registeredAgentName` | string \| null | Name of the registered agent |
| `data[].previousNames` | string[] | Previous names collapsed from search results |
| `data[].uniqueKey` | string | Base64URL-encoded key for drill-down: `{ "state": "MO", "id": "{entityId}" }` |
| `data[].state` | string | Always `"MO"` |
| `totalCount` | integer | Number of results in this response |
| `siteTotalCount` | integer | Total entities on MO SOS matching query |
| `truncated` | boolean | `true` if results exceed the 1,051 display cap |
| `pagesScraped` | integer | Number of pages scraped (20 results per page) |
| `searchTerm` | string | Original search term |
| `diagnosticsUrl` | string | Azure Blob Storage URL to screenshots, logs, and HTML snapshots |

---

## Example 2: Drill Down to Entity Details

Use the `uniqueKey` from search results to retrieve comprehensive entity details including filings with downloadable PDFs.

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Host: localhost:5000
Content-Type: application/json

{
  "definition": "us-mo-entity-details",
  "parameters": {
    "uniqueKey": "eyJzdGF0ZSI6Ik1PIiwiaWQiOiI3ODkwMTIifQ"
  }
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | string | Yes | Must be exactly `"us-mo-entity-details"` |
| `parameters.uniqueKey` | string | Yes | Base64URL-encoded drill-down reference from search result |

### Response

```json
{
  "correlationId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "definition": "us-mo-entity-details",
  "state": "MO",
  "executionTimeMs": 4850,
  "data": {
    "uniqueKey": "eyJzdGF0ZSI6Ik1PIiwiaWQiOiI3ODkwMTIifQ",
    "state": "MO",
    "details": {
      "name": "Smith Enterprises LLC",
      "charterNumber": "LC0012345",
      "entityType": "Limited Liability Company",
      "domesticity": "Domestic",
      "homeState": null,
      "registeredAgentName": "John Smith",
      "registeredAgentAddress": "123 Main St, Jefferson City, MO 65101",
      "status": "Good Standing",
      "dateFormed": "01/15/2020",
      "duration": "Perpetual"
    },
    "filings": [
      {
        "action": "Creation",
        "documentType": "Articles of Organization",
        "dateFiled": "01/15/2020",
        "effectiveDate": "01/15/2020",
        "storageUrl": "https://api.service.azure.com/api/v1/documents/b2c3d4e5.../Articles_of_Organization_01152020.pdf",
        "fileName": "Articles_of_Organization_01152020.pdf",
        "error": null
      },
      {
        "action": "Modify",
        "documentType": "Annual Report",
        "dateFiled": "03/10/2023",
        "effectiveDate": "03/10/2023",
        "storageUrl": "https://api.service.azure.com/api/v1/documents/b2c3d4e5.../Annual_Report_03102023.pdf",
        "fileName": "Annual_Report_03102023.pdf",
        "error": null
      },
      {
        "action": "Modify",
        "documentType": "Amendment",
        "dateFiled": "06/01/2024",
        "effectiveDate": "06/01/2024",
        "storageUrl": null,
        "fileName": null,
        "error": "PDF download failed: 404 Not Found"
      }
    ],
    "addresses": [
      {
        "addressType": "Reg. Office",
        "address": "456 Commerce Blvd, Kansas City, MO 64105",
        "since": "01/15/2020"
      }
    ]
  },
  "diagnosticsUrl": "https://storage.azure.com/diagnostics/b2c3d4e5-f6a7-8901-bcde-f23456789012/"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `correlationId` | string | Trace ID for this operation |
| `definition` | string | Script definition used: `"us-mo-entity-details"` |
| `state` | string | State code: `"MO"` |
| `executionTimeMs` | integer | Total execution time in milliseconds |
| `data.uniqueKey` | string | The decoded unique key for this entity |
| `data.state` | string | Always `"MO"` |
| `data.details.name` | string | Business entity name |
| `data.details.entityType` | string | Entity type |
| `data.details.charterNumber` | string | MO charter number |
| `data.details.domesticity` | string | Domestic or Foreign |
| `data.details.homeState` | string \| null | Home state (foreign entities) |
| `data.details.registeredAgentName` | string \| null | Registered agent name |
| `data.details.registeredAgentAddress` | string \| null | Registered agent full address |
| `data.details.status` | string | Current status |
| `data.details.dateFormed` | string \| null | Formation date (MM/DD/YYYY) |
| `data.details.duration` | string \| null | Duration (e.g., "Perpetual") |
| `data.filings` | array | Filed document list with downloaded PDFs |
| `data.filings[].action` | string | Filing action type (e.g., Creation, Modify) |
| `data.filings[].documentType` | string | Document type description |
| `data.filings[].dateFiled` | string \| null | Date of filing |
| `data.filings[].effectiveDate` | string \| null | Effective date |
| `data.filings[].storageUrl` | string \| null | Proxy URL to downloaded PDF (null if failed) |
| `data.filings[].fileName` | string \| null | Generated PDF filename |
| `data.filings[].error` | string \| null | Error message if PDF download failed |
| `data.addresses` | array | Principal office address records |
| `data.addresses[].addressType` | string | Address type (e.g., "Reg. Office") |
| `data.addresses[].address` | string | Full address |
| `data.addresses[].since` | string \| null | Effective since date |
| `diagnosticsUrl` | string | Azure Blob Storage URL to diagnostic artifacts |

---

## Response Codes

### Success (2xx)

| Status | Condition | Notes |
|--------|-----------|-------|
| 200 | Search completed successfully | May contain 0 results if no entities match |
| 200 | Detail retrieval successful | Entity details fully populated |

### Client Errors (4xx)

| Status | Condition | Notes |
|--------|-----------|-------|
| 400 | Invalid parameters | Missing or malformed `definition`, `parameters`, etc. |
| 400 | Search term empty | Must be at least 1 character |
| 404 | Entity not found | `uniqueKey` points to non-existent entity |

### Server Errors (5xx)

| Status | Condition | Notes |
|--------|-----------|-------|
| 503 | State concurrency limit exceeded | Too many concurrent MO requests; retry with backoff |
| 503 | MO SOS website unavailable | Network error or site down; retry with exponential backoff |
| 504 | Timeout | Operation exceeded timeout threshold |

---

## Testing with Test UI

1. Start the API: `dotnet run --project src/BizScraper.Api`
2. Open Test UI at `http://localhost:5000` (or via `src/test-ui/`)
3. Select **MO** from the state dropdown
4. Enter a business name and click **Search**
5. Click **Details** on any result row to retrieve entity details with filings

---

## Notes

- **Previous Names**: MO SOS displays previous entity names as separate rows beneath the parent entity. The scraper collapses these into a `previousNames` string array on the parent.
- **Filing PDFs**: Each filing's PDF is downloaded to Azure Blob Storage and a proxy URL is returned. If a download fails, the `proxyUrl` is null and an `error` field describes the failure.
- **Truncation**: MO SOS caps results at 1,051. When this cap is hit, `truncated: true` is returned.
- **Browser Mode**: Uses Azure Playwright Workspaces (remote) — no anti-bot measures detected.
- **Certificate of Good Standing**: Not available via this API (MO SOS paywall).
