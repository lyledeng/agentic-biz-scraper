# Feature Specification: Wyoming Entity Details Support

**Feature Branch**: `007-wy-entity-details`  
**Created**: 2026-03-25  
**Status**: Draft  
**Input**: User description: "Update the entity details endpoint to support WY entity details. Accept a state code parameter. Scrape entity details from the Wyoming SOS FilingDetails page, including CAPTCHA handling."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve Wyoming Entity Details (Priority: P1)

As an API consumer, I want to retrieve business entity details from the Wyoming Secretary of State by providing a WY details URL and the state code "WY", so that I can view filing information, status, registered agent, and address data for Wyoming entities.

**Why this priority**: This is the core feature — extending the existing entity details endpoint to support a second state. Without it, WY entity details cannot be retrieved at all.

**Independent Test**: Can be fully tested by calling `GET /api/v1/entity-details?detailsUrl={wyUrl}&state=WY` and verifying the response contains the expected entity fields.

**Acceptance Scenarios**:

1. **Given** a valid WY entity details URL (e.g., `https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=...`), **When** the API is called with `state=WY`, **Then** the response contains the entity name, filing ID, type, status, sub-status, initial filing date, standings, term of duration, formed-in jurisdiction, and addresses.
2. **Given** a valid WY entity details URL for an entity with a registered agent, **When** the API is called, **Then** the response includes the registered agent name and address.
3. **Given** a valid WY entity details URL, **When** the WY site presents a CAPTCHA challenge, **Then** the system solves the CAPTCHA using the existing FAB captcha-solver service before scraping the details.
4. **Given** any entity details URL, **When** called without a `state` query parameter, **Then** the system returns 400 Bad Request indicating that `state` is required.

---

### User Story 2 - Normalize Response Across States (Priority: P1)

As an API consumer, I want the response JSON structure to remain consistent regardless of whether the entity is from CO or WY, so that I can process results uniformly without state-specific parsing logic.

**Why this priority**: Consistent response structure is critical for API consumers. Without normalization, consuming applications would need branching logic per state.

**Independent Test**: Can be tested by calling the endpoint for both a CO and a WY entity and verifying that the top-level response fields (Details, RegisteredAgent) follow the same shape, with state-specific fields included where applicable.

**Acceptance Scenarios**:

1. **Given** a CO entity details request, **When** the response is returned, **Then** it has the same top-level structure as a WY response (Details, RegisteredAgent, Certificate, Parties, HistoryDocuments sections).
2. **Given** a WY entity details request, **When** the response includes WY-specific fields (SubStatus, FilingId, Type, StandingTax, StandingRA, StandingOther, TermOfDuration, FormedIn, InactiveDate), **Then** these fields appear as additional nullable properties on the details object.
3. **Given** a CO entity details request, **When** WY-specific fields are not applicable, **Then** those fields are `null` in the response.

---

### User Story 3 - URL Validation for Wyoming (Priority: P2)

As an API operator, I want incoming WY details URLs to be validated against an allowlist, so that the system only navigates to trusted Wyoming SOS URLs and rejects anything else.

**Why this priority**: Security is important but is an incremental addition to the existing URL validation infrastructure.

**Independent Test**: Can be tested by sending malicious or invalid URLs with `state=WY` and verifying the system returns 400 Bad Request.

**Acceptance Scenarios**:

1. **Given** a URL with host `wyobiz.wyo.gov` and path starting with `/Business/FilingDetails.aspx`, **When** called with `state=WY`, **Then** the request is accepted.
2. **Given** a URL with an unauthorized host (e.g., `evil.com`), **When** called with `state=WY`, **Then** the system returns 400 Bad Request.
3. **Given** a non-HTTPS URL, **When** called, **Then** the system returns 400 Bad Request.

---

### User Story 4 - Diagnostics and Audit for WY (Priority: P2)

As an operations engineer, I want WY entity detail scraping to produce the same diagnostics artifacts (screenshots, traces) and audit trail records as CO, so that troubleshooting and compliance are consistent.

**Why this priority**: Diagnostics parity is essential for production supportability but not required for basic functionality.

**Independent Test**: Can be tested by performing a WY entity details scrape and verifying screenshot files, trace files, and audit records exist under the correlation ID.

**Acceptance Scenarios**:

1. **Given** a WY entity details scrape request, **When** the flow executes, **Then** screenshots are captured at key milestones (page load, after CAPTCHA, after details extraction).
2. **Given** a WY entity details scrape, **When** tracing is enabled, **Then** Playwright trace files are stored with the correlation ID.
3. **Given** a WY entity details scrape, **When** the operation completes, **Then** an audit trail record is persisted with correlation ID, state code, and cloud storage URL for diagnostics.

---

### Edge Cases

- What happens when the WY CAPTCHA solver fails all retry attempts? The system returns a 502 Bad Gateway with a descriptive error.
- What happens when a WY entity has no registered agent? The RegisteredAgent section of the response is `null`.
- What happens when a WY entity has no parties? The Parties field is an empty array (`[]`); `null` is reserved for states where parties do not apply (e.g., CO).
- What happens when the WY site is down or unreachable? The system returns 502 Bad Gateway after retry exhaustion.
- What happens when an invalid `state` parameter is provided (e.g., `state=TX`)? The system returns 400 Bad Request with a message listing supported states.
- What happens when a CO details URL is sent with `state=WY` (or vice versa)? URL validation rejects the mismatch.
- What happens when a WY entity has fewer than 3 history documents? The system downloads all available documents (1 or 2) without error.
- What happens when a WY history document download fails? The system logs the failure, continues with remaining documents, and returns partial results with an error note for the failed download.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The entity details endpoint MUST accept a required `state` query parameter (`CO` or `WY`). Callers must always specify the state code explicitly.
- **FR-002**: When `state=WY`, the system MUST navigate to the provided Wyoming SOS FilingDetails URL, solve any CAPTCHA challenge using the existing FAB captcha-solver, and extract entity details.
- **FR-003**: The system MUST extract the following WY entity detail fields: Name, Filing ID, Type, Status, Sub Status, Initial Filing Date, Standing - Tax, Standing - RA, Standing - Other, Inactive Date (if present), Term of Duration, Formed In, Principal Office Address, Mailing Address, Latest AR/Year (if present), AR Exempt, and License Tax Paid.
- **FR-004**: The system MUST extract WY Registered Agent information (name and address) from the "Additional Details" section when present.
- **FR-005**: The system MUST validate WY details URLs against an allowlist: HTTPS scheme, host `wyobiz.wyo.gov`, path starting with `/Business/FilingDetails.aspx`.
- **FR-006**: The response JSON MUST use a normalized structure that works for both CO and WY, with WY-specific fields as nullable additions to the existing details model.
- **FR-007**: The system MUST capture screenshots at key milestones during WY entity detail scraping (page load, CAPTCHA challenge, details extraction).
- **FR-008**: The system MUST persist an audit trail record for each WY entity details request, including the state code.
- **FR-009**: If an unsupported state code is provided, the system MUST return 400 Bad Request.
- **FR-010**: The WY entity details scraper MUST use the existing JSON-driven scraping engine with a new `wy-entity-details.json` flow definition.
- **FR-011**: The Certificate section of the response MUST be `null` for WY entities (WY does not provide an inline certificate download link on the details page).
- **FR-012**: The system MUST download the most recent 3 history documents (filing images) from the WY entity details History section and upload them to the cloud storage account. Document filenames MUST follow the format `{filingId}-{slugified-event-title}.{ext}` (e.g., `2018-000815365-initial-filing.pdf`). The response MUST include the storage URLs for each downloaded document.
- **FR-013**: The system MUST extract WY Parties information (name, role, organization, address) from the "Parties" section when present. The response MUST include a list of party objects. If no parties are assigned, the list MUST be empty.

### Key Entities

- **EntityDetailResult** (extended): The existing details model gains nullable WY-specific fields: SubStatus, StandingTax, StandingRA, StandingOther, InactiveDate, TermOfDuration, FormedIn, LatestAnnualReportYear, AnnualReportExempt, LicenseTaxPaid. (WY Filing ID maps to existing `IdNumber`; WY Type maps to existing `Form`.)
- **PartyResult**: A new model representing a party associated with a WY entity: Name, Role (e.g., Organizer, Manager, Director), Organization (nullable), Address.
- **HistoryDocumentResult**: A new model representing a downloaded WY history document: EventTitle, Date, StorageUrl, FileName.
- **WY Flow Definition**: A JSON-driven scraping definition (`wy-entity-details.json`) describing the navigate → CAPTCHA → extract sequence for Wyoming entity details pages.
- **Audit Record**: The existing entity details audit table gains a `StateCode` column to distinguish CO vs WY operations.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can retrieve WY entity details in under 30 seconds (including CAPTCHA resolution).
- **SC-002**: The endpoint successfully returns WY entity details for both active and inactive entities.
- **SC-003**: Existing CO entity details requests continue to function identically when callers supply `state=CO`.
- **SC-004**: All WY entity detail requests produce diagnostic artifacts (screenshots, traces) retrievable by correlation ID.
- **SC-005**: Invalid URLs and unsupported state codes are rejected with appropriate error responses 100% of the time.

## Assumptions

- The existing FAB captcha-solver service handles WY CAPTCHAs the same way it handles WY search CAPTCHAs (same image format and challenge type).
- WY entity details pages follow a consistent HTML structure across all entity types (LLC, Corporation, etc.).
- The WY site does not provide a direct "certificate of good standing" download link on the entity details page; certificates are available via a separate page (`ViewCertificate.aspx`). Certificate download for WY is out of scope for this feature.
- The existing concurrency semaphore and browser pool infrastructure supports the additional WY load without configuration changes.
- The `eFNum` parameter in WY URLs is an opaque identifier; no parsing or validation of its content is required beyond URL structure validation.

## Clarifications

### Session 2026-03-25

- Q: Should the WY annual report fields (Latest AR/Year, AR Exempt, License Tax Paid) be extracted? → A: Yes, extract as nullable fields on the response.
- Q: Should the WY History section be extracted? → A: Skip full history list extraction. Download the most recent 3 history documents (filing images) and upload to the storage account.
- Q: Should the WY Parties section be extracted? → A: Yes, extract as a list of (name, role, organization, address) objects.
