# Data Model: Multi-State Business Entity Search

**Feature**: `005-multi-state-search`  
**Date**: 2026-03-25

## Entities

### NormalizedSearchResult (New)

Unified representation of a business entity search result across all supported states.

| Field | Type | Required | Source (CO) | Source (WY) | Description |
|-------|------|----------|-------------|-------------|-------------|
| `name` | `string` | Yes | `BusinessEntityResult.Name` | Parsed from result line (before ` - `) | Business entity name |
| `identifier` | `string` | Yes | `BusinessEntityResult.DocumentNumber` | Parsed filing number (e.g., `2018-000797412`) | State-assigned identifier |
| `status` | `string` | Yes | `BusinessEntityResult.Status` | Parsed from "Status: ..." line | Current filing status |
| `entityType` | `string` | Yes | `BusinessEntityResult.Form` | Parsed from parenthetical (e.g., `LLC`, `CORP`) | Entity type / form |
| `formationDate` | `string` | Yes | `BusinessEntityResult.FormationDate` | Parsed from "Filed On: ..." line | Date of formation/filing |
| `detailsUrl` | `string` | Yes | `BusinessEntityResult.DetailsUrl` | Resolved absolute URL from `FilingDetails.aspx?eFNum=...` | Link to entity detail page |
| `state` | `string` | Yes | `"CO"` (literal) | `"WY"` (literal) | Two-letter state code |
| `event` | `string?` | No | `BusinessEntityResult.Event` | `null` | CO-specific: last event type |
| `standingTax` | `string?` | No | `null` | Parsed from "Standing - Tax: ..." line | WY-specific: tax standing |
| `standingRA` | `string?` | No | `null` | Parsed from "Standing - RA: ..." line | WY-specific: registered agent standing |

**Validation rules**: `name`, `identifier`, `status`, `entityType`, `formationDate`, `detailsUrl`, `state` must be non-null, non-empty strings.

### NormalizedSearchResponse (New)

Response envelope for all multi-state search requests.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `results` | `NormalizedSearchResult[]` | Yes | Array of normalized entity results |
| `totalCount` | `int` | Yes | Number of results returned |
| `siteTotalCount` | `int` | Yes | Total results reported by the state site |
| `truncated` | `bool` | Yes | Whether results were truncated due to page limit |
| `pagesScraped` | `int` | Yes | Number of pages actually scraped |
| `searchTerm` | `string` | Yes | The original search term |
| `state` | `string` | Yes | The state that was searched ("CO" or "WY") |

### NormalizedSearchScrapeResult (New)

Intermediate result returned by `IStateSearchScraper` implementations. Each scraper maps its own state-specific fields to `NormalizedSearchResult` internally, so the handler receives pre-normalized data.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `results` | `IReadOnlyList<NormalizedSearchResult>` | Yes | Pre-normalized entity results |
| `siteTotalCount` | `int` | Yes | Total results reported by the state site |
| `truncated` | `bool` | Yes | Whether results were truncated due to page limit |
| `pagesScraped` | `int` | Yes | Number of pages actually scraped |

### SearchAuditEntry (Modified)

Extended with a `State` column.

| Field | Type | Required | Change | Description |
|-------|------|----------|--------|-------------|
| `Id` | `Guid` | Yes | Existing | Primary key |
| `SearchTerm` | `string` | Yes | Existing | Search term |
| `ResultCount` | `int` | Yes | Existing | Number of results |
| `Truncated` | `bool` | Yes | Existing | Truncation flag |
| `PagesScraped` | `int` | Yes | Existing | Pages scraped |
| `DurationMs` | `long` | Yes | Existing | Duration in milliseconds |
| `Status` | `string` | Yes | Existing | Outcome status |
| `ErrorDetail` | `string?` | No | Existing | Error description |
| `CreatedAt` | `DateTimeOffset` | Yes | Existing | Timestamp |
| `CorrelationId` | `string` | Yes | Existing | Request correlation ID |
| `DiagnosticsUrl` | `string?` | No | Existing | Cloud diagnostics URL |
| **`State`** | **`string?`** | **No** | **NEW** | **Two-letter state code ("CO" or "WY"). NULL for pre-existing rows.** |

### FabCaptchaSolverRequest (New, internal)

Serialized request body sent to the FAB AI agent CAPTCHA solver.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `input` | `FabCaptchaSolverInput` | Yes | Wrapper object containing the image data |

#### FabCaptchaSolverInput

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `format` | `string` | Yes | Image format — always `"base64"` |
| `data` | `string` | Yes | Base64-encoded PNG screenshot of the CAPTCHA image |

### FabCaptchaSolverResponse (New, internal)

Deserialized response from the FAB AI agent CAPTCHA solver.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `output` | `FabCaptchaSolverOutput` | Yes | Wrapper object containing the solved text |

#### FabCaptchaSolverOutput

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `captchaText` | `string` | Yes | The extracted CAPTCHA text |
| `confidence` | `string` | Yes | Confidence level: "high", "medium", or "low" |

## Database Schema Changes

### Changelog: `2.0.2603.0-add-state-column-to-search-audit.sql`

```sql
-- liquibase formatted sql

-- changeset cososwebscraping:2.0.2603.0-add-state-column-to-search-audit
ALTER TABLE dbo.SearchAuditEntries
    ADD State NVARCHAR(10) NULL;

-- rollback ALTER TABLE dbo.SearchAuditEntries DROP COLUMN State;
```

## Relationships

```
NormalizedSearchResponse 1──* NormalizedSearchResult
   └─ state (same value propagated to each result)

SearchAuditEntry ←── SearchBusinessEntitiesHandler (writes one entry per search)
   └─ state (CO or WY, matches request parameter)

IStateSearchScraper ──> SearchScrapeResult ──> BusinessEntityResult[]
   └─ handler maps to NormalizedSearchResult[]
```

## State Transitions

No entity state transitions — the search is a stateless read operation. However, the WY scraper has a page-level state machine:

```
NAVIGATE → [CAPTCHA_CHECK] → CAPTCHA_SOLVE → SEARCH_FORM → SUBMIT → 
EXTRACT_RESULTS → [PAGINATE] → DONE
```

Where `CAPTCHA_CHECK` and `PAGINATE` are conditional branches.
