# Feature Specification: Iowa Business Entity Search Integration

**Feature Branch**: `022-iowa-entity-search`  
**Created**: 2026-04-01  
**Status**: Draft  
**Input**: User description: "I want to integrate Iowa business entity search. Integrate it with the unified search as in CO and WY currently. Search Url: https://sos.iowa.gov/search/business/Search.aspx. Enter business name and click on search. Support drill down using entity details. You have access to playwright mcp server to discover. Ignore TLS errors locally."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Iowa Business Entity Search via Execute-Script (Priority: P1)

As an API consumer, I want to invoke Iowa business entity searches through the v2 `/api/v2/execute-script` API with script name `us-ia-business-search`, so that I can retrieve filing information from the Iowa Secretary of State in the same unified format as other state searches (CO, WY).

**Why this priority**: This is the core new capability — without Iowa search, the v2 execute-script multi-state feature lacks complete Midwest coverage. It delivers immediate value by extending geographic reach while establishing IA as the first v2-native state search (CO/WY migration to follow).

**Independent Test**: Can be fully tested by invoking execute-script with `{definition: "us-ia-business-search", parameters: {searchTerm: "Apple"}}` and verifying the response contains Iowa business entity results in the normalized v2 format.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a consumer invokes execute-script with definition `us-ia-business-search` and parameters `{searchTerm: "Apple"}`, **Then** the system returns a normalized response containing Iowa business entity results with business number, name, status, type, legal name, and unique key for each result.
2. **Given** an Iowa search returns multiple pages of results, **When** the execute-script operation processes the search, **Then** it paginates through results up to the configured page limit and aggregates all entities into a single response.
3. **Given** an Iowa search returns zero results, **When** the execute-script operation processes the search, **Then** it returns an empty result list with a total count of zero.
4. **Given** the Iowa Secretary of State site is unreachable or returns an error, **When** execute-script attempts to search, **Then** it returns an appropriate error response with diagnostics indicating the upstream service is unavailable.

---

### User Story 2 - Unified v2 Execute-Script API (Priority: P1)

As an API consumer, I want to invoke Iowa business entity search and detail operations through the same v2 execute-script API endpoint used for all states (CO, WY, IA), so that I can use a consistent, schema-driven interface across all supported states and future state additions.

**Why this priority**: Using the unified v2 execute-script pattern ensures CO and WY migrate off the deprecated v1 API, establishes a consistent pattern for all states, and enables schema-driven state management without endpoint proliferation.

**Independent Test**: Can be verified by invoking `/api/v2/execute-script` with script definitions `us-ia-business-search` (for search) and extracting results in the same format as `us-co-business-search` and `us-wy-entity-details`.

**Acceptance Scenarios**:

1. **Given** a consumer sends an execute-script request with definition `us-ia-business-search` and parameters `{searchTerm: "Wendy"}`, **Then** the system returns a normalized response containing Iowa business entity results.
2. **Given** consumers invoke `us-co-business-search`, `us-wy-entity-details`, and `us-ia-business-search` sequentially, **When** all requests complete successfully, **Then** each response can be deserialized using the same response model without branching logic.
3. **Given** a consumer sends an execute-script request with an unsupported or misspelled script name, **When** the system processes the request, **Then** it returns a 400 Bad Request error with a descriptive message indicating the script is not supported or undefined.

---

### User Story 3 - Iowa Entity Details Drill-Down via Execute-Script (Priority: P1)

As an API consumer, I want to invoke the same v2 execute-script API with a `uniqueKey` parameter from an Iowa search result to retrieve comprehensive entity detail information, so that I can drill down into full entity records using the same unified API interface without page navigation.

**Why this priority**: Entity details are fundamental to the feature. Without drill-down capability via execute-script, the search results are incomplete for users who need comprehensive business information like agent contacts, addresses, and filing dates.

**Independent Test**: Can be verified by executing an Iowa search via `us-ia-business-search`, extracting the `uniqueKey` from a result, and invoking execute-script again with that key to retrieve the full entity detail (names list, agent, principal office, dates, etc.).

**Acceptance Scenarios**:

1. **Given** an Iowa search returns results with `uniqueKey` values, **When** execute-script is invoked with definition `us-ia-entity-details` and `uniqueKey` parameter, **Then** the system returns the full entity profile (business number, legal name, status, type, state of incorporation, effective/filing/expiration dates, names list, registered agent contact, principal office address, chapter/code, certificate note) without forcing page navigation.
2. **Given** multiple results from an Iowa search, **When** each `uniqueKey` is used independently with execute-script, **Then** each returns the correct entity's detail record (no cross-linking or data corruption).

---

### Edge Cases

- What happens when the Iowa site returns an HTTP error (403, 404, 500) during search or pagination? The system should detect the error, capture diagnostics, and return an appropriate error response to the consumer.
- What happens when the Iowa site's HTML markup structure changes unexpectedly? The system should log parse errors and include diagnostic information in the response to enable rapid troubleshooting.
- What happens when the search term contains special characters (e.g., `&`, `'`, `"`, `/`) that may be handled differently by Iowa's form submission? The system should properly escape and submit the term without corrupting the search.
- What happens when the Iowa site returns results with missing or null values in fields (e.g., no Legal Name, no Type)? The system should return null/empty for missing fields rather than failing the entire response.
- What happens when concurrent Iowa searches are attempted? The system should enforce the per-state concurrency limit (1 concurrent IA request) and reject excess requests with a 503 Retry-After response.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The v2 execute-script API (`/api/v2/execute-script`) MUST support a state-specific script definition named `us-ia-business-search` that accepts canonical request shape `{definition: "us-ia-business-search", parameters: {searchTerm: "<search term>"}}` to perform Iowa business entity searches. CO/WY migration to v2 is out of scope for this feature and tracked separately.
- **FR-002**: Iowa search operations MUST require `parameters.searchTerm` as the only mandatory search input key.
- **FR-003**: For Iowa searches, the `us-ia-business-search` script MUST navigate to the Iowa Secretary of State business search page (https://sos.iowa.gov/search/business/Search.aspx), enter the search term in the Business Name field, and submit the search using Playwright browser automation.
- **FR-004**: For Iowa searches, the `us-ia-business-search` script MUST extract the following fields from each result table row: business number, name, status (Active/Inactive), type (Legal/Fictitious name), legal name, and a details URL link (encoded as `uniqueKey` for drill-down via execute-script).
- **FR-005**: For Iowa searches, the `us-ia-business-search` script MUST handle multi-page results by iterating through pagination links up to a configurable page limit (default: all available pages).
- **FR-006**: The response JSON returned for Iowa searches via `us-ia-business-search` script MUST conform to the same normalized schema as CO and WY execute-script responses, with common fields: `identifier` (Iowa business number), `name`, `status` (Active/Inactive mapped to normalized status), `entityType` (Legal/Fictitious name), `formationDate` (mapped from Iowa filing/effective date), `uniqueKey` (base64url-encoded drill-down reference), and `state` (set to "IA"). Iowa-specific fields: `legalName`, `searchResultType`.
- **FR-007**: The response JSON for Iowa entity detail drill-down (via `us-ia-entity-details` with `uniqueKey` parameter) MUST return extended fields: business number, legal name, status, type, state of incorporation, modified flag, expiration date, effective date, filing date, chapter/code, names list (with type, status, modified flag), registered agent (full name, address, city/state/zip), principal office (full name, address, city/state/zip).
- **FR-008**: The response JSON for entity details MUST include a `certificateNote` field set to: "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)" to transparently document the limitation.
- **FR-009**: The response envelope for both search and detail operations MUST include canonical fields: `definition`, `correlationId`, `truncated`, and `data`. Search operations MAY additionally include metadata fields `totalCount`, `siteTotalCount`, `pagesScraped`, `searchTerm`, `state`, and `executionTimeMs` where applicable.
- **FR-010**: The system MUST record the script name (`us-ia-business-search` or `us-ia-entity-details`) in the audit trail entry for every Iowa operation, following the same audit format as execute-script operations.
- **FR-011**: The system MUST provide diagnostic screenshots and tracing for Iowa operations via execute-script, following the same pattern as other states: capture on page load, search results, pagination, detail pages, and errors.
- **FR-012**: The system MUST enforce a concurrency limit for Iowa operations using a per-state semaphore allowing 1 concurrent IA request. Excess concurrent IA requests receive a 503 Service Unavailable response with Retry-After header.
- **FR-013**: For local development, the system MUST ignore TLS certificate errors when connecting to the Iowa SOS website to support testing on systems with certificate validation issues.

### Key Entities *(include if feature involves data)*

- **IowaBusinessSearchResult (Normalized)**: Represents a single Iowa business entity search result conforming to the v2 execute-script normalized schema with common fields (identifier/business number, name, status, entity type, formation date, unique key, state) and Iowa-specific extensions (legal name, search result type).
- **IowaEntityDetail (Extended)**: Represents comprehensive Iowa entity detail information returned by drill-down operation, including summary fields above plus names list, registered agent contact, principal office address, state of incorporation, modified flag, filing/effective/expiration dates, chapter/code, and certificate availability note.
- **IowaSearchResponse (Envelope)**: The response envelope for Iowa operations containing canonical fields (`definition`, `correlationId`, `truncated`, `data`) plus search metadata when applicable (`totalCount`, `siteTotalCount`, `pagesScraped`, `searchTerm`, `state`, `executionTimeMs`) and diagnostics.
- **IowaExecuteScriptAuditEntry (Audit)**: The audit trail record for Iowa execute-script operations including script name (`us-ia-business-search` or `us-ia-entity-details`), parameters, result count, truncation flag, pages scraped, duration, status, error detail, correlation ID, and diagnostics URL.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Consumers can invoke `/api/v2/execute-script` with script name `us-ia-business-search` and receive Iowa results within the same response time expectations as Colorado and Wyoming execute-script calls (results returned within 30 seconds for typical queries).
- **SC-002**: The normalized response schema returned by `us-ia-business-search`, `us-co-business-search`, and `us-wy-entity-details` execute-script calls is identical — consumers can deserialize all three state responses using the same model without any conditional logic.
- **SC-003**: Iowa entity detail drill-down via execute-script with `uniqueKey` parameter returns all extended fields (names list, agent details, principal office, dates, chapter/code, certificate note) correctly extracted from Iowa SOS website.
- **SC-004**: Iowa search results match the data displayed on the Iowa Secretary of State website for the same query when verified via end-to-end tests with known business entities.
- **SC-005**: The system correctly paginates Iowa search results when available, collecting all pages up to the configured limit, with the total count reflecting the actual number of entities returned.
- **SC-006**: Iowa entity detail drill-down correctly navigates to and extracts the specific entity's profile information from the Iowa SOS website, with 100% accuracy across a sample of tested results.
- **SC-007**: Concurrent Iowa execute-script requests are properly serialized per the 1-concurrent-request-per-state semaphore, with excess requests receiving 503 responses and no cross-request interference.
- **SC-008**: The `certificateNote` field correctly documents that Iowa certificates are unavailable via this API, with transparent message indicating paywall limitation.
- **SC-009**: Adding a hypothetical fourth state requires only implementing a new script definition and scraper (no changes to the execute-script handler or core response models).

## Clarifications

### Session 2026-04-01

- **Q: Should Iowa searches use v1 `/api/v1/business-search` endpoint or v2 `/api/v2/execute-script`?** → **A**: Use v2 execute-script. All state searches (CO, WY, IA) MUST migrate to v2 with state-specific script definitions (`us-co-business-search`, `us-wy-entity-details`, `us-ia-business-search`). v1 API is deprecated.
- **Q: What entity detail fields should Iowa drill-down return?** → **A**: Extended set including summary info, names list, registered agent details, principal office address, dates (filing, effective, expiration), chapter/code, and certificate availability note.
- **Q: How should Iowa's paywall-protected Certificate of Existence be handled?** → **A**: Document as unavailable in response with note: "Certificate of Existence available via Iowa SOS website (paywall - not available via this API)". Out-of-scope for v1.

## Assumptions

- Iowa Secretary of State website maintains the current search page structure (https://sos.iowa.gov/search/business/Search.aspx) and entity detail page format for the duration of this feature's implementation.
- Iowa search is limited to "search by business name" functionality (not "search by business number") to align with the current feature scope.
- Pagination on Iowa results is optional/non-mandatory; many searches will return a single page of results. The system should gracefully handle both paginated and single-page result sets.
- Iowa entity "Legal Name" may be present in both search results and detail pages. When present, it represents the formal legal entity name and should be captured as a distinct field.
- The normalized response schema used for v2 execute-script is flexible enough to accommodate Iowa's field structure without modification.
- Diagnostics and screenshot capture for Iowa operations follow the same infrastructure as other states (Azure Blob Storage for artifacts, correlation IDs for tracing).
- v1 API deprecation and CO/WY migration to v2 are tracked in separate follow-on work; this feature establishes IA as the first v2-native state search capability.
- TLS certificate errors are expected when testing against Iowa SOS locally; the system MUST tolerate and ignore these errors.
