# Data Model: CO SOS Business Entity Search API

**Date**: 2026-03-24
**Feature**: 001-cosos-business-search-api

## Entities

### BusinessEntityResult

A single row scraped from the CO SOS results table.

| Field | Type | Required | Source | Notes |
|-------|------|----------|--------|-------|
| DocumentNumber | string | Yes (may be empty) | Table column 3 (`Document Number`) | May be empty for some records; empty string, not null |
| Name | string | Yes | Table column 4 (`Name`) | Full entity name including status text (e.g., "ACME CO, Colorado Authority Terminated August 1, 2002") |
| Event | string | Yes (may be empty) | Table column 5 (`Event`) | e.g., "Batch Event" |
| Status | string | Yes (may be empty) | Table column 6 (`Status`) | e.g., "Effective", "Revoked" |
| Form | string | Yes (may be empty) | Table column 7 (`Form`) | Entity form type, e.g., "TRDNM", "FPC", "LLC" |
| FormationDate | string | Yes (may be empty) | Table column 8 (`Formation Date`) | Date string in MM/DD/YYYY format as displayed on the site |
| DetailsUrl | string | Yes | Hyperlink on `ID Number` column (column 2) | Full URL to `BusinessEntityDetail.do` page |

**Validation rules**:
- All fields are present in every record (FR-011)
- Missing/empty cells are represented as empty strings, never null or omitted
- DetailsUrl must be a valid absolute URL

### SearchRequest

The API input.

| Field | Type | Required | Validation |
|-------|------|----------|------------|
| Name | string | Yes | Non-empty, non-whitespace (FR-009). Trimmed before use. |

**Validation rules**:
- Empty or whitespace-only values rejected with 400 Bad Request before any scraping occurs

### SearchResponse

The API output envelope.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Results | BusinessEntityResult[] | Yes | Array of scraped records; empty array if no matches |
| TotalCount | int | Yes | Number of records in the Results array |
| SiteTotalCount | int | Yes (when available) | Total records reported by CO SOS site ("Found N matching record(s)"); -1 if unavailable |
| Truncated | bool | Yes | `true` when pagination was capped at 10 pages (FR-014) |
| PagesScraped | int | Yes | Number of result pages actually scraped |
| SearchTerm | string | Yes | Echo of the input search term |

### ErrorResponse (RFC 9457 Problem Details)

All error conditions use the standard RFC 9457 Problem Details format.

| HTTP Status | Error Type | When |
|-------------|-----------|------|
| 400 Bad Request | `validation-error` | Empty/whitespace search term (FR-009) |
| 422 Unprocessable Content | `exceeded-record-count` | CO SOS site returns "Exceeded Record Count" error (FR-008) |
| 503 Service Unavailable | `service-busy` | Another search is already in progress (FR-012). Includes `Retry-After` header. |
| 502 Bad Gateway | `upstream-error` | CO SOS site unreachable, timeout, or unexpected response (FR-010) |
| 502 Bad Gateway | `scraping-error` | Pagination failure mid-scrape (FR-013) |

### SearchAuditEntry

Audit trail entity persisted to Azure SQL MI.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| Id | Guid | Yes | Primary key |
| SearchTerm | string | Yes | The search term submitted |
| ResultCount | int | Yes | Number of results returned |
| Truncated | bool | Yes | Whether pagination was capped |
| PagesScraped | int | Yes | Number of pages scraped |
| DurationMs | long | Yes | Total request duration in milliseconds |
| Status | string | Yes | "Success", "Error", "ExceededRecordCount", "Timeout", "Busy" |
| ErrorDetail | string | No | Error message if status is not "Success" |
| CreatedAt | DateTimeOffset | Yes | Timestamp of the search |
| CorrelationId | string | Yes | Request correlation ID for tracing |

## State Transitions

### Request Processing States

```text
Received → Validating → [Invalid → 400/422 response]
                       → [Valid → Acquiring Lock]
                           → [Busy → 503 response]
                           → [Acquired → Scraping]
                               → [Page 1..N → Aggregating]
                               → [Page Error → 502 response]
                               → [Page Cap → Truncated response]
                               → [Complete → 200 response]
```

## Relationships

```text
SearchRequest (1) ──triggers──> (1) SearchResponse
SearchRequest (1) ──audited-as──> (1) SearchAuditEntry
SearchResponse (1) ──contains──> (0..N) BusinessEntityResult
```
