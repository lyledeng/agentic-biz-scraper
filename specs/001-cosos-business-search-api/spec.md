# Feature Specification: CO SOS Business Entity Search API

**Feature Branch**: `001-cosos-business-search-api`  
**Created**: 2026-03-24  
**Status**: Draft  
**Input**: User description: "I would like to build an API to allow searching using names on the CO SOS Web Site. State URL is https://www.sos.state.co.us/biz/BusinessEntityResults.do. Enter the search term, click Search, and scrape results table data into JSON across all pages."

## User Scenarios & Testing *(mandatory)*

<!--
  User stories prioritized as independent, testable journeys.
-->

### User Story 1 - Single-Page Business Name Search (Priority: P1)

A consumer of the API sends a business name search term and receives a JSON array of matching business entity records scraped from the Colorado Secretary of State website. The search navigates to the CO SOS Business Database Search page, enters the term into the search field, submits the form, and extracts all result rows from the first page of results.

**Why this priority**: This is the core capability — without it, no other feature has value. A single-page search covers the majority of specific (non-broad) searches and proves the end-to-end scraping pipeline.

**Independent Test**: Can be fully tested by sending a specific business name (e.g., "Acme Construction Colorado") and verifying the JSON response contains the expected fields with correct data matching the CO SOS website.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a search request with the name "Acme Construction Colorado", **Then** the API returns a JSON array where each element contains DocumentNumber, Name, Event, Status, Form, FormationDate, and DetailsUrl fields.
2. **Given** the API is running, **When** a caller submits a search request that returns results, **Then** every record in the JSON response has a DetailsUrl pointing to the corresponding BusinessEntityDetail page on the CO SOS site.
3. **Given** the API is running, **When** a caller submits a search request with a name that yields no results, **Then** the API returns an empty JSON array and a clear status indicating no matches were found.

---

### User Story 2 - Multi-Page Result Pagination (Priority: P2)

When a search term returns results spanning multiple pages on the CO SOS site, the API automatically navigates through all result pages and aggregates every record into a single consolidated JSON response. The caller receives the complete dataset without needing to know about or manage pagination.

**Why this priority**: Many common business name searches return dozens or hundreds of results across multiple pages. Without pagination support, the API would silently return incomplete data, making it unreliable for real-world use.

**Independent Test**: Can be tested by searching for a broad-but-valid term (e.g., "Mountain") that returns multi-page results and verifying the total record count in the JSON matches the "Found N matching record(s)" count displayed on the CO SOS site.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a search that returns results spanning 3 pages on the CO SOS site, **Then** the API response contains all records from all 3 pages in a single JSON array.
2. **Given** a multi-page search result, **When** the API aggregates records, **Then** the total record count matches the "Found N matching record(s)" indicator displayed on the CO SOS results page.
3. **Given** a multi-page search result, **When** the API processes the final page, **Then** it stops pagination (does not attempt to navigate beyond the last page).

---

### User Story 3 - Search Error and Edge Case Handling (Priority: P3)

When a search term is too broad and the CO SOS site returns an "Exceeded Record Count" error instead of results, or when the site is unreachable or returns unexpected content, the API communicates the issue clearly to the caller with an appropriate error response rather than failing silently or returning partial data.

**Why this priority**: Robust error handling is essential for a production API. The CO SOS site enforces result limits and can be intermittently unavailable. Callers need clear, actionable error information.

**Independent Test**: Can be tested by submitting an overly broad search term (e.g., a single common letter) and verifying the API returns a structured error response indicating the search was too broad.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a very broad search term that triggers the CO SOS "Exceeded Record Count" error, **Then** the API returns a structured error response indicating the search must be refined.
2. **Given** the API is running, **When** the CO SOS website is unreachable or times out during a search, **Then** the API returns a structured error response indicating a connectivity issue.
3. **Given** the API is running, **When** a caller submits an empty or whitespace-only search term, **Then** the API returns a validation error before attempting to contact the CO SOS site.

---

### Edge Cases

- What happens when the CO SOS site changes its page structure or table layout?
- How does the system handle a search term that contains special characters (e.g., `&`, `"`, accented characters)?
- What happens if the CO SOS site introduces a CAPTCHA or rate-limiting mechanism during scraping?
- How does the system handle intermittent network failures mid-pagination (e.g., page 1 succeeds, page 2 fails)? → **Resolved**: Fail entirely, return error with zero results (see FR-013).
- What happens when the CO SOS site returns a result row with missing or empty cells (e.g., no Document Number)?
- How does the system handle extremely large result sets (hundreds of pages) without exhausting resources? → **Resolved**: Pagination capped at 10 pages with truncation indicator (see FR-014).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a REST/HTTP endpoint that accepts a business name search term as input and returns JSON.
- **FR-002**: The system MUST navigate to the CO SOS Business Database Search page and submit the search term via the site's search form.
- **FR-003**: The system MUST use browser automation to interact with the CO SOS website, handling its server-rendered form-based workflow.
- **FR-004**: The system MUST extract the following fields from each result row: DocumentNumber, Name, Event, Status, Form, FormationDate, and DetailsUrl.
- **FR-005**: The DetailsUrl field MUST contain the full URL to the business entity detail page on the CO SOS site (the link from the ID Number column).
- **FR-006**: When search results span multiple pages, the system MUST navigate through all pages and aggregate all records into a single response.
- **FR-007**: The system MUST return results as a JSON array of objects with the specified field names.
- **FR-008**: The system MUST return a structured error response (HTTP 422) when the CO SOS site displays an "Exceeded Record Count" error.
- **FR-009**: The system MUST validate that the search term is non-empty before initiating a search.
- **FR-010**: The system MUST handle CO SOS site unavailability or timeout by returning a clear error response.
- **FR-011**: The system MUST handle result rows that have empty or missing cell values by including the field with an empty string rather than omitting it.
- **FR-012**: The system MUST process one search request at a time. If a request arrives while another search is in progress, the system MUST reject it with a structured "busy" error response.
- **FR-013**: If a pagination failure occurs (e.g., a subsequent page fails to load after earlier pages succeeded), the system MUST discard any partially collected results and return a structured error response with zero results.
- **FR-014**: The system MUST stop pagination after 10 pages. If more pages exist, the system MUST return all results collected from the first 10 pages along with a "truncated" indicator in the response metadata showing that additional results were not retrieved.
- **FR-015**: The system MUST persist an audit record for every search request, capturing the search term, result count, truncation flag, pages scraped, duration, outcome status, error detail (if any), timestamp, and correlation ID.

### Key Entities

- **SearchRequest**: The input to the REST/HTTP API — a business name search term provided as a query parameter by the caller.
- **BusinessEntityResult**: A single scraped result record containing DocumentNumber, Name, Event, Status, Form, FormationDate, and DetailsUrl. Represents one row from the CO SOS results table.
- **SearchResponse**: The API output — a collection of BusinessEntityResult records, along with metadata such as total result count and any error information.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caller can submit a business name search and receive complete, accurate results matching the CO SOS website within 30 seconds for single-page results and within 120 seconds for multi-page results (up to 10 pages).
- **SC-002**: For multi-page results, all records across all pages are returned in a single response — the count matches the CO SOS "Found N matching record(s)" indicator with 100% accuracy.
- **SC-003**: 100% of the seven specified data fields (DocumentNumber, Name, Event, Status, Form, FormationDate, DetailsUrl) are present in every result record.
- **SC-004**: Error conditions (too-broad search, site unavailable, invalid input) produce structured, descriptive error responses — no silent failures or unstructured exceptions.
- **SC-005**: The API successfully handles searches that span up to 10 pages without data loss or timeout. When results exceed 10 pages, the response includes a truncation indicator.

## Clarifications

### Session 2026-03-24

- Q: What API interface protocol should the system use? → A: REST/HTTP API (e.g., `GET /search?name=...` returns JSON)
- Q: How should the API handle concurrent requests? → A: Sequential — process one search at a time; reject additional requests while busy
- Q: What should happen if pagination fails mid-way (e.g., page 1 succeeds, page 2 fails)? → A: Fail entirely — return an error with zero results; no partial data
- Q: Should there be a maximum result/page cap to prevent runaway scraping? → A: Cap at 10 pages; stop pagination and return collected results with a "truncated" indicator

## Assumptions

- The CO SOS Business Database Search page structure (form fields, table columns, pagination controls) remains stable for the foreseeable future.
- The CO SOS site does not employ CAPTCHA or aggressive bot-detection that would block automated browser interactions.
- Azure Playwright workspaces will be used as the execution environment for browser automation; specific configuration details will be provided during the planning phase.
- The API does not need authentication or authorization for v1 — it is an internal/trusted service.
- The CO SOS site's TLS certificate may trigger errors through corporate network proxies; the browser automation must be configured to tolerate certificate errors.
- Rate-limiting or throttling of requests to the CO SOS site is not required for v1 but may be added later.
- The "Document Number" column on the CO SOS site may be empty for some records; this is expected and not an error.
