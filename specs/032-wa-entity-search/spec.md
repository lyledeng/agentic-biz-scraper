# Feature Specification: Washington Business Entity Search Integration

**Feature Branch**: `032-wa-entity-search`  
**Created**: 2026-04-05  
**Status**: Ready  
**Input**: User description: "Add WA Business Entity Searching and Details capability. Url: https://ccfs.sos.wa.gov/#/"

## Clarifications

### Session 2026-04-05

- Q: Should there be a limit on how many filing documents are downloaded per entity-details request? → A: Download documents for only the N most recent filings, configured to 10 initially.
- Q: Should WA definitions use remote (Azure Playwright Workspaces) or local (Chrome) browser mode? → A: Remote mode, consistent with MO. Azure IPs reduce Cloudflare fingerprinting risk.
- Q: Should entity detail response include nonprofit-specific fields (Charitable Corporation, Nonprofit EIN, Gross Revenue <$500K, Has Members, Public Benefit Designation, Host Home)? → A: Yes, include them. They are already on the page and valuable for nonprofit due diligence.
- Q: Should filing document retrieval click through modal dialogs or use page-level fetch to call the document list API? → A: Use page-level fetch() to call GetTransactionDocumentsList API directly. Avoids fragile modal interaction and matches MO filing download pattern.
- Q: Should the search definition accept a UBI number parameter in addition to business name search? → A: Name search only for initial implementation. UBI search can be added later if needed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Washington Business Entity Search (Priority: P1)

As an API consumer, I want to search for Washington business entities through the v2 `/api/v2/execute-script` API with definition `us-wa-business-search`, so that I can retrieve business filing information from the Washington Secretary of State Corporations and Charities Filing System (CCFS) in the same unified format as other state searches (CO, WY, IA, MO).

**Why this priority**: This is the core new capability — without Washington search, users cannot discover WA business entities. It delivers immediate value by extending geographic coverage to Washington and follows the established v2 execute-script pattern.

**Independent Test**: Can be fully tested by invoking execute-script with `{definition: "us-wa-business-search", parameters: {searchTerm: "Starbucks"}}` and verifying the response contains Washington business entity results in the normalized v2 format.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a consumer invokes execute-script with definition `us-wa-business-search` and parameters `{searchTerm: "Starbucks"}`, **Then** the system returns a normalized response containing Washington business entity results with business name, UBI number, business type, principal office address, registered agent name, status, and unique key for each result.
2. **Given** a Washington search returns multiple pages of results, **When** the execute-script operation processes the search, **Then** it paginates through results up to the configured page limit and aggregates all entities into a single response.
3. **Given** a Washington search returns zero results, **When** the execute-script operation processes the search, **Then** it returns an empty result list with a total count of zero.
4. **Given** the Washington CCFS site is unreachable or returns an error, **When** execute-script attempts to search, **Then** it returns an appropriate error response with diagnostics indicating the upstream service is unavailable.

---

### User Story 2 - Washington Entity Detail Drill-Down (Priority: P1)

As an API consumer, I want to retrieve comprehensive entity details for a Washington business entity by invoking the v2 execute-script API with definition `us-wa-entity-details` and a `uniqueKey` from search results, so that I can access the full entity profile without manual page navigation.

**Why this priority**: Entity details are fundamental to the feature. Without drill-down capability, the search results are incomplete for users who need comprehensive business information such as registered agent contact, principal office address, governors/officers, filing history, and name history.

**Independent Test**: Can be verified by executing a Washington search via `us-wa-business-search`, extracting the `uniqueKey` from a result, and invoking execute-script with definition `us-wa-entity-details` and that key to retrieve the full entity detail.

**Acceptance Scenarios**:

1. **Given** a Washington search returns results with `uniqueKey` values, **When** execute-script is invoked with definition `us-wa-entity-details` and a `uniqueKey` parameter, **Then** the system returns the full entity profile including: business name, UBI number, business type, business status, principal office street and mailing addresses, expiration date, jurisdiction, formation/registration date, period of duration, inactive date, nature of business, registered agent (name, street address, mailing address), and governors/officers (title, type, entity name, first name, last name).
2. **Given** a `uniqueKey` for a dissolved, terminated, or inactive entity, **When** execute-script is invoked with `us-wa-entity-details`, **Then** the system still returns the full entity detail record with the correct status and inactive date reflected.
3. **Given** an invalid or non-existent `uniqueKey`, **When** execute-script is invoked with `us-wa-entity-details`, **Then** the system returns an appropriate error response indicating the entity was not found.

---

### User Story 3 - Washington Filing History with Document Downloads (Priority: P2)

As an API consumer, I want the entity detail response to include the complete filing history with downloadable document links, so that I can access the filed documents (annual reports, amendments, mergers, etc.) through proxy URLs.

**Why this priority**: Filing documents are valuable for compliance and due diligence research. This depends on entity details working first and follows the established filing document download pattern (WY history documents, MO filings).

**Independent Test**: Can be verified by requesting entity details for an active entity with filing history (e.g., Starbucks Corporation), and confirming the response includes filing records with proxy URLs that serve the actual document content.

**Acceptance Scenarios**:

1. **Given** a Washington entity with filing history, **When** execute-script returns the entity detail, **Then** the filing history includes filing number, filing date/time, effective date, filing type, and for each filing that has documents: a list of downloadable documents with document type, created date, storage proxy URL, and file name.
2. **Given** a filing document download fails for one filing, **When** the system processes filing documents, **Then** it continues processing remaining filings and records a per-document error field (not failing the entire request).
3. **Given** an entity with no filing history, **When** execute-script returns the entity detail, **Then** the filing history section is an empty collection rather than an error.

---

### User Story 4 - Washington Search Appears in Test UI (Priority: P2)

As a Test UI user, I want Washington to appear as an available state in the Test UI search interface, so that I can search for and drill into Washington business entities using the same visual interface used for other states.

**Why this priority**: The Test UI provides an interactive way to validate and demonstrate the Washington integration. It depends on the search and detail capabilities being implemented first.

**Independent Test**: Can be verified by opening the Test UI, selecting Washington from the state dropdown, entering a search term, and confirming results render in the standard result table with drill-down links.

**Acceptance Scenarios**:

1. **Given** the Test UI is loaded, **When** the user selects Washington from the state dropdown, **Then** Washington appears as an option and the search form is displayed.
2. **Given** the user performs a Washington search in the Test UI, **When** results are returned, **Then** they render in the standard results table showing business name, UBI number, type, status, address, and registered agent.
3. **Given** the user clicks on a Washington search result in the Test UI, **When** the detail view loads, **Then** it displays general information, governors/officers, filing history, and registered agent data for the selected entity.

---

### Edge Cases

- What happens when the CCFS AngularJS SPA fails to load data from its backend API (`ccfs-api.prod.sos.wa.gov`)? The system should detect the missing data and return an appropriate error with diagnostics.
- What happens when the search term contains special characters (ampersands, periods, apostrophes)? The system should properly pass these through the search input field.
- What happens when Cloudflare Turnstile challenge is triggered on the CCFS site? The system should report the block in diagnostics and return an appropriate error to the consumer.
- What happens when a business entity has no governors/officers listed? The system should return an empty governors collection rather than erroring.
- What happens when the filing history has no documents associated with a filing (Action column is empty)? The system should return the filing metadata without document links.
- What happens when the entity detail page fields are partially populated (e.g., no mailing address, no nature of business)? The system should return null for unpopulated fields rather than erroring.
- What happens when the search returns a very large number of results with multiple pages? The system should paginate up to the configured page limit and set `truncated: true` if more pages exist.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a new scraping definition `us-wa-business-search` that accepts a `searchTerm` parameter and returns normalized search results from the Washington CCFS Corporation Search.
- **FR-002**: System MUST support a new scraping definition `us-wa-entity-details` that accepts a `uniqueKey` parameter (base64url-encoded JSON containing the WA business ID and business type) and returns detailed entity information including filing history and document downloads.
- **FR-003**: Search results MUST include for each entity: business name, UBI number, business type (e.g., "WA PROFIT CORPORATION", "FOREIGN LIMITED LIABILITY COMPANY"), principal office address, registered agent name, status (e.g., "ACTIVE", "TERMINATED", "ADMINISTRATIVELY DISSOLVED"), and unique key for drill-down. Note: `formationDate` is not available in WA CCFS search results and MUST be set to `null` in the normalized output.
- **FR-004**: Entity detail results MUST include: business name, UBI number, business type, business status, principal office street address, principal office mailing address, expiration date, jurisdiction, formation/registration date, period of duration, inactive date, nature of business, and nonprofit-specific fields where applicable: charitable corporation (boolean), nonprofit EIN, most recent gross revenue under $500K (boolean), has members (boolean), public benefit designation (boolean), and host home (boolean).
- **FR-005**: Entity detail results MUST include registered agent information: agent name, street address, and mailing address.
- **FR-006**: Entity detail results MUST include governors/officers with: title (e.g., "GOVERNOR"), governors type (e.g., "INDIVIDUAL", "ENTITY"), entity name (if applicable), first name, and last name.
- **FR-007**: Entity detail results MUST include filing history with: filing number, filing date/time, effective date, and filing type (e.g., "ANNUAL REPORT", "MERGER", "STATEMENT OF CHANGE").
- **FR-008**: For each of the N most recent filings that have associated documents (where N defaults to 10 and is configurable), the system MUST retrieve the document list (document type, created date) and download each document file, upload it to cloud storage, and return a proxy URL — consistent with the WY history document and MO filing document handling pattern. Filings are processed in the order returned by the CCFS `GetBusinessFilingList` API (reverse chronological by filing date, based on observed behavior); filings beyond the limit are included in metadata only (no document download). Document list retrieval MUST use page-level `fetch()` to call the CCFS `GetTransactionDocumentsList` API directly within the page session context (avoiding fragile modal dialog interaction), and document downloads MUST similarly use page-level `fetch()` to the `DownloadOnlineFilesByNumber` endpoint to preserve session cookies.
- **FR-009**: The WA CCFS site is a single-page AngularJS application backed by a JSON REST API at `ccfs-api.prod.sos.wa.gov`. The scraping definition MUST interact with the SPA's UI elements (search input, buttons, navigation) rather than calling the backend API directly, to maintain consistency with the project's browser-automation approach.
- **FR-010**: The search operation MUST handle the SPA's pagination controls to retrieve multiple pages of results when available.
- **FR-011**: The `uniqueKey` for each search result MUST encode the entity's internal business ID and business type (both needed for the `showBusineInfo(businessID, businessType)` navigation) following the existing base64url-encoded JSON convention used by other states.
- **FR-012**: The Washington definitions MUST be registered and available through the `/api/v2/definitions` endpoint.
- **FR-013**: When downloading filing documents, individual download failures MUST NOT fail the entire entity detail request. Each filing document result MUST include `storageUrl`, `fileName`, and `error` fields, following the same per-document error pattern as WY history documents and MO filings. On success, `error` is null and `storageUrl` contains the proxy URL. On failure, `error` contains a description and `storageUrl` is null.
- **FR-014**: Name History retrieval is out of scope for the initial implementation but may be added in a future iteration.

### Key Entities

- **Search Result**: A Washington business entity returned from a name or UBI search — includes business name, UBI number, business type, principal office address, registered agent name, status, and unique key for drill-down.
- **Entity Detail**: The full profile of a Washington business entity — includes general information (name, UBI, type, status, addresses, expiration date, jurisdiction, formation date, duration, inactive date, nature of business), registered agent (name, street and mailing addresses), and governors/officers list.
- **Governor/Officer**: A person or entity associated with a business entity — includes title, governor type (Individual/Entity), entity name, first name, and last name.
- **Filing Record**: A record of a filing event associated with an entity — includes filing number, filing date/time, effective date, and filing type.
- **Filing Document**: A downloadable document associated with a filing — includes document type (e.g., "ANNUAL REPORT - FULFILLED", "CONFIRMATION LETTER"), created date, storageUrl (proxy URL to downloaded document in cloud storage), fileName, and error (null on success, description on failure).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can search for Washington business entities and receive results within 15 seconds for a typical search term.
- **SC-002**: Users can retrieve full entity details (including governors and filing history) for a Washington entity within 30 seconds.
- **SC-003**: Filing documents are successfully downloaded and available via proxy URL for at least 90% of filings that have associated documents.
- **SC-004**: Search results for well-known Washington businesses (e.g., "Starbucks", "Microsoft") return accurate, complete data matching the information displayed on the CCFS website.
- **SC-005**: The Washington integration appears in the Test UI state dropdown and end-to-end search-to-detail workflow completes successfully.

## Assumptions

- The WA CCFS site (`ccfs.sos.wa.gov`) is publicly accessible without authentication for business entity search and detail viewing.
- The WA CCFS site uses Cloudflare Turnstile for bot protection; the system's existing browser automation approach (with stealth configuration) is sufficient to pass these challenges without CAPTCHAs.
- The WA CCFS AngularJS SPA renders data client-side after JSON API calls, requiring the scraping engine to wait for data to load after navigation actions.
- Document downloads from the CCFS site use the `Common/DownloadOnlineFilesByNumber` endpoint and return PDF files accessible within the browser session context.
- The WA CCFS search returns results paginated with 25 results per page (based on observed behavior).
- The filing history API returns all filings without pagination (observed 89 filings for Starbucks Corporation loaded in a single view).
- The existing unified entity detail response format and v2 execute-script pattern accommodate the WA-specific fields (governors, UBI number, jurisdiction, nature of business).
- Remote browser mode (Azure Playwright Workspaces) will be used for WA definitions, consistent with the MO approach for SPA-based sites.
