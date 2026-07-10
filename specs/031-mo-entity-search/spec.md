# Feature Specification: Missouri Business Entity Search Integration

**Feature Branch**: `031-mo-entity-search`  
**Created**: 2026-04-04  
**Status**: Draft  
**Input**: User description: "Add MO Business Entity Search. Url https://bsd.sos.mo.gov/BusinessEntity/BESearch.aspx?SearchType=0. Use Playwright MCP server running as chrome browser to discover. Certificate of Good Standing is behind a paywall, so just entity search and detail is required."

## Clarifications

### Session 2026-04-04

- Q: Should the system download filing PDFs to cloud storage (like WY history docs), or just return metadata? → A: Download filing PDFs to cloud storage and return proxy URLs.
- Q: Should MO use remote (Azure Playwright Workspaces) or local (Chrome) browser mode? → A: Remote. Anti-bot scan confirmed no CAPTCHA, no Cloudflare challenge, no bot-detection SDKs on the MO SOS site.
- Q: How should previous/former business names appear in the normalized search response? → A: Include as a `previousNames` string array nested inside each parent entity result object.
- Q: How should individual filing document download failures be handled? → A: Continue processing remaining filings; record per-document error field on each filing result (same as WY `historyDocuments[].error` pattern).
- Q: How should truncation be signaled when the MO site caps results at 1,051? → A: Use the existing `truncated: true` boolean field in the unified v2 response format.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Missouri Business Entity Search (Priority: P1)

As an API consumer, I want to search for Missouri business entities through the v2 `/api/v2/execute-script` API with definition `us-mo-business-search`, so that I can retrieve business filing information from the Missouri Secretary of State in the same unified format as other state searches (CO, WY, IA).

**Why this priority**: This is the core new capability — without Missouri search, users cannot discover MO business entities. It delivers immediate value by extending geographic coverage to Missouri and follows the established v2 execute-script pattern.

**Independent Test**: Can be fully tested by invoking execute-script with `{definition: "us-mo-business-search", parameters: {searchTerm: "Smith"}}` and verifying the response contains Missouri business entity results in the normalized v2 format.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a consumer invokes execute-script with definition `us-mo-business-search` and parameters `{searchTerm: "Smith"}`, **Then** the system returns a normalized response containing Missouri business entity results with business name, charter number, entity type, status, creation date, registered agent name, and unique key for each result.
2. **Given** a Missouri search returns more than 20 results (one page), **When** the execute-script operation processes the search, **Then** it paginates through results up to the configured page limit and aggregates all entities into a single response.
3. **Given** a Missouri search returns zero results, **When** the execute-script operation processes the search, **Then** it returns an empty result list with a total count of zero.
4. **Given** the Missouri Secretary of State site is unreachable or returns an error, **When** execute-script attempts to search, **Then** it returns an appropriate error response with diagnostics indicating the upstream service is unavailable.

---

### User Story 2 - Missouri Entity Detail Drill-Down (Priority: P1)

As an API consumer, I want to retrieve comprehensive entity details for a Missouri business entity by invoking the v2 execute-script API with definition `us-mo-entity-details` and a `uniqueKey` from search results, so that I can access the full entity profile without manual page navigation.

**Why this priority**: Entity details are fundamental to the feature. Without drill-down capability, the search results are incomplete for users who need comprehensive business information such as registered agent contact, principal office address, filed documents, and entity status.

**Independent Test**: Can be verified by executing a Missouri search via `us-mo-business-search`, extracting the `uniqueKey` from a result, and invoking execute-script with definition `us-mo-entity-details` and that key to retrieve the full entity detail.

**Acceptance Scenarios**:

1. **Given** a Missouri search returns results with `uniqueKey` values, **When** execute-script is invoked with definition `us-mo-entity-details` and a `uniqueKey` parameter, **Then** the system returns the full entity profile including: business name, entity type, charter number, domesticity, home state, registered agent (name and address), status, date formed, duration, filed documents (action, type, date filed, effective date), and principal office addresses (type, address, since date).
2. **Given** a `uniqueKey` for a dissolved or inactive entity, **When** execute-script is invoked with `us-mo-entity-details`, **Then** the system still returns the full entity detail record with the correct status reflected.
3. **Given** an invalid or non-existent `uniqueKey`, **When** execute-script is invoked with `us-mo-entity-details`, **Then** the system returns an appropriate error response indicating the entity was not found.

---

### User Story 3 - Missouri Search Appears in Test UI (Priority: P2)

As a Test UI user, I want Missouri to appear as an available state in the Test UI search interface, so that I can search for and drill into Missouri business entities using the same visual interface used for other states.

**Why this priority**: The Test UI provides an interactive way to validate and demonstrate the Missouri integration. It depends on the search and detail capabilities being implemented first.

**Independent Test**: Can be verified by opening the Test UI, selecting Missouri from the state dropdown, entering a search term, and confirming results render in the standard result table with drill-down links.

**Acceptance Scenarios**:

1. **Given** the Test UI is loaded, **When** the user selects Missouri from the state dropdown, **Then** Missouri appears as an option and the search form is displayed.
2. **Given** the user performs a Missouri search in the Test UI, **When** results are returned, **Then** they render in the standard results table showing business name, charter number, type, status, creation date, and registered agent.
3. **Given** the user clicks on a Missouri search result in the Test UI, **When** the detail view loads, **Then** it displays general information, filings, and principal office address data for the selected entity.

---

### Edge Cases

- What happens when the Missouri site returns more than 1,051 results (the site's display cap)? The system should return whatever results are available (up to the cap) and set `truncated: true` in the v2 response format.
- What happens when the search term contains special characters (ampersands, periods, apostrophes)? The system should properly encode and submit these characters in the ASP.NET postback form.
- What happens when a business entity has previous names listed alongside the primary result? The system should associate previous names with the correct parent entity rather than treating them as separate results.
- What happens when the detail page has no filings or no principal office address records? The system should return empty collections for those sections rather than erroring.
- What happens when the Missouri site requires a CAPTCHA or blocks automated access? The system should report the block in diagnostics and return an appropriate error to the consumer.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST support a new scraping definition `us-mo-business-search` that accepts a `searchTerm` parameter and returns normalized search results from the Missouri Secretary of State Business Entity Search page.
- **FR-002**: System MUST support a new scraping definition `us-mo-entity-details` that accepts a `uniqueKey` parameter (base64url-encoded JSON containing the MO entity ID) and returns detailed entity information.
- **FR-003**: Search results MUST include for each entity: business name, charter number, entity type (e.g., "Limited Liability Company (Foreign)"), status (e.g., "Active", "Voluntarily Dissolved", "Admin Diss/Cancel - Report"), creation date, registered agent name, previous names (as a string array), and unique key for drill-down.
- **FR-004**: Entity detail results MUST include: business name, entity type, charter number, domesticity (Domestic/Foreign), home state, registered agent (name and full address), status, date formed, and duration.
- **FR-005**: Entity detail results MUST include filed documents with: action type, filing category (e.g., "Modify", "Creation"), document type description, date filed, and effective date. Each filing document PDF MUST be downloaded from the MO SOS site, uploaded to cloud storage, and returned as a proxy URL (consistent with WY history document handling).
- **FR-006**: Entity detail results MUST include principal office addresses with: address type (e.g., "Reg. Office", "Mailing"), full address, and since date.
- **FR-007**: The search operation MUST handle ASP.NET Web Forms postback mechanics (ViewState, event validation) to submit search queries and paginate through results.
- **FR-008**: The system MUST correctly associate previous business names with their parent entity record by collapsing them into a `previousNames` string array on the parent result, rather than returning them as independent search results.
- **FR-009**: The `uniqueKey` for each search result MUST encode the entity's internal ID (from the detail page URL parameter `ID`) following the existing base64url-encoded JSON convention used by other states.
- **FR-010**: The Missouri definitions MUST be registered and available through the `/api/v2/definitions` endpoint.
- **FR-011**: Certificate of Good Standing and ORDER COPIES/CERTIFICATES functionality is explicitly out of scope as these are behind a paywall.
- **FR-012**: When downloading filing document PDFs, individual download failures MUST NOT fail the entire entity detail request. Each filing document result MUST include `storageUrl`, `fileName`, and `error` fields, following the same per-document error pattern as WY history documents. On success, `error` is null and `storageUrl` contains the proxy URL. On failure, `error` contains a description and `storageUrl` is null.
- **FR-013**: When the MO site indicates results exceed its display cap (1,051), the response MUST set the existing `truncated: true` boolean field in the unified v2 response format, providing a machine-readable truncation signal consistent with other states.

### Key Entities

- **Search Result**: A Missouri business entity returned from a name search — includes business name, charter number, entity type, status, creation date, registered agent name, previous names (string array), and unique key for drill-down.
- **Entity Detail**: The full profile of a Missouri business entity — includes general information (name, type, charter no., domesticity, home state, registered agent with address, status, date formed, duration), filed documents list, and principal office addresses.
- **Filed Document**: A record of a filing event associated with an entity — includes action, filing category, document type description, date filed, effective date, storageUrl (proxy URL to downloaded PDF in cloud storage), fileName, and error (null on success, description on failure).
- **Principal Office Address**: An address record associated with an entity — includes type (Reg. Office, Mailing), full street address with city/state/zip, and effective-since date.
- **Previous Name**: An alternate or former name associated with a business entity, linked to the same entity ID.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can search for Missouri business entities by name and receive results within 15 seconds for a typical query (defined as a search returning ≤100 results across ≤5 pages).
- **SC-002**: Users can retrieve full entity details (general info, filings metadata, principal office address) for any Missouri entity within 10 seconds. Filing PDF downloads to cloud storage are excluded from this SLA as they run per-document and scale linearly with filing count.
- **SC-003**: Missouri search results follow the same normalized response format as existing state searches (CO, WY, IA), requiring no consumer-side branching logic.
- **SC-004**: 100% of search result entries include a working `uniqueKey` that successfully resolves to the correct entity detail record.
- **SC-005**: The Test UI correctly renders Missouri search results and entity details without any Missouri-specific rendering logic.
- **SC-006**: Previous business names are correctly grouped under their parent entity, not returned as separate search results.

## Assumptions

- The Missouri Secretary of State website at `https://bsd.sos.mo.gov/BusinessEntity/BESearch.aspx?SearchType=0` will remain publicly accessible without authentication for search and detail operations.
- The MO site uses ASP.NET Web Forms with postback-based navigation; Playwright browser automation is required (not simple HTTP requests).
- The site's 1,051-result display cap is a hard limit imposed by the source and cannot be bypassed.
- The existing v2 execute-script infrastructure, browser management (local/remote), and definition registration patterns from prior features (CO, WY, IA) will be reused.
- Certificate of Good Standing and any paid services on the MO SOS site are out of scope for this feature.
- Search is by "Business Name" with "Starting With" match type by default; other search modes (Registered Agent, Charter No., Name Availability) and match types (All Words, Any Word, Exact Match) are out of scope for v1.
- The "Show Registration Reports" checkbox on the Filings tab is checked by default; the system will include registration reports in filed documents.
- The per-definition browser mode configuration (feature 030) will be used to configure MO's browser mode to **remote** (Azure Playwright Workspaces), consistent with CO, WY, and DE. Anti-bot scan confirmed no bot-detection measures on the MO SOS site (no CAPTCHA, no Cloudflare challenge, no anti-bot SDKs).
- The existing cloud storage infrastructure (Azure Blob Storage, feature 003) will be reused for uploading MO filing document PDFs, with the same per-correlation-ID folder convention.
