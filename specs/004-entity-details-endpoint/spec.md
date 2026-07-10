# Feature Specification: Entity Details Retrieval & Certificate of Good Standing Download

**Feature Branch**: `004-entity-details-endpoint`  
**Created**: 2026-03-24  
**Status**: Draft  
**Input**: User description: "Build a new endpoint to pull the entity details from detailsUrl of an entity. When navigating to the details url, it pulls the entity details in a table. If 'Get a certificate of good standing' link is available, click on that link, and download the pdf. Store the pdf in the storage account with documentnumber-certificateofgoodstanding.pdf filename."

## Clarifications

### Session 2026-03-24

- Q: Should the certificate of good standing URL be stored in the audit trail and returned in the JSON response? → A: Yes. The certificate URL must be persisted to the audit trail as an explicit, dedicated column and returned in the response JSON.
- Q: Should entity detail lookups use a new dedicated audit table or add rows to the existing SearchAuditEntries table? → A: New dedicated audit table (e.g., EntityDetailAuditEntries) with columns tailored to entity detail lookups.
- Q: Should the entity detail endpoint allow concurrent requests or enforce a single-request semaphore like the existing search endpoint? → A: Separate configurable concurrency limit (e.g., max 3 concurrent entity detail requests), independent of the search semaphore.
- Q: What should the API route pattern be for the entity detail endpoint? → A: `GET /api/v1/entity-details?detailsUrl={url}` — accept the full CO SOS details URL as a query parameter, but do not trust the URL: enforce strict allowlist validation (host, scheme, path pattern) before navigating to prevent SSRF.
- Q: Should the certificate PDF be stored in the same blob container as diagnostics or in a separate container? → A: Same container as diagnostics — certificates stored alongside screenshots/traces under the correlation-ID folder.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Retrieve Business Entity Details (Priority: P1)

A consumer of the API provides a CO SOS entity details URL (the `detailsUrl` returned by the existing business search endpoint). The system navigates to that URL, scrapes the structured entity detail information from the "Details" and "Registered Agent" tables, and returns it as JSON.

The details page contains two distinct data sections discovered via live page inspection:

**Details table fields:**
- Name (full legal name including any status suffix)
- Status (e.g., "Good Standing", "Delinquent")
- Formation date (e.g., "11/02/2018")
- ID number (e.g., "20181871974")
- Form (e.g., "Corporation", "Limited Liability Company")
- Periodic report month (e.g., "November")
- Jurisdiction (e.g., "Colorado")
- Principal office street address
- Principal office mailing address

**Registered Agent table fields:**
- Name
- Street address
- Mailing address

**Why this priority**: This is the core value — retrieving structured entity detail data. Without this, the certificate download feature has no context. This directly extends the existing search API by enabling drill-down into individual results.

**Independent Test**: Call the endpoint with a valid details URL for a known entity (e.g., entity ID 20181871974). Verify the JSON response contains all detail fields with values matching the CO SOS website.

**Acceptance Scenarios**:

1. **Given** the API is running, **When** a caller submits a valid entity details URL, **Then** the API returns a JSON object containing all fields from the Details table (Name, Status, FormationDate, IdNumber, Form, PeriodicReportMonth, Jurisdiction, PrincipalOfficeStreetAddress, PrincipalOfficeMailingAddress).
2. **Given** the API is running, **When** a caller submits a valid entity details URL, **Then** the API returns a JSON object containing all Registered Agent fields (Name, StreetAddress, MailingAddress).
3. **Given** the API is running, **When** a caller submits a valid entity details URL for an entity in "Good Standing", **Then** the response includes a flag indicating a certificate of good standing is available.
4. **Given** the API is running, **When** a caller submits a valid entity details URL for a "Delinquent" entity, **Then** the response includes a flag indicating no certificate of good standing is available.
5. **Given** the API is running, **When** the entity details page lists action links (e.g., "Filing history and documents", "Get certified copies of documents"), **Then** those links are not followed — only the detail data is scraped.

---

### User Story 2 — Download Certificate of Good Standing PDF (Priority: P2)

When the entity detail page contains a "Get a certificate of good standing" link, the system clicks that link, captures the PDF response (the CO SOS site returns `application/pdf` directly), and stores the PDF in the cloud storage account using the naming convention `{documentnumber}-certificateofgoodstanding.pdf`.

**Why this priority**: This extends the details retrieval with actionable document acquisition. It depends on the details scraping from US-1 to determine whether the certificate link is present and to extract the document number for the filename.

**Independent Test**: Call the endpoint with a details URL for a "Good Standing" entity (e.g., entity ID 20051063025). Verify the PDF is stored in the cloud storage container with the correct filename, and the response includes the storage URL of the downloaded certificate.

**Acceptance Scenarios**:

1. **Given** cloud storage is configured and the entity has "Good Standing" status, **When** the details endpoint is called, **Then** the system clicks the "Get a certificate of good standing" link, downloads the PDF, and uploads it to cloud storage with filename `{documentnumber}-certificateofgoodstanding.pdf`.
2. **Given** cloud storage is configured and the entity does not have a "Get a certificate of good standing" link, **When** the details endpoint is called, **Then** no PDF download is attempted and the response indicates no certificate is available.
3. **Given** cloud storage is configured, **When** the certificate PDF is successfully uploaded, **Then** the response includes the cloud storage URL where the certificate was saved.
4. **Given** the certificate PDF download fails (site error, timeout), **When** the details endpoint is called, **Then** the entity details are still returned successfully but the certificate section indicates the download failed.
5. **Given** cloud storage is not configured (disabled), **When** the details endpoint is called for a "Good Standing" entity, **Then** the certificate is not downloaded and the response indicates cloud storage is not enabled for certificate storage.

---

### User Story 3 — Diagnostics for Entity Detail Scraping (Priority: P3)

As an operator, I want the entity detail scraping to produce the same diagnostic artifacts (screenshots and traces) as the existing search feature, organized by correlation ID, so that I can troubleshoot failures.

**Why this priority**: Reuses the existing diagnostics infrastructure. Important for production operations but not core to the feature's data retrieval value.

**Independent Test**: Call the endpoint with diagnostics enabled. Verify screenshots are captured at key milestones (page load, after scraping, certificate download) and written to the correlation-ID folder both locally and in cloud storage.

**Acceptance Scenarios**:

1. **Given** diagnostics are enabled, **When** the entity detail endpoint processes a request, **Then** screenshots are captured at key milestones (detail page load, certificate download if applicable) and saved to the per-request correlation ID folder.
2. **Given** diagnostics and cloud upload are enabled, **When** screenshots are captured, **Then** they are uploaded to the cloud storage container under the same correlation-ID folder structure used by the search feature.
3. **Given** the scraping fails, **When** an error occurs, **Then** an error screenshot is force-captured and a trace file is saved for debugging.

---

### Edge Cases

- What happens when the details URL is malformed or points to a non-existent entity? The system returns a clear error indicating the entity was not found or the URL is invalid.
- What happens when the CO SOS site is down or returns an unexpected page structure? The system returns an upstream error response with details about the failure.
- What happens when the entity details page loads but the expected data tables are missing? The system returns a scraping error indicating the page structure was unexpected.
- What happens when the certificate PDF download returns an HTML error page instead of a PDF? The system detects the non-PDF content type and reports the certificate download as failed without crashing.
- What happens when the document number extracted from the page contains unexpected characters? The system validates the document number is alphanumeric before constructing the filename; if it contains non-alphanumeric characters, the certificate download is skipped and the error is reported in the CertificateResult.
- What happens when a concurrent request attempts to retrieve details for the same entity? Each request is independent and produces its own result; no caching or deduplication is applied.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose a `GET /api/v1/entity-details?detailsUrl={url}` endpoint that accepts a CO SOS entity details URL and returns structured entity information as JSON.
- **FR-002**: System MUST scrape the "Details" table from the entity detail page, extracting: Name, Status, Formation date, ID number, Form, Periodic report month, Jurisdiction, Principal office street address, and Principal office mailing address.
- **FR-003**: System MUST scrape the "Registered Agent" table from the entity detail page, extracting: Name, Street address, and Mailing address.
- **FR-004**: System MUST detect the presence of a "Get a certificate of good standing" link on the entity detail page and indicate its availability in the response.
- **FR-005**: System MUST, when the certificate link is present and cloud storage is enabled, click the link, capture the PDF response, and upload it to the same cloud storage container used for diagnostics with filename `{documentnumber}-certificateofgoodstanding.pdf` under the request's correlation-ID folder.
- **FR-006**: System MUST return the cloud storage URL of the uploaded certificate PDF in the response when the upload succeeds.
- **FR-007**: System MUST validate the input URL before navigating using a strict allowlist: scheme MUST be HTTPS, host MUST be `www.sos.state.co.us`, path MUST match the expected `BusinessEntityDetail.do` pattern. Any URL that fails validation MUST be rejected with a 400 error. This prevents SSRF by ensuring the browser is never directed to an attacker-controlled destination.
- **FR-008**: System MUST produce diagnostic artifacts (screenshots, traces) following the same conventions as the existing search feature.
- **FR-009**: System MUST return entity details even if the certificate download fails — the certificate is a supplementary artifact, not a prerequisite for the response.
- **FR-010**: System MUST record an audit trail entry for each entity detail retrieval request in a dedicated audit table (separate from the search audit table), including the correlation ID, outcome status, and the cloud storage URL of the certificate of good standing PDF (when downloaded).
- **FR-011**: System MUST persist the certificate of good standing URL as an explicit, dedicated column in the entity detail audit table — not embedded within a generic metadata field.
- **FR-012**: System MUST enforce a separate, configurable concurrency limit for entity detail requests (independent of the search semaphore). Excess requests beyond the limit MUST receive a 503 response with a Retry-After header.

### Key Entities

- **EntityDetail**: Represents the full detail information for a single business entity. Contains identity fields (ID number, name, status, form), date fields (formation date), location fields (addresses), jurisdiction, periodic report month, and registered agent information.
- **RegisteredAgent**: Represents the registered agent associated with a business entity. Contains name, street address, and mailing address.
- **CertificateOfGoodStanding**: Represents the optional certificate PDF. Contains availability flag, document number used in the filename, and cloud storage URL if downloaded. The cloud storage URL is persisted to the audit trail as a dedicated field and returned in the JSON response.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Callers can retrieve structured entity details for any valid CO SOS details URL within 30 seconds (including browser automation overhead).
- **SC-002**: When a certificate of good standing is available, the PDF is downloaded and stored in cloud storage within 45 seconds of the initial request.
- **SC-003**: 100% of entity detail fields visible on the CO SOS page are captured in the JSON response — no silent data loss.
- **SC-004**: Certificate download failures do not prevent entity details from being returned — the detail response is always delivered when the page loads successfully.
- **SC-005**: Diagnostic artifacts are produced for every request, enabling operators to troubleshoot any failure without needing to reproduce it manually.

## Assumptions

- The CO SOS entity details page structure (HTML table layout with "Details" and "Registered Agent" sections) is stable and follows the pattern observed during discovery.
- The "Get a certificate of good standing" link is only present for entities whose status allows certificate issuance (typically "Good Standing").
- The `BuildCertificate.do` endpoint returns `application/pdf` content directly — no intermediate confirmation pages or CAPTCHAs.
- The existing cloud storage infrastructure (Azure Blob Storage) and upload mechanism from feature 003 will be reused for certificate PDF storage.
- The existing Playwright browser pool, diagnostics, and audit trail infrastructure will be reused.
- The certificate PDF filename uses only the numeric document number (e.g., `20051063025-certificateofgoodstanding.pdf`). The document number is validated to be alphanumeric before use; unexpected characters cause the certificate download to be skipped gracefully.
- The entity detail endpoint is a separate API route from the existing search endpoint, using the same CQRS pattern and vertical-slice architecture.
