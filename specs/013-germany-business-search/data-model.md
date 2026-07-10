# Data Model: Germany Business Entity Search (Handelsregister)

**Feature**: 013-germany-business-search  
**Date**: 2026-03-27

## Entities

### GermanyBusinessResult

A single scraped business entity record from the German Handelsregister results page.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| CompanyName | string | No | Business entity name extracted from the result row (e.g., "Rohde & Schwarz Group Services GmbH") |
| RegisteredOffice | string | No | City of registration (e.g., "Leipzig"). Empty string if absent on the page. |
| Status | string | No | Registration status (e.g., "currently registered"). Empty string if absent. |

**Source**: Extracted from each `table.ui-panelgrid.ui-widget` result block on the Handelsregister results page. Row 2 (`.ui-panelgrid-odd`) contains the data cells: CompanyName (class `paddingBottom20Px`, colspan 5), RegisteredOffice (class `sitzSuchErgebnisse`, colspan 1), Status (colspan 1).

**Excluded**: "History" sub-rows and "Branches" nested tables are not extracted.

---

### GermanySearchResponse

The API response envelope for a Germany business search.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Results | GermanyBusinessResult[] | No | Array of scraped results. Empty array if no matches. |
| TotalCount | int | No | Number of records in the Results array |
| PagesScraped | int | No | Number of paginator pages scraped (1 if single-page, up to 10 for 100 results) |
| SearchTerm | string | No | The original search term submitted by the caller |
| Warning | string | Yes | The exceeded-hits warning message if present, e.g., "The maximum number of 100 hits has been exceeded. Please limit your request further." Null if no warning. |

---

### SearchAuditEntry (Extended)

Extension of the existing `dbo.SearchAuditEntries` table.

| Field | Type | Nullable | Description | Change |
|-------|------|----------|-------------|--------|
| Id | uniqueidentifier | No | Primary key | Existing |
| SearchTerm | nvarchar(200) | No | Search input | Existing |
| ResultCount | int | No | Number of results scraped | Existing |
| Truncated | bit | No | Whether results were truncated | Existing |
| PagesScraped | int | No | Number of pages scraped | Existing |
| DurationMs | bigint | No | Scraping duration in milliseconds | Existing |
| Status | nvarchar(50) | No | Outcome status (Success, ValidationError, Busy, Error) | Existing |
| ErrorDetail | nvarchar(2000) | Yes | Exception message if failed | Existing |
| CreatedAt | datetimeoffset | No | UTC timestamp | Existing |
| CorrelationId | nvarchar(100) | No | Request trace ID | Existing |
| DiagnosticsUrl | nvarchar(2000) | Yes | Blob storage folder URL for screenshots | Existing |
| State | nvarchar(10) | Yes | State/country code ("CO", "WY", "DE") | Existing |
| WarningMessage | nvarchar(500) | Yes | Exceeded-hits warning message (Germany-specific but extensible) | **NEW** |

---

## State Transitions

### Search Request Lifecycle

```
[Received] → [Validated] → [Semaphore Acquired] → [Scraping] → [Audit Written] → [Response Returned]
                  ↓                    ↓                  ↓
           [ValidationError]     [Busy/503]     [ScrapeError/Timeout]
                  ↓                    ↓                  ↓
           [Audit Written]      [Audit Written]   [Audit Written]
                  ↓                    ↓                  ↓
           [400 Response]       [503 Response]    [500 Response]
```

### Audit Status Values

| Status | Meaning |
|--------|---------|
| Success | Scraping completed, results returned |
| ValidationError | Empty/invalid search term |
| Busy | Concurrent request rejected (semaphore not acquired) |
| Error | Scraping failed (timeout, site error, etc.) |

---

## Relationships

```
GermanySearchRequest (1) ───→ (1) GermanySearchResponse
                                    ├── (0..N) GermanyBusinessResult
                                    └── (0..1) Warning message

GermanySearchRequest (1) ───→ (1) SearchAuditEntry (State = "DE")
                                    └── (0..1) WarningMessage
```

---

## Validation Rules

- **SearchTerm**: Must be non-empty and non-whitespace. Trimmed before use.
- **CompanyName**: Always present in extraction; empty string if DOM cell is missing.
- **RegisteredOffice**: Empty string if `.sitzSuchErgebnisse` cell not found.
- **Status**: Empty string if status cell not found.
- **Warning**: Only populated when the `div.warning.displayBlock` element is present on the results page.
