# Feature Specification: Multi-State Business Entity Search

**Feature Branch**: `005-multi-state-search`  
**Created**: 2026-03-25  
**Status**: Draft  
**Input**: User description: "I want to enhance the API. I want to add the ability to perform the initial search for Wyoming in addition to Colorado. Initial search page: https://wyobiz.wyo.gov/Business/FilingSearch.aspx. Enter the Filing Name and click on Search. Once the page renders, Extract the name, filing number, status - tax, standing - ra, status and filed-on. Reorganize the API to normalize the api to accept CO or WY as inputs in addition to the search term. Normalize the response json. Use this opportunity to describe the web scraping actions with semantic intent instead of the specific DOM model if that makes sense."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Wyoming Business Entity Search (Priority: P1)

As an API consumer, I want to search for business entities registered in Wyoming by providing a search term and the state code "WY", so that I can retrieve filing information from the Wyoming Secretary of State in the same normalized format as Colorado results.

**Why this priority**: This is the core new capability — without Wyoming search, there is no multi-state feature. It delivers immediate value by doubling the geographic coverage of the API.

**Independent Test**: Can be fully tested by sending a search request with `state=WY` and a known business name (e.g., "Wendy") and verifying the response contains Wyoming filing data in the normalized format.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a consumer sends a search request with `state=WY` and `name=Wendy`, **Then** the system returns a normalized response containing Wyoming business entity results with name, filing number, status, standing (tax), standing (RA), filed-on date, and a details URL for each result.
2. **Given** a Wyoming search returns multiple pages of results, **When** the system processes the search, **Then** it paginates through the results (up to the configured page limit) and aggregates all entities into a single response.
3. **Given** a Wyoming search returns zero results, **When** the system processes the search, **Then** it returns an empty result list with a total count of zero.
4. **Given** the Wyoming Secretary of State site is unreachable or returns an error, **When** the system attempts to search, **Then** it returns an appropriate error response indicating the upstream service is unavailable.

---

### User Story 2 - Normalized Multi-State API (Priority: P1)

As an API consumer, I want the search endpoint to accept a state code parameter (CO or WY) alongside the search term, so that I can use a single, consistent API interface regardless of which state I am searching.

**Why this priority**: Normalization is equally critical — without it, consumers would need to use different endpoints or interpret different response shapes per state, defeating the purpose of multi-state support.

**Independent Test**: Can be fully tested by sending search requests with `state=CO` and `state=WY` using the same search term and verifying both return the same normalized response structure.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a consumer sends a search request with `state=CO` and `name=Wendy`, **Then** the system returns Colorado results in the normalized response format (identical structure to Wyoming responses).
2. **Given** the API is running, **When** a consumer sends a search request with `state=WY` and `name=Wendy`, **Then** the system returns Wyoming results in the normalized response format.
3. **Given** a consumer sends a valid search request without a `state` parameter, **When** the system processes the request, **Then** it returns a 400 Bad Request error indicating the state parameter is required.
4. **Given** a consumer sends a request with an unsupported state code (e.g., `state=TX`), **When** the system processes the request, **Then** it returns a 400 Bad Request error indicating the state is not supported.

---

### User Story 3 - Normalized Response Schema (Priority: P1)

As an API consumer, I want the response JSON to follow a unified schema that accommodates fields from both Colorado and Wyoming, so that I can process results from any supported state without branching logic.

**Why this priority**: A normalized schema is fundamental to the multi-state concept. Without it, consumers must handle state-specific response shapes, making the integration fragile.

**Independent Test**: Can be verified by comparing responses from CO and WY searches and confirming they share the same top-level structure with common fields populated and state-specific fields present where applicable.

**Acceptance Scenarios**:

1. **Given** a successful Colorado search, **When** the response is returned, **Then** each result contains the common fields (name, identifier, status, entity type, formation date, details URL) and the state field set to "CO".
2. **Given** a successful Wyoming search, **When** the response is returned, **Then** each result contains the common fields (name, identifier, status, entity type, formation date, details URL) plus Wyoming-specific fields (standing tax, standing RA) and the state field set to "WY".
3. **Given** both CO and WY responses, **When** compared, **Then** the top-level response envelope (results array, total count, pagination info, search term, state) is identical in structure.

---

### User Story 4 - Backward-Compatible Existing Colorado Search (Priority: P2)

As an existing API consumer, I want my current Colorado search workflows to continue functioning after the API is reorganized, so that I do not experience breaking changes when multi-state support is introduced.

**Why this priority**: Protecting existing consumers from breakage is essential but secondary to delivering the new capability. A versioned or backward-compatible migration path ensures smooth adoption.

**Independent Test**: Can be verified by running existing CO search integration tests against the new endpoint and confirming they pass with the updated response format.

**Acceptance Scenarios**:

1. **Given** the API has been updated for multi-state support, **When** a consumer sends a search with `state=CO` and a known search term, **Then** the response contains all data previously available from the CO-only endpoint (document number, name, event, status, form, formation date, details URL).
2. **Given** the existing audit trail captures CO searches, **When** a CO search is performed via the new normalized endpoint, **Then** the audit trail continues to record the search with the state information included.
3. **Given** the endpoint now requires a mandatory `state` parameter, **When** a consumer sends a request without the `state` parameter, **Then** the system returns a 400 Bad Request response indicating the state parameter is required.

---

### User Story 5 - Semantic Scraping Action Abstraction (Priority: P2)

As a system maintainer, I want the web scraping logic to be expressed using semantic intent (e.g., "navigate to search page", "enter search term", "submit search", "extract results", "navigate to next page") rather than tightly coupled DOM selectors, so that each state's scraper can be maintained independently when site markup changes.

**Why this priority**: Abstracting scraping actions by intent improves maintainability and makes adding future states easier, but is a design-quality concern rather than a user-facing feature.

**Independent Test**: Can be verified by reviewing the scraper implementation and confirming that scraping steps are described at a semantic level (e.g., interface methods like "submit search", "extract results from current page", "advance to next page") with state-specific DOM details encapsulated within each state's scraper.

**Acceptance Scenarios**:

1. **Given** the scraper abstraction defines semantic scraping actions, **When** a new state scraper is implemented, **Then** it fulfills the same interface contract without modifying the handler or orchestration logic.
2. **Given** the Wyoming page markup changes, **When** the WY scraper is updated, **Then** no changes are required to the CO scraper, the handler, or the endpoint.

---

### Edge Cases

- What happens when the Wyoming site serves a CAPTCHA/bot-detection challenge? The system MUST detect the CAPTCHA, screenshot the challenge image, send it to the FAB (Foundations and Beyond) AI agent for solving, submit the returned answer, and proceed with the search. If the CAPTCHA solution fails, the system returns an appropriate error.
- What happens when the Wyoming site's ASP.NET postback pagination fails mid-scrape? The system should return partial results collected so far and indicate truncation.
- What happens when the search term contains special characters (e.g., `&`, `'`, `"`) that may be handled differently by CO vs. WY form submissions? The system should properly escape and submit the term for each state's form technology.
- What happens when the Wyoming site returns results with unexpected or missing fields (e.g., no Standing - Tax value)? The system should return null/empty for missing fields rather than failing.
- What happens when both CO and WY scrapers attempt to use the same browser pool simultaneously? The system should handle concurrent cross-state requests without interference.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The search endpoint (`/api/v1/business-search`) MUST accept a mandatory `state` query parameter that identifies the target state (initially "CO" or "WY"). The endpoint path remains unchanged from the current API; only the addition of the `state` parameter distinguishes the normalized multi-state behavior.
- **FR-002**: The search endpoint MUST accept a mandatory `name` parameter containing the business name search term.
- **FR-003**: The system MUST validate the `state` parameter and reject unsupported values with a 400 Bad Request response containing a descriptive error message.
- **FR-004**: The system MUST validate the `name` parameter and reject empty or whitespace-only values with a 400 Bad Request response.
- **FR-005**: For Wyoming searches, the system MUST navigate to the Wyoming Secretary of State filing search page, enter the search term in the "Filing Name" field, and submit the search.
- **FR-006**: For Wyoming searches, the system MUST extract the following fields from each result: business name, filing number, entity type, status, standing (tax), standing (RA), filed-on date, and the details URL.
- **FR-007**: For Wyoming searches, the system MUST handle multi-page results by navigating through paginated pages up to a configurable page limit.
- **FR-008**: For Colorado searches, the system MUST continue to extract: document number, name, event, status, form (entity type), formation date, and the details URL.
- **FR-009**: The response JSON MUST use a normalized schema with common fields populated for both states: `name`, `identifier` (CO document number / WY filing number), `status`, `entityType` (CO form / WY entity type from result line), `formationDate` (CO formation date / WY filed-on), `detailsUrl`, and `state`.
- **FR-010**: The response JSON MUST include state-specific fields when available: Colorado results include `event`; Wyoming results include `standingTax` and `standingRA`.
- **FR-011**: The response envelope MUST include: `results` array, `totalCount`, `siteTotalCount`, `truncated` flag, `pagesScraped`, `searchTerm`, and `state`.
- **FR-012**: The system MUST record the state code in the audit trail entry for every search request.
- **FR-013**: The system MUST provide diagnostic screenshots and tracing for Wyoming searches following the same pattern as Colorado searches (capture on page load, search results, pagination, and errors).
- **FR-014**: The system MUST handle Wyoming-specific error conditions: ASP.NET postback failures and site unavailability, mapping them to appropriate error responses.
- **FR-017**: When the Wyoming site presents a CAPTCHA challenge, the system MUST capture a screenshot of the CAPTCHA image, send it as a base64-encoded HTTP POST request to the FAB (Foundations and Beyond) "Generic Document Processing Agent", extract the solved text from the structured JSON response, submit it on the page, and proceed with the search flow. If the CAPTCHA answer is rejected, the system MUST return an error indicating CAPTCHA resolution failed. The FAB agent endpoint URL and authentication token MUST be configurable via appsettings/Key Vault.
- **FR-015**: The system MUST define scraping operations using semantic intent (navigate to search, submit query, extract results, advance page) to decouple the orchestration logic from state-specific DOM structures.
- **FR-016**: The system MUST enforce concurrency limits using one semaphore per state, each allowing 1 concurrent request. Colorado and Wyoming searches may execute simultaneously, but two concurrent searches for the same state MUST be serialized. Excess same-state requests receive a 503 with Retry-After.

### Key Entities

- **BusinessEntityResult (Normalized)**: Represents a single business entity search result with common fields (name, identifier, status, entity type, formation date, details URL, state) and optional state-specific extensions (event for CO; standing tax, standing RA for WY).
- **SearchResponse (Normalized)**: The response envelope containing the results array, pagination metadata (total count, site total count, truncated, pages scraped), the original search term, and the state code.
- **SearchAuditEntry (Extended)**: The audit trail record extended to include the state code for each search, preserving all existing fields (search term, result count, truncated, pages scraped, duration, status, error detail, correlation ID, diagnostics URL).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can search for Wyoming business entities and receive results within the same response time expectations as Colorado searches (results returned within 30 seconds for typical queries).
- **SC-002**: The normalized response schema is consistent across states — consumers can deserialize CO and WY responses using the same model without conditional logic.
- **SC-003**: 100% of existing Colorado search test scenarios continue to pass after the API reorganization.
- **SC-004**: Wyoming search results match the data displayed on the Wyoming Secretary of State website for the same query (verified via end-to-end tests with known entities).
- **SC-005**: The system correctly paginates Wyoming results, collecting all pages up to the configured limit, with the total count matching the site-reported count.
- **SC-006**: Unsupported state codes and missing parameters are rejected with clear error messages, achieving 100% coverage of invalid input scenarios.
- **SC-007**: Adding a hypothetical third state requires only implementing a new scraper (no changes to the handler, endpoint, or response models).

## Clarifications

### Session 2026-03-25

- Q: When the WY site presents a CAPTCHA, which strategy should the scraper use? → A: Always use the FAB AI agent to solve the CAPTCHA (screenshot the challenge image, send to FAB AI agent, submit the returned answer).
- Q: What should happen to the old `/api/v1/business-search` (CO-only) endpoint? → A: Remove it entirely; consumers must use the new normalized endpoint with `state=CO`.
- Q: How should concurrency be structured for multi-state searches? → A: One semaphore per state, each allowing 1 concurrent request. CO and WY can run simultaneously, but not two COs or two WYs.
- Q: How should the scraper communicate with the FAB AI agent for CAPTCHA solving? → A: HTTP POST with base64-encoded screenshot to the FAB "Generic Document Processing Agent". The agent is configured on the FAB dashboard with system prompt, user prompt, AI params, structured JSON output, and schema. Endpoint URL and auth token stored in appsettings/Key Vault.
- Q: What should the new normalized multi-state search endpoint path be? → A: Reuse the existing path `/api/v1/business-search` and add a mandatory `state` query parameter (e.g., `/api/v1/business-search?state=CO&name=Wendy`).

## Assumptions

- The Wyoming Secretary of State website (`https://wyobiz.wyo.gov/Business/FilingSearch.aspx`) remains publicly accessible and does not require authentication beyond the initial CAPTCHA challenge.
- The CAPTCHA/bot-detection on the Wyoming site will be solved at runtime by sending a screenshot of the challenge to the FAB (Foundations and Beyond) "Generic Document Processing Agent" via HTTP POST with the image base64-encoded. The FAB agent is pre-configured on the FAB dashboard with the appropriate system prompt, user prompt, AI parameters, and structured JSON output schema. The FAB agent endpoint is available and reliable during scraping operations. Manual FAB dashboard configuration is a prerequisite (see `specs/005-multi-state-search/fab-captcha-agent-config.md`).
- The Wyoming site's ASP.NET WebForms postback mechanism for pagination remains stable and functional when driven by browser automation.
- The existing Azure Playwright workspace has sufficient capacity to handle concurrent searches across both states.
- The "Contains" vs. "Starts With" radio option on the Wyoming search form defaults to "Starts With", matching the behavior description; the system will use "Starts With" as the default search mode.
- The existing Colorado search endpoint (`/api/v1/business-search`) will be removed and replaced by the new normalized endpoint that requires a `state` parameter. Consumers must migrate to the new endpoint using `state=CO` for Colorado searches. There is no backward-compatible wrapper or deprecation period.
- Wyoming filing details URLs (`FilingDetails.aspx?eFNum=...`) are relative to the Wyoming site base URL and will be resolved to absolute URLs in the response.
- The Wyoming entity type (e.g., "LLC", "CORP") is parsed from the first line of each result which follows the pattern `"{Name} - {FilingNumber} ({EntityType})"`.
- The audit trail schema will be extended with a state column; existing audit entries from before this feature will have a null or "CO" state value.
