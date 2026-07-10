# API Contract: Entity Details Endpoint

**Feature**: 007-wy-entity-details  
**Date**: 2026-03-25

## GET /api/v1/entity-details

### Request

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| detailsUrl | query | string | ✅ | Full HTTPS URL to the entity details page |
| state | query | string | ✅ | State code: `CO` or `WY` |

**Example Requests**:
```
GET /api/v1/entity-details?detailsUrl=https://www.sos.state.co.us/biz/BusinessEntityDetail.do?masterFileId=20181871974&entityId2=20181871974&state=CO

GET /api/v1/entity-details?detailsUrl=https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=148028109002063097078042099251229204182040235172&state=WY
```

### Response 200 OK

```json
{
  "details": {
    "name": "Wendy's Shabby Chic LLC",
    "status": "Inactive - Administratively Dissolved (Tax)",
    "formationDate": "08/07/2018",
    "idNumber": "2018-000815365",
    "form": "Limited Liability Company - Domestic",
    "periodicReportMonth": null,
    "jurisdiction": "Wyoming",
    "principalOfficeStreetAddress": "500 Church Ave Lot 17, Gillette, WY 82716, USA",
    "principalOfficeMailingAddress": "500 Church Ave Lot 17, Gillette, WY 82716, USA",
    "subStatus": "Archived",
    "standingTax": "Delinquent",
    "standingRA": "Good",
    "standingOther": "Good",
    "inactiveDate": "10/09/2019",
    "termOfDuration": "Perpetual",
    "formedIn": "Wyoming",
    "latestAnnualReportYear": null,
    "annualReportExempt": "AR Exempt",
    "licenseTaxPaid": null
  },
  "registeredAgent": {
    "name": "Wendy Beck",
    "streetAddress": "500 Church Ave Lot 17, Gillette, WY 82716",
    "mailingAddress": null
  },
  "certificate": null,
  "parties": [
    {
      "name": "Wendy Beck",
      "role": "Organizer",
      "organization": null,
      "address": "500 Church Ave Lot 17 Gillette, WY 82716"
    }
  ],
  "historyDocuments": [
    {
      "eventTitle": "Initial Filing - See Filing ID",
      "date": "08/07/2018",
      "storageUrl": "https://storageaccount.blob.core.windows.net/playwright-diagnostics/...",
      "fileName": "2018-000815365-initial-filing.pdf",
      "error": null
    }
  ]
}
```

### Response 200 OK (CO — backward compatible shape)

```json
{
  "details": {
    "name": "SOME COLORADO ENTITY",
    "status": "Good Standing",
    "formationDate": "01/15/2020",
    "idNumber": "20201234567",
    "form": "Limited Liability Company",
    "periodicReportMonth": "March",
    "jurisdiction": "Colorado",
    "principalOfficeStreetAddress": "123 Main St, Denver, CO 80202",
    "principalOfficeMailingAddress": "PO Box 999, Denver, CO 80202",
    "subStatus": null,
    "standingTax": null,
    "standingRA": null,
    "standingOther": null,
    "inactiveDate": null,
    "termOfDuration": null,
    "formedIn": null,
    "latestAnnualReportYear": null,
    "annualReportExempt": null,
    "licenseTaxPaid": null
  },
  "registeredAgent": {
    "name": "John Doe",
    "streetAddress": "456 Agent Blvd, Denver, CO 80202",
    "mailingAddress": null
  },
  "certificate": {
    "available": true,
    "storageUrl": "https://storageaccount.blob.core.windows.net/.../20201234567-certificateofgoodstanding.pdf",
    "fileName": "20201234567-certificateofgoodstanding.pdf",
    "error": null
  },
  "parties": null,
  "historyDocuments": null
}
```

### Response 400 Bad Request — Missing State

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Bad Request",
  "status": 400,
  "detail": "The 'state' query parameter is required. Supported values: CO, WY."
}
```

### Response 400 Bad Request — Invalid State

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Bad Request",
  "status": 400,
  "detail": "Unsupported state code 'TX'. Supported values: CO, WY."
}
```

### Response 400 Bad Request — URL Validation Failed

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Bad Request",
  "status": 400,
  "detail": "Invalid details URL: host must be 'wyobiz.wyo.gov' for state 'WY'."
}
```

### Response 502 Bad Gateway — Scraping or CAPTCHA Failure

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Bad Gateway",
  "status": 502,
  "detail": "Flow 'Wyoming Entity Details' failed: CAPTCHA resolution failed after 3 attempts."
}
```

### Response 503 Service Unavailable — Concurrency Limit

```json
{
  "type": "https://tools.ietf.org/html/rfc9457",
  "title": "Service Unavailable",
  "status": 503,
  "detail": "Too many concurrent requests.",
  "headers": {
    "Retry-After": "5"
  }
}
```

## URL Validation Rules

### State: CO

| Rule | Value |
|------|-------|
| Scheme | `https` |
| Host | `www.sos.state.co.us` |
| Path prefix | `/biz/BusinessEntityDetail.do` |
| Fragment | Not allowed |
| User info | Not allowed |

### State: WY

| Rule | Value |
|------|-------|
| Scheme | `https` |
| Host | `wyobiz.wyo.gov` |
| Path prefix | `/Business/FilingDetails.aspx` |
| Fragment | Not allowed |
| User info | Not allowed |
