# QuickStart: Washington Business Entity Search API

**Feature**: 032-wa-entity-search  
**API Version**: v2 (execute-script)  
**Date**: 2026-04-05  

---

## Overview

Washington business entity search is integrated into the unified v2 `/api/v2/execute-script` endpoint. This endpoint accepts JSON-driven script definitions and returns structured entity data.

**Key Features**:
- Search by business name on WA CCFS (AngularJS SPA)
- Paginated results (automatic pagination, 25 per page)
- Drill-down to entity details (general info, registered agent, governors, filing history)
- Filing document download to Azure Blob Storage with proxy URLs
- Nonprofit-specific fields (charitable, EIN, revenue, members, public benefit, host home)
- Cloudflare Turnstile detection with error reporting
- Full diagnostic trace logging with screenshots

---

## Base Endpoint

```
POST /api/v2/execute-script
Content-Type: application/json
Authorization: Bearer {token}
```

---

## Example 1: Search for Washington Entities

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Content-Type: application/json
Authorization: Bearer {token}

{
  "definition": "us-wa-business-search",
  "parameters": {
    "searchTerm": "Starbucks"
  }
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | string | Yes | Must be exactly `"us-wa-business-search"` |
| `parameters.searchTerm` | string | Yes | Business name to search (1-200 characters) |

### Response

```json
{
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "definition": "us-wa-business-search",
  "state": "WA",
  "executionTimeMs": 4500,
  "data": [
    {
      "name": "STARBUCKS CORPORATION",
      "identifier": "602042885",
      "entityType": "WA PROFIT CORPORATION",
      "status": "ACTIVE",
      "principalOfficeAddress": "2401 UTAH AVE S, SEATTLE, WA 98134",
      "registeredAgentName": "C T CORPORATION SYSTEM",
      "uniqueKey": "eyJzdGF0ZSI6IlVTLVdBIiwicGFyYW1zIjp7ImJ1c2luZXNzSWQiOiI2MDIwNDI4ODUiLCJidXNpbmVzc1R5cGUiOiJXQSBQUk9GSVQgQ09SUE9SQVRJT04ifX0",
      "state": "WA",
      "formationDate": null
    }
  ],
  "totalCount": 15,
  "siteTotalCount": 15,
  "truncated": false,
  "pagesScraped": 1,
  "searchTerm": "Starbucks",
  "diagnosticsUrl": "https://storage.azure.com/diagnostics/a1b2c3d4/"
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `correlationId` | string | Unique trace ID for this request |
| `definition` | string | Definition that was executed |
| `data[].name` | string | Business entity name |
| `data[].identifier` | string | UBI number |
| `data[].entityType` | string | Business type (e.g., "WA PROFIT CORPORATION") |
| `data[].status` | string | Entity status (e.g., "ACTIVE") |
| `data[].principalOfficeAddress` | string? | Principal office address |
| `data[].registeredAgentName` | string? | Registered agent name |
| `data[].uniqueKey` | string | Encoded key for entity detail drill-down |
| `totalCount` | int | Total results extracted |
| `truncated` | bool | Whether more pages exist beyond limit |

---

## Example 2: Get Washington Entity Details

### Request

```http
POST /api/v2/execute-script HTTP/1.1
Content-Type: application/json
Authorization: Bearer {token}

{
  "definition": "us-wa-entity-details",
  "parameters": {
    "uniqueKey": "eyJzdGF0ZSI6IlVTLVdBIiwicGFyYW1zIjp7ImJ1c2luZXNzSWQiOiI2MDIwNDI4ODUiLCJidXNpbmVzc1R5cGUiOiJXQSBQUk9GSVQgQ09SUE9SQVRJT04ifX0"
  }
}
```

### Request Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `definition` | string | Yes | Must be exactly `"us-wa-entity-details"` |
| `parameters.uniqueKey` | string | Yes | Base64url-encoded key from search results |

### Response

```json
{
  "correlationId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "definition": "us-wa-entity-details",
  "data": {
    "details": {
      "name": "STARBUCKS CORPORATION",
      "ubiNumber": "602042885",
      "businessType": "WA PROFIT CORPORATION",
      "status": "ACTIVE",
      "principalOfficeStreetAddress": "2401 UTAH AVE S, SEATTLE, WA 98134",
      "principalOfficeMailingAddress": "PO BOX 34067, SEATTLE, WA 98124",
      "expirationDate": null,
      "jurisdiction": "WA",
      "formationDate": "11/04/1987",
      "periodOfDuration": "PERPETUAL",
      "inactiveDate": null,
      "natureOfBusiness": "RETAILING, WHOLESALING, AND DISTRIBUTING COFFEE",
      "charitableCorporation": null,
      "nonprofitEin": null,
      "grossRevenueUnder500K": null,
      "hasMembers": null,
      "publicBenefitDesignation": null,
      "hostHome": null
    },
    "registeredAgent": {
      "name": "C T CORPORATION SYSTEM",
      "streetAddress": "711 CAPITOL WAY S STE 204, OLYMPIA, WA 98501",
      "mailingAddress": null
    },
    "governors": [
      {
        "title": "GOVERNOR",
        "governorType": "INDIVIDUAL",
        "entityName": null,
        "firstName": "MELLODY",
        "lastName": "HOBSON"
      },
      {
        "title": "GOVERNOR",
        "governorType": "INDIVIDUAL",
        "entityName": null,
        "firstName": "LAXMAN",
        "lastName": "NARASIMHAN"
      }
    ],
    "filings": [
      {
        "filingNumber": "604975361",
        "filingDateTime": "05/29/2025 04:49 PM",
        "effectiveDate": "05/29/2025",
        "filingType": "ANNUAL REPORT",
        "documents": [
          {
            "documentType": "ANNUAL REPORT - FULFILLED",
            "createdDate": "05/29/2025",
            "storageUrl": "/api/v1/documents/b2c3d4e5/wa/602042885/annual-report-604975361.pdf",
            "fileName": "annual-report-604975361.pdf",
            "error": null
          }
        ]
      },
      {
        "filingNumber": "604123456",
        "filingDateTime": "01/15/2025 10:30 AM",
        "effectiveDate": "01/15/2025",
        "filingType": "STATEMENT OF CHANGE",
        "documents": []
      }
    ]
  }
}
```

### Response Fields — Entity Details

| Field | Type | Description |
|-------|------|-------------|
| `details.name` | string | Business entity name |
| `details.ubiNumber` | string | Unified Business Identifier |
| `details.businessType` | string | Entity type |
| `details.status` | string | Current status |
| `details.principalOfficeStreetAddress` | string? | Street address |
| `details.principalOfficeMailingAddress` | string? | Mailing address |
| `details.expirationDate` | string? | When entity expires |
| `details.jurisdiction` | string? | Incorporation jurisdiction |
| `details.formationDate` | string? | Formation/registration date |
| `details.periodOfDuration` | string? | Duration (e.g., "PERPETUAL") |
| `details.inactiveDate` | string? | When entity became inactive |
| `details.natureOfBusiness` | string? | Business description |
| `details.charitableCorporation` | bool? | Nonprofit: charitable designation |
| `details.nonprofitEin` | string? | Nonprofit: EIN |
| `details.grossRevenueUnder500K` | bool? | Nonprofit: revenue threshold |
| `details.hasMembers` | bool? | Nonprofit: has members |
| `details.publicBenefitDesignation` | bool? | Nonprofit: public benefit |
| `details.hostHome` | bool? | Nonprofit: host home |
| `registeredAgent.name` | string | Agent name |
| `registeredAgent.streetAddress` | string? | Agent street address |
| `registeredAgent.mailingAddress` | string? | Agent mailing address |
| `governors[].title` | string | Governor/officer title |
| `governors[].governorType` | string | "INDIVIDUAL" or "ENTITY" |
| `governors[].firstName` | string? | First name (individuals) |
| `governors[].lastName` | string? | Last name (individuals) |
| `governors[].entityName` | string? | Name (entity governors) |
| `filings[].filingNumber` | string | Filing transaction number |
| `filings[].filingType` | string | Filing type description |
| `filings[].documents[].storageUrl` | string? | Proxy URL to downloaded PDF |
| `filings[].documents[].error` | string? | Error if download failed |

---

## Error Responses

### Cloudflare Turnstile Blocked

```json
{
  "type": "https://BizScraper.local/errors/cloudflare-blocked",
  "title": "Cloudflare Blocked",
  "status": 502,
  "detail": "Cloudflare Turnstile challenge detected. Request blocked by anti-bot protection."
}
```

### Entity Not Found

```json
{
  "type": "https://BizScraper.local/errors/entity-not-found",
  "title": "Entity Not Found",
  "status": 404,
  "detail": "No entity found for the provided uniqueKey."
}
```

### No Records Found

```json
{
  "correlationId": "...",
  "definition": "us-wa-business-search",
  "data": [],
  "totalCount": 0,
  "truncated": false,
  "searchTerm": "xyznonexistent123"
}
```

---

## Available via Definitions Endpoint

```http
GET /api/v2/definitions
```

Response includes:
```json
[
  {
    "definitionSlug": "us-wa-business-search",
    "name": "Washington Business Search",
    "description": "Search Washington Secretary of State CCFS business entity database by name",
    "state": "WA",
    "requiredParameters": [
      { "name": "searchTerm", "description": "Business name to search" }
    ]
  },
  {
    "definitionSlug": "us-wa-entity-details",
    "name": "Washington Entity Details",
    "description": "Extract entity details from Washington CCFS including general info, registered agent, governors, and filing history",
    "state": "WA",
    "requiredParameters": [
      { "name": "uniqueKey", "description": "Base64url-encoded key from search results" }
    ]
  }
]
```
