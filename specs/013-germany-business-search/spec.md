# Feature Specification: Germany Business Entity Search (Handelsregister)

**Feature Branch**: `013-germany-business-search`  
**Created**: 2026-03-27  
**Status**: Draft  
**Input**: User description: "I want to add a new capability to perform a business entity search in Germany. This should be exposed as a new API endpoint. Navigate to https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml, enter the company name in the 'Company or keywords' edit box, click Find, and return CompanyName, RegisteredOffice, and Status. Scrape all pages if multiple pages exist. If the maximum of 100 hits is exceeded, return the warning in the response JSON. Update the test-ui to add this capability."

## Clarifications

### Session 2026-03-27

- Q: Should Germany search integrate into the existing `/api/v1/business-search` endpoint (stateCode=DE) or be a new separate endpoint? → A: New separate endpoint `/api/v1/germany-search`.
- Q: How should TLS certificate validation be handled for the Handelsregister site (corporate proxy causes cert errors)? → A: Ignore TLS errors only in dev/local; enforce strict TLS in staging/production.
- Q: If a mid-pagination failure occurs (e.g., page 5 of 10 fails), should partial results be returned or discarded? → A: Retry the failed page up to 2 times, then discard all partial results and return a structured error if still failing.
- Q: Should screenshots of the scraping session be captured and stored for diagnostics? → A: Yes, capture screenshots per scraping step and store in blob storage, following existing CO/WY patterns.
- Q: Does the scraping engine need explicit AJAX-aware pagination handling for the Handelsregister's PrimeFaces paginator? → A: Yes, explicitly require AJAX-aware pagination — the scraping flow must wait for table content refresh after paginator click, not a full page navigation.

## User Scenarios & Testing *(mandatory)*

<!--
  User stories prioritized as independent, testable journeys.
-->

### User Story 1 - Single-Page Germany Business Name Search (Priority: P1)

A consumer of the API sends a company name or keywords and receives a JSON array of matching business entity records scraped from the German Handelsregister (Common Register Portal). The search navigates to the Handelsregister Normal Search page, enters the term into the "Company or keywords" field, submits the form, and extracts all result rows from the first page of results. Each result contains the company name, registered office (city), and registration status.

**Why this priority**: This is the core capability — without it, no other feature has value. A single-page search proves the end-to-end scraping pipeline against the German register and covers the majority of specific company name searches.

**Independent Test**: Can be fully tested by sending the exact company name "Rohde & Schwarz Group Services GmbH" and verifying the JSON response contains exactly one record with CompanyName, RegisteredOffice ("Leipzig"), and Status ("currently registered").

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a search request with the name "Rohde & Schwarz Group Services GmbH", **Then** the API returns a JSON response containing one record with CompanyName, RegisteredOffice, and Status fields populated correctly.
2. **Given** the API is running, **When** a caller submits a search request that returns results, **Then** every record in the JSON response has non-empty CompanyName and Status fields.
3. **Given** the API is running, **When** a caller submits a search request with a name that yields no results, **Then** the API returns an empty JSON array and the response metadata indicates zero matches found (the Handelsregister displays "There are no results for this search." and "0-0 of 0 records").

---

### User Story 2 - Multi-Page Result Pagination (Priority: P2)

When a search term returns results spanning multiple pages on the Handelsregister site (default 10 results per page, up to 100 total hits), the API automatically navigates through all result pages and aggregates every record into a single consolidated JSON response. The caller receives the complete dataset without needing to know about or manage pagination.

**Why this priority**: Many keyword searches return dozens of results across multiple pages. Without pagination support, the API would silently return incomplete data from only the first page, making it unreliable for broad searches.

**Independent Test**: Can be tested by searching for "Rohde" (which returns 100 hits across 10 pages) and verifying the total record count in the JSON approximates the count displayed on the Handelsregister site (the paginator shows "1-10 of 100 records").

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a search for "Rohde" that returns results spanning multiple pages on the Handelsregister, **Then** the API response contains records from all pages aggregated into a single JSON array.
2. **Given** a multi-page search result, **When** the API reaches the final page, **Then** it stops pagination and does not attempt to navigate beyond the last page.
3. **Given** a multi-page search result, **When** the API aggregates records, **Then** the response metadata includes the total record count matching the paginator display (e.g., "100 records").

---

### User Story 3 - Exceeded-Hits Warning Passthrough (Priority: P2)

When a search term is too broad and the Handelsregister displays the warning "The maximum number of 100 hits has been exceeded. Please limit your request further.", the API includes this warning in the JSON response alongside whatever results are returned. The caller receives both the available data and the warning indicator so they can decide whether to refine their search.

**Why this priority**: This is explicitly requested behavior. Returning the warning alongside results gives callers actionable feedback without losing the partial data set (the site still returns up to 100 results even when the warning fires).

**Independent Test**: Can be tested by searching for "Rohde" and verifying the JSON response contains both a populated results array and a warning field with the exceeded-hits message.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a broad search for "Rohde" that triggers the exceeded-hits warning on the Handelsregister, **Then** the API response JSON includes a warning field containing the message "The maximum number of 100 hits has been exceeded. Please limit your request further."
2. **Given** the exceeded-hits warning is present, **When** the API builds the response, **Then** it still includes all available result records (up to 100) alongside the warning — it does not discard results.
3. **Given** a search that does not trigger the warning, **When** the API builds the response, **Then** the warning field is absent or null.

---

### User Story 4 - Test UI Germany Search Tab (Priority: P3)

The existing test-ui Angular application is updated with a new capability to perform Germany business searches. Users can enter a company name, initiate a search, and see results displayed in a table showing CompanyName, RegisteredOffice, and Status. The UI also displays the exceeded-hits warning if present.

**Why this priority**: The test UI provides a visual way to exercise and demo the new API endpoint, but the API stands on its own value and must work first.

**Independent Test**: Can be tested by opening the test-ui, navigating to the Germany search capability, entering "Rohde" as the search term, clicking search, and verifying the results table populates and the exceeded-hits warning banner appears.

**Acceptance Scenarios**:

1. **Given** the test-ui is open, **When** a user navigates to the Germany search capability and enters "Rohde & Schwarz Group Services GmbH" and clicks search, **Then** the results table shows one row with the company name, registered office, and status.
2. **Given** a search for "Rohde" that triggers the exceeded-hits warning, **When** results are displayed, **Then** a warning banner is shown with the exceeded-hits message above the results table.
3. **Given** a search term that returns no results, **When** the UI receives the empty response, **Then** an appropriate "no results found" message is displayed.

---

### User Story 5 - Error and Edge Case Handling (Priority: P3)

When the Handelsregister site is unreachable, times out, or returns unexpected content, the API communicates the issue clearly to the caller with an appropriate error response rather than failing silently or returning partial data.

**Why this priority**: Robust error handling is essential for a production API. The Handelsregister site may be intermittently unavailable or may impose rate limiting. Callers need clear, actionable error information.

**Independent Test**: Can be tested by simulating a site timeout (e.g., via network mocking) and verifying the API returns a structured error response indicating a connectivity issue.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** the Handelsregister website is unreachable or times out during a search, **Then** the API returns a structured error response indicating a connectivity issue.
2. **Given** the API is running, **When** a caller submits an empty or whitespace-only search term, **Then** the API returns a validation error before attempting to contact the Handelsregister site.

---

### Edge Cases

- What happens when the Handelsregister changes its page structure or form element IDs (JSF-generated IDs like `form:schlagwoerter`)?
- How does the system handle a search term containing special characters (e.g., `&`, `"`, umlauts like `ü`, `ö`, `ä`)?
- What happens if the Handelsregister introduces a CAPTCHA or rate-limiting mechanism?
- How does the system handle intermittent network failures mid-pagination (e.g., page 3 of 10 fails to load)?
- What happens when a result row has missing or empty cells (e.g., no registered office)?
- How are "History" sub-rows and "Branches" sub-rows handled — are they included or excluded from the main result set?
- What happens if a session cookie or JSF ViewState expires during a multi-page scrape?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a new API endpoint at `/api/v1/germany-search` that accepts a company name or keywords as input and returns JSON results from the German Handelsregister. This endpoint is independent from the existing `/api/v1/business-search` (CO/WY) endpoint.
- **FR-002**: The system MUST navigate to the Handelsregister Normal Search page (`https://www.handelsregister.de/rp_web/normalesuche/welcome.xhtml`) and submit the search term via the "Company or keywords" field.
- **FR-003**: The system MUST use browser automation to interact with the Handelsregister, handling its JSF server-rendered form workflow (PrimeFaces components, ViewState tokens). TLS certificate errors MUST be ignored in dev/local environments only; staging and production MUST enforce strict TLS validation.
- **FR-004**: The system MUST extract the following fields from each result row: CompanyName, RegisteredOffice, and Status.
- **FR-005**: When search results span multiple pages, the system MUST navigate through all pages using the PrimeFaces paginator and aggregate all records into a single response. Because the Handelsregister paginator uses AJAX partial page updates (not full page navigations), the scraping flow MUST wait for the results table content to refresh after each paginator click before extracting data.
- **FR-006**: The system MUST return results as a JSON array of objects with the specified field names (CompanyName, RegisteredOffice, Status).
- **FR-007**: When the Handelsregister displays the warning "The maximum number of 100 hits has been exceeded. Please limit your request further.", the system MUST include this warning message in the JSON response as a distinct warning field alongside the results.
- **FR-008**: The system MUST validate that the search term is non-empty before initiating a search.
- **FR-009**: The system MUST handle Handelsregister site unavailability or timeout by returning a clear error response.
- **FR-010**: The system MUST handle result rows with empty or missing cell values by including the field with an empty string rather than omitting it.
- **FR-011**: The system MUST return an empty results array when the Handelsregister displays "There are no results for this search." (0-0 of 0 records).
- **FR-012**: The system MUST stop pagination after the last available page. If the paginator indicates no further pages (the "Next" link is disabled), the system MUST not attempt to navigate beyond the last page.
- **FR-013**: If a pagination failure occurs (e.g., a subsequent page fails to load), the system MUST retry the failed page up to 2 times. If the page still fails after retries, the system MUST discard all partial results and return a structured error response.
- **FR-014**: The system MUST persist an audit record for every search request, capturing the search term, result count, warning presence, pages scraped, duration, outcome status, error detail (if any), timestamp, correlation ID, and cloud storage URL where screenshots are stored.
- **FR-015**: The response metadata MUST include the total number of records returned and the number of pages scraped.
- **FR-016**: The test-ui MUST be updated with a new Germany search capability that allows users to enter a company name, trigger the search, view results in a table, and see any exceeded-hits warning.
- **FR-017**: The system MUST capture screenshots at each scraping step (search page load, form submission, each results page) and store them in blob storage organized under a folder tied to the search request correlation ID, following the existing CO/WY screenshot storage patterns.
- **FR-018**: The scraping flow definition for Germany MUST explicitly handle AJAX-based partial page updates. After clicking a paginator page link, the system MUST wait for the results table DOM to update (e.g., via a network-idle or element-content-change signal) before extracting data from the refreshed page. The URL does not change between pages.

### Key Entities

- **GermanySearchRequest**: The input to the API — a company name or keywords provided as a query parameter by the caller.
- **GermanyBusinessResult**: A single scraped result record containing CompanyName (the business entity name), RegisteredOffice (the city of registration), and Status (e.g., "currently registered"). Represents one main result row from the Handelsregister results page.
- **GermanySearchResponse**: The API output — a collection of GermanyBusinessResult records, along with metadata (total count, pages scraped) and an optional warning field for the exceeded-hits message.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A search for "Rohde & Schwarz Group Services GmbH" returns exactly 1 result with CompanyName containing "Rohde & Schwarz Group Services GmbH", RegisteredOffice "Leipzig", and Status "currently registered".
- **SC-002**: A search for "Rohde" returns up to 100 results from all available pages and includes the exceeded-hits warning in the response.
- **SC-003**: A search for a nonsensical term (e.g., "xyznonexistentcompany99999") returns an empty array with zero records.
- **SC-004**: Users can complete a Germany business search via the test-ui from entering a term to viewing results in under 60 seconds.
- **SC-005**: The API returns a structured validation error within 1 second when an empty search term is submitted.
- **SC-006**: The system correctly scrapes all 10 pages of results (100 records) for a broad search without data loss or duplication.

## Assumptions

- The existing browser automation and JSON-driven scraping engine used for CO SOS searches will be extended to support the Handelsregister, following established project patterns.
- The Handelsregister site is publicly accessible and does not require authentication or CAPTCHA for normal keyword searches.
- The default search mode ("contain all keywords") is used unless the caller specifies otherwise; exact-name matching is an optional enhancement for later.
- Only main result rows (CompanyName, RegisteredOffice, Status) are extracted; "History" sub-rows and "Branches" sub-rows are excluded from the primary result set in v1.
- The Handelsregister caps results at 100 hits; the system does not need to handle result sets larger than 100.
- The existing audit trail infrastructure will be reused for Germany search requests.
- The test-ui update follows established patterns (separate section/tab or route for Germany search, similar to existing state search UI).
