# Quickstart: Semantic Error Handling for Colorado Exceeded Record Count

**Feature**: 012-co-record-count-error  
**Date**: 2026-03-27

## What Changed

The Colorado "Exceeded Record Count" error now returns a semantic HTTP 422 response instead of a generic 500. The system no longer retries when this condition occurs.

## Before

```
GET /api/v1/business-search?searchTerm=a&state=CO

HTTP 500
{
  "type": "https://BizScraper.local/errors/unhandled-error",
  "title": "Unhandled Error",
  "status": 500,
  "detail": "An unexpected error occurred.",
  "instance": "/api/v1/business-search"
}
```

Response time: ~30+ seconds (retries exhausted)

## After

```
GET /api/v1/business-search?searchTerm=a&state=CO

HTTP 422
{
  "type": "https://BizScraper.local/errors/exceeded-record-count",
  "title": "Exceeded Record Count",
  "status": 422,
  "detail": "The search term is too broad. The CO SOS site reported: Exceeded Record Count. Please refine your search.",
  "instance": "/api/v1/business-search"
}
```

Response time: ~5 seconds (no retries)

## How to Test

1. Start the API: `dotnet run --project src/BizScraper.Api`
2. Send a broad search: `GET /api/v1/business-search?searchTerm=a&state=CO`
3. Verify: HTTP 422 with `exceeded-record-count` type
4. Run unit tests: `dotnet test tests/BizScraper.UnitTests`
5. Run integration tests: `dotnet test tests/BizScraper.IntegrationTests`
