# Feature Specification: German Hardcopy Printout Retrieval with FAB Translation

**Feature Branch**: `015-de-hardcopy-printout`  
**Created**: 2026-03-27  
**Status**: Draft  
**Input**: User description: "In the German business results, each row has a link 'AD' which is 'Current Hardcopy Printout'. I want to enhance the german business search api to pull down the hard copy printout. This printout is in german, so I want to call a 'Generic Document Processor' FAB Agent to convert this into English. I want to generate a pdf with this converted english document. I want to enhance the test-ui to offer testing this capability. The api endpoint should be /api/v1/germany-search/current-hardcopy-printout."

## Clarifications

### Session 2026-03-27

- Q: What format should the German document content be sent to the FAB agent? → A: Base64-encoded PDF (the AD link downloads a PDF directly via PrimeFaces form submission; send as base64 matching the existing CAPTCHA solver pattern).
- Q: What output format should the FAB agent return the translated document in? → A: Markdown (converted to HTML then rendered to PDF via Playwright `page.pdf()`).
- Q: Should the original German PDF also be stored in blob storage alongside the translated English PDF? → A: Yes, store both. Original as `{correlationId}/de-hardcopy-{slug}-original.pdf`, translated as `{correlationId}/de-hardcopy-{slug}.pdf`.
- Q: How should the FAB Document Translator be configured relative to the existing CAPTCHA solver? → A: Separate options class `FabDocumentTranslatorOptions` with its own independent configuration section in `appsettings.json`.
- Q: Where does the `registrationId` come from? The current Germany search API returns only `CompanyName`, `RegisteredOffice`, and `Status` — no registration header. → A: Enhance the Germany search to extract and return the registration header (court + register type + register number) from the `span.fontWeightBold` element in each result row. Add a `RegistrationId` field to `GermanyBusinessResult`. This provides callers (and the test-ui) the data they need to call the hardcopy endpoint. Verified via Playwright MCP on 2026-03-27: each result row's header contains structured data like "District court München HRB 73851".

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Retrieve and Translate a German Hardcopy Printout (Priority: P1)

A caller sends a company identifier (the court name and registration number, e.g., "Paderborn HRA 3059") and search term to the API. The system navigates the Handelsregister, locates the matching result row, clicks the "AD" (Aktueller Druck / Current Hardcopy Printout) link, captures the resulting German-language document, sends it to the FAB Generic Document Processor agent for translation into English, generates a PDF from the translated English content, stores it in blob storage, and returns the PDF to the caller.

**Why this priority**: This is the core value of the feature — without retrieval, translation, and PDF generation, no other story is useful.

**Independent Test**: Call `POST /api/v1/germany-search/current-hardcopy-printout` with `{ "searchTerm": "Claes und Rohde", "registrationId": "Paderborn HRA 3059" }` → receive a PDF response containing an English translation of the German hardcopy printout.

**Acceptance Scenarios**:

1. **Given** a valid search term and registration ID that matches exactly one result on the Handelsregister, **When** the caller sends a POST request, **Then** the system returns a PDF containing the English-translated content with a 200 status and `Content-Type: application/pdf`.
2. **Given** a valid request, **When** the hardcopy is retrieved and translated, **Then** the translated PDF is stored in blob storage under `{correlationId}/de-hardcopy-{registrationId-slug}.pdf` and the storage URL is included in the response headers (`X-Document-Url`).
3. **Given** a valid request, **When** the system captures the German document, **Then** it also saves a screenshot of the original German printout page as a diagnostic artifact.

---

### User Story 2 — Handle Missing or Unavailable Hardcopy (Priority: P1)

A caller provides a registration ID that either does not exist in the Handelsregister results or whose "AD" link is not available (some results have greyed-out/inactive document links).

**Why this priority**: Error handling is essential for a usable API — callers must receive clear feedback when a document cannot be retrieved.

**Independent Test**: Call the endpoint with a registration ID that does not appear in the search results → receive a 404 response with RFC 9457 Problem Details explaining that the registration was not found.

**Acceptance Scenarios**:

1. **Given** a search term that returns results but no row matches the provided registration ID, **When** the caller sends the request, **Then** the system returns 404 with a Problem Details body: "No result matching registration ID '{id}' found in Handelsregister results for search term '{term}'."
2. **Given** a matching result row exists but the AD link is inactive/unavailable, **When** the caller sends the request, **Then** the system returns 404 with a Problem Details body: "The current hardcopy printout (AD) is not available for registration '{id}'."
3. **Given** an empty or missing search term or registration ID, **When** the caller sends the request, **Then** the system returns 400 Bad Request with a Problem Details body listing the missing fields.

---

### User Story 3 — FAB Translation Failure Handling (Priority: P2)

The FAB Generic Document Processor agent may be unavailable, return errors, or time out. The system must handle these gracefully.

**Why this priority**: The FAB agent is an external dependency — resilient error handling prevents cascading failures and provides useful diagnostics.

**Independent Test**: Simulate FAB agent returning 500 → system returns 502 with a clear error message indicating the translation service is unavailable.

**Acceptance Scenarios**:

1. **Given** the hardcopy is retrieved successfully but the FAB agent returns a 5xx error, **When** processing, **Then** the system returns 502 Bad Gateway with Problem Details: "The document translation service is temporarily unavailable."
2. **Given** the FAB agent times out, **When** processing, **Then** the system returns 504 Gateway Timeout with Problem Details explaining the timeout.
3. **Given** the FAB agent returns a successful response but the translated content is empty or malformed, **When** processing, **Then** the system returns 502 with Problem Details: "The translation service returned an invalid response."

---

### User Story 4 — Test UI Hardcopy Printout Capability (Priority: P2)

A tester navigates to the Germany Search section of the test-ui. After performing a search, each result row displays an "AD" action button. Clicking it triggers the hardcopy retrieval and translation, then displays the resulting English PDF.

**Why this priority**: The test-ui provides the manual testing capability requested by the user, but the API must work first.

**Independent Test**: In the test-ui, search for "Claes und Rohde" → results appear → click the "AD" button on a result row → a loading indicator appears → the English PDF opens in the browser's PDF viewer.

**Acceptance Scenarios**:

1. **Given** Germany search results are displayed in the test-ui, **When** a result row is rendered, **Then** an "AD" button appears in an actions column.
2. **Given** the user clicks the "AD" button, **When** processing, **Then** a loading indicator is shown and the button is disabled to prevent duplicate requests.
3. **Given** the translation completes successfully, **When** the PDF is returned, **Then** the PDF opens in a new browser tab using the browser's built-in PDF viewer.
4. **Given** the translation fails, **When** the error response is received, **Then** an error banner displays the failure reason to the user.

---

### User Story 5 — Audit Trail for Hardcopy Requests (Priority: P3)

Every hardcopy retrieval request is persisted to the audit trail for traceability.

**Why this priority**: Audit is important for compliance and debugging but does not affect core functionality.

**Independent Test**: Call the endpoint successfully → verify an audit record exists with the search term, registration ID, outcome, duration, correlation ID, and document storage URL.

**Acceptance Scenarios**:

1. **Given** a successful hardcopy retrieval, **When** the response is returned, **Then** an audit entry is persisted with status "Success", the blob storage URL of the translated PDF, and the correlation ID.
2. **Given** a failed request (any failure reason), **When** the error response is returned, **Then** an audit entry is persisted with the appropriate error status and error detail.

---

### Edge Cases

- What happens when the Handelsregister returns multiple results and more than one matches the registration ID partially? The system uses exact match on the registration ID from the result header row (e.g., "District court Paderborn HRA 3059").
- What happens when the AD link is clicked? The AD link triggers a PrimeFaces form-based PDF download (verified). The system uses Playwright's `page.waitForEvent('download')` to capture the downloaded PDF file. No popup or page navigation occurs.
- What happens when the German document is extremely large (many pages)? The FAB agent request includes the full document content. The system enforces a configurable maximum document size limit; documents exceeding the limit return 413 with a Problem Details error.
- What happens when the Handelsregister session expires mid-request? The system performs the entire flow (search → find row → click AD) in a single browser session, reducing the risk of session expiry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST expose a new API endpoint at `POST /api/v1/germany-search/current-hardcopy-printout` that accepts a JSON body with `searchTerm` (company name) and `registrationId` (court and registration number, e.g., "Paderborn HRA 3059").
- **FR-002**: The system MUST navigate to the Handelsregister, perform a search using the provided search term, and locate the result row whose header contains the provided registration ID.
- **FR-003**: The system MUST click the "AD" (Aktueller Druck / Current Hardcopy Printout) link on the matching result row and capture the resulting PDF download. The AD link is a PrimeFaces form submission that triggers a PDF file download (not a page navigation). The system MUST use Playwright's download event handling (`page.waitForEvent('download')`) to capture the downloaded file. The downloaded PDF filename follows the pattern `{State}-{Court}_{RegisterType}_{RegisterNumber}+AD-{timestamp}.pdf` (e.g., `SN-Leipzig_HRB_30226+AD-20260328035907.pdf`).
- **FR-004**: The system MUST read the downloaded German PDF file, encode it as base64, and send it to the FAB Generic Document Processor agent for translation from German to English. The input format MUST match the existing FAB integration pattern: `{ "input": { "format": "base64", "data": "<base64-encoded-pdf>" } }`.
- **FR-005**: The FAB agent request MUST follow the existing FAB integration contract: HTTP POST with base64-encoded PDF input, receiving a structured JSON response containing the translated English text as Markdown in the `output` field.
- **FR-006**: The system MUST convert the Markdown content returned by the FAB agent into HTML, then render the HTML to PDF using Playwright's `page.pdf()` capability. Markdig (BSD-2-Clause licensed) MUST be used for the Markdown-to-HTML conversion step.
- **FR-007**: The system MUST store both PDFs in blob storage: the original German PDF under `{correlationId}/de-hardcopy-{registrationId-slug}-original.pdf` and the translated English PDF under `{correlationId}/de-hardcopy-{registrationId-slug}.pdf`, where `registrationId-slug` is a URL-safe version of the registration ID.
- **FR-008**: The system MUST return the translated English PDF as the response body with `Content-Type: application/pdf` and include response headers: `X-Document-Url` (translated PDF blob URL), `X-Original-Document-Url` (original German PDF blob URL), `X-Correlation-Id` (correlation ID for traceability), and `Content-Disposition` (attachment filename).
- **FR-009**: The system MUST validate that both `searchTerm` and `registrationId` are non-empty in the request. Missing or empty values MUST return 400 Bad Request with RFC 9457 Problem Details.
- **FR-010**: When no matching result row is found for the registration ID, the system MUST return 404 Not Found with a descriptive Problem Details body.
- **FR-011**: When the matching result row's AD link is inactive or unavailable, the system MUST return 404 with a Problem Details body explaining the AD link is not available.
- **FR-012**: When the FAB agent is unavailable (5xx or timeout), the system MUST return 502 Bad Gateway or 504 Gateway Timeout with Problem Details.
- **FR-013**: The system MUST capture diagnostic screenshots at key points: search results page, the specific result row matched, and the AD printout page. Screenshots are stored in blob storage under the correlation ID folder.
- **FR-014**: The system MUST persist an audit trail entry for every request, capturing: search term, registration ID, outcome status, duration, error detail (if any), correlation ID, and document storage URL.
- **FR-015**: The system MUST enforce a configurable concurrency limit (semaphore) for hardcopy requests to avoid overwhelming the Handelsregister site. Excess requests MUST return 503 Service Unavailable with `Retry-After`.
- **FR-016**: The test-ui MUST be updated to display an "AD" action button on each Germany search result row. Clicking the button triggers the hardcopy retrieval and opens the resulting PDF.
- **FR-017**: The system MUST use the existing Playwright browser pool and scraping engine patterns for navigating the Handelsregister.
- **FR-018**: The FAB Generic Document Processor agent MUST be configured with a system prompt instructing it to translate the German Handelsregister document into English while preserving the document structure (headings, tables, lists).
- **FR-019**: The FAB Document Translator MUST be configured via a new, independent `FabDocumentTranslatorOptions` class (separate from the existing `FabAgentOptions.CaptchaSolver`). The configuration section in `appsettings.json` MUST include `EndpointUrl`, `AuthToken` (from Key Vault), and `TimeoutSeconds`. This class MUST NOT be nested inside `FabAgentOptions`.
- **FR-020**: The Germany search extraction (`de-business-search.json`) MUST be enhanced to also extract the registration header from each result row's `span.fontWeightBold` element (e.g., "District court München HRB 73851"). The `GermanyBusinessResult` record MUST gain a new `RegistrationId` field containing this value. The `SearchGermanyBusinessesHandler` mapping from `NormalizedSearchResult` MUST be updated to populate this field.
- **FR-021**: The test-ui Germany search results table MUST display the `registrationId` as a new column and pass it to the hardcopy endpoint when the AD button is clicked.
- **FR-022**: The system MUST enforce a configurable maximum document size limit for the downloaded German PDF. Documents exceeding the limit MUST return 413 Content Too Large with RFC 9457 Problem Details.

### Key Entities

- **HardcopyPrintoutRequest**: The API input — a search term and registration ID identifying a specific business entity record on the Handelsregister.
- **HardcopyPrintoutResponse**: The API output — an English-translated PDF document streamed as the response body, with metadata (document storage URL, correlation ID) in response headers.
- **FabTranslationRequest**: The outbound request to the FAB Generic Document Processor agent — contains the German PDF as base64-encoded data: `{ "input": { "format": "base64", "data": "<base64-pdf>" } }`.
- **FabTranslationResponse**: The response from the FAB agent — contains the translated English content as Markdown in the `output` field: `{ "output": { "translatedMarkdown": "# Company Registration\n\n| Field | Value |\n...", "sourceLanguage": "de" } }`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A caller can retrieve and receive an English-translated PDF of any available German hardcopy printout within 90 seconds end-to-end (search + navigate + capture + translate + PDF generation).
- **SC-002**: The translated PDF accurately preserves the structural layout (headings, tables, paragraphs) of the original German document as determined by manual review.
- **SC-003**: Failed requests (missing result, unavailable AD link, FAB failure) return clear, actionable error messages with appropriate HTTP status codes within 30 seconds.
- **SC-004**: Every request is audited with complete traceability — correlation ID links the audit entry, diagnostic screenshots, and stored PDF.
- **SC-005**: The test-ui allows testers to trigger hardcopy retrieval from the Germany search results with a single click and view the resulting PDF.
- **SC-006**: The system handles concurrent requests gracefully — excess requests receive 503 with Retry-After rather than degrading or failing silently.

## Assumptions

- The Handelsregister "AD" link is a PrimeFaces `monitorDownload` form submission that triggers a direct PDF file download (verified via Playwright MCP discovery on 2026-03-27). The system captures the download using Playwright's download event API.
- The FAB Generic Document Processor agent is available and accepts document translation requests following the same HTTP contract pattern as the existing CAPTCHA solver agent (POST with structured JSON input/output).
- The FAB agent can translate German legal/business registration documents with sufficient accuracy for business use.
- The translated English content returned by the FAB agent can be rendered into a well-formatted PDF using Playwright's PDF generation capabilities (rendering translated HTML to PDF).
- The existing browser pool, blob storage, audit trail, and diagnostics infrastructure can be reused without modification.
- The registration ID from the Handelsregister result header (e.g., "District court Paderborn HRA 3059") is sufficiently unique within a search result set to identify a specific row.
- The registration header is present in every Handelsregister result row inside a `span.fontWeightBold` element in the first `tr` of each result's `table.ui-panelgrid` (verified via Playwright MCP on 2026-03-27 with "Rohde & Schwarz" search returning 14 results, all with headers like "District court München HRB 73851").
- A single concurrent hardcopy request limit (semaphore=1) is appropriate initially, matching the Germany search pattern.
