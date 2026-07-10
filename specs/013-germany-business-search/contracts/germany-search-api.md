# API Contract: Germany Business Search

**Feature**: 013-germany-business-search  
**Date**: 2026-03-27  
**Version**: 1.0

## Endpoint

```
GET /api/v1/germany-search
```

## Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| name | string | Yes | Company name or keywords to search for on the Handelsregister |

## Responses

### 200 OK — Results Found

```json
{
  "results": [
    {
      "companyName": "Rohde & Schwarz Group Services GmbH",
      "registeredOffice": "Leipzig",
      "status": "currently registered"
    }
  ],
  "totalCount": 1,
  "pagesScraped": 1,
  "searchTerm": "Rohde & Schwarz Group Services GmbH",
  "warning": null
}
```

### 200 OK — Results with Exceeded-Hits Warning

```json
{
  "results": [
    {
      "companyName": "\"MOOS-weg\" Schulte, Rohde & Co. GmbH",
      "registeredOffice": "Schwelm",
      "status": "currently registered"
    },
    {
      "companyName": "A. Rohde Immobilien & Vermögens-Verwaltungs GmbH",
      "registeredOffice": "Lennestadt",
      "status": "currently registered"
    }
  ],
  "totalCount": 100,
  "pagesScraped": 10,
  "searchTerm": "Rohde",
  "warning": "The maximum number of 100 hits has been exceeded. Please limit your request further."
}
```

### 200 OK — No Results

```json
{
  "results": [],
  "totalCount": 0,
  "pagesScraped": 1,
  "searchTerm": "xyznonexistentcompany99999",
  "warning": null
}
```

### 400 Bad Request — Validation Error

Returned when the `name` query parameter is missing, empty, or whitespace-only.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The 'name' query parameter is required and must not be empty."
}
```

### 503 Service Unavailable — Busy

Returned when a Germany search is already in progress (per-state semaphore).

```json
{
  "type": "https://BizScraper.local/errors/service-busy",
  "title": "Service Busy",
  "status": 503,
  "detail": "A Germany business search is already in progress. Please try again later."
}
```

### 500 Internal Server Error — Scraping Failure

Returned when the Handelsregister is unreachable, times out, or returns unexpected content.

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An error occurred while searching the German Handelsregister."
}
```

## Response Headers

| Header | Value | Description |
|--------|-------|-------------|
| Content-Type | application/json | Always JSON |
| X-Correlation-Id | {guid} | Request trace ID for diagnostics |

## Notes

- The `warning` field is only populated when the Handelsregister displays the exceeded-hits banner. It is `null` for normal searches.
- `totalCount` reflects the number of records actually scraped and returned in the `results` array.
- `pagesScraped` indicates how many paginator pages were visited (minimum 1, maximum 10 for 100 results at 10/page).
- All string fields use empty string (`""`) rather than `null` when the DOM cell exists but has no content.
- Results exclude "History" sub-rows and "Branches" nested tables from the Handelsregister page.
