# API Contract: Business Search (Multi-State)

**Feature**: `005-multi-state-search`  
**Date**: 2026-03-25  
**Version**: `2.0.2603.0` (MAJOR bump — breaking change: new `state` parameter required, normalized response schema)

## Endpoint

```
GET /api/v1/business-search?state={state}&name={name}
```

## Parameters

| Name | Location | Type | Required | Constraints | Description |
|------|----------|------|----------|-------------|-------------|
| `state` | query | `string` | Yes | `CO` or `WY` (case-insensitive) | Two-letter state code |
| `name` | query | `string` | Yes | Non-empty, non-whitespace | Business name search term |

## Success Response

**Status**: `200 OK`  
**Content-Type**: `application/json`

### Wyoming Example (`?state=WY&name=Wendy`)

```json
{
  "results": [
    {
      "name": "WEND&YLZ LLC",
      "identifier": "2018-000797412",
      "status": "Active",
      "entityType": "LLC",
      "formationDate": "04/06/2018",
      "detailsUrl": "https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=133111247...",
      "state": "WY",
      "event": null,
      "standingTax": "Good",
      "standingRA": "Good"
    }
  ],
  "totalCount": 37,
  "siteTotalCount": 37,
  "truncated": false,
  "pagesScraped": 2,
  "searchTerm": "Wendy",
  "state": "WY"
}
```

### Colorado Example (`?state=CO&name=Wendy`)

```json
{
  "results": [
    {
      "name": "WENDY'S INTERNATIONAL INC",
      "identifier": "19871296702",
      "status": "Good Standing",
      "entityType": "Corporation",
      "formationDate": "01/08/1976",
      "detailsUrl": "https://www.sos.state.co.us/biz/BusinessEntityDetail.do?...",
      "state": "CO",
      "event": "Report Filed",
      "standingTax": null,
      "standingRA": null
    }
  ],
  "totalCount": 15,
  "siteTotalCount": 15,
  "truncated": false,
  "pagesScraped": 1,
  "searchTerm": "Wendy",
  "state": "CO"
}
```

## Error Responses

### 400 Bad Request — Missing or Invalid State

```json
{
  "type": "/errors/validation-error",
  "title": "Bad Request",
  "status": 400,
  "detail": "The 'state' parameter is required and must be one of: CO, WY.",
  "instance": "/api/v1/business-search"
}
```

### 400 Bad Request — Empty Search Term

```json
{
  "type": "/errors/validation-error",
  "title": "Bad Request",
  "status": 400,
  "detail": "Search term must not be empty or whitespace.",
  "instance": "/api/v1/business-search"
}
```

### 422 Unprocessable Entity — Exceeded Record Count (CO only)

```json
{
  "type": "/errors/exceeded-record-count",
  "title": "Unprocessable Entity",
  "status": 422,
  "detail": "The search term is too broad. The CO SOS site reported: Exceeded Record Count.",
  "instance": "/api/v1/business-search"
}
```

### 502 Bad Gateway — Scraping or Upstream Failure

```json
{
  "type": "/errors/scraping-error",
  "title": "Bad Gateway",
  "status": 502,
  "detail": "Failed while scraping search results.",
  "instance": "/api/v1/business-search"
}
```

### 502 Bad Gateway — CAPTCHA Resolution Failed (WY only)

```json
{
  "type": "/errors/captcha-resolution-failed",
  "title": "Bad Gateway",
  "status": 502,
  "detail": "Failed to solve the CAPTCHA challenge on the Wyoming Secretary of State website.",
  "instance": "/api/v1/business-search"
}
```

### 503 Service Unavailable — Busy

```json
{
  "type": "/errors/service-busy",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "A search for this state is currently in progress. Please retry after the indicated interval.",
  "instance": "/api/v1/business-search"
}
```

**Headers**: `Retry-After: 30`

## Breaking Changes from Previous Version

| Change | Before (`1.x`) | After (`2.0`) |
|--------|----------------|---------------|
| `state` parameter | Not present | Required |
| Response type | `SearchResponse` | `NormalizedSearchResponse` |
| Result fields | `documentNumber`, `form`, `event` (all required) | `identifier`, `entityType`, `event?`, `standingTax?`, `standingRA?`, `state` |
| Response envelope | No `state` field | `state` field present |

## OpenAPI Tags

- Tag: `Business Search`
- Operation ID: `SearchBusinessEntities`
