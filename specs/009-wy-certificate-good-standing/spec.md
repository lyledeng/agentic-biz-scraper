# Feature Specification: WY Certificate of Good Standing

**Feature Branch**: `009-wy-certificate-good-standing`
**Created**: 2026-03-26
**Status**: Draft
**Input**: User description: "Pull the WY Certificate of Good Standing from the Wyoming SOS website when performing a WY detail search. Navigate to ViewCertificate.aspx, enter the Filing ID, verify the entity, click Print, and save the downloaded PDF."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Retrieve WY Certificate of Good Standing During Entity Detail Scrape (Priority: P1)

As an API consumer, I want the WY entity details response to include a Certificate of Good Standing PDF when the entity is eligible, so that I can obtain the official certificate without manually visiting the Wyoming SOS website.

When a WY entity details request is processed, the system navigates to the Wyoming SOS Certificate of Good Standing page (`https://wyobiz.wyo.gov/Business/ViewCertificate.aspx`), enters the entity's Filing ID, verifies the entity information on the confirmation page, clicks "Print" to trigger the PDF download, saves the PDF locally, and uploads it to the cloud storage account. The certificate storage URL and filename are returned in the existing `certificate` field of the entity details response.

**Why this priority**: This is the core feature — without it, the WY `certificate` field remains permanently `null` despite WY certificates being freely available. This directly addresses the gap documented in feature 007 (FR-011).

**Independent Test**: Can be fully tested by calling `GET /api/v1/entity-details?detailsUrl={wyUrl}&state=WY` for an entity in good standing (e.g., Filing ID `2017-000757675`) and verifying the response `certificate` field contains a valid `storageUrl`, `fileName`, and `available: true`.

**Acceptance Scenarios**:

1. **Given** a WY entity in good standing (e.g., Filing ID `2017-000757675`), **When** the entity details endpoint is called with `state=WY`, **Then** the system navigates to the WY Certificate of Good Standing page, enters the Filing ID, verifies the entity, clicks Print, downloads the PDF, uploads it to cloud storage, and returns the `certificate` field with `available: true`, a valid `storageUrl`, and `fileName`.
2. **Given** a WY entity in good standing and cloud storage is enabled, **When** the certificate PDF is successfully uploaded, **Then** the certificate is stored at `{correlationId}/{filingId}-certificateofgoodstanding.pdf` in the configured blob container.
3. **Given** a WY entity in good standing, **When** the certificate download completes, **Then** the audit trail record includes the certificate storage URL.
4. **Given** a WY entity details request, **When** the certificate page returns an error or the entity is not eligible for a certificate, **Then** the entity details are still returned with `certificate` set to `null` (if the page was never reached) or `certificate.available` set to `false` with an `error` message.

---

### User Story 2 - Graceful Failure When Certificate Is Unavailable (Priority: P2)

As an API consumer, I want the entity details response to still be returned even if the certificate download fails or the entity is not eligible, so that a certificate issue never blocks access to entity details.

**Why this priority**: Resilience is essential for production use, but the core happy-path must work first. This ensures certificate retrieval is supplementary — consistent with the CO certificate pattern (feature 004, FR-009).

**Independent Test**: Can be tested by calling the endpoint for a WY entity that is not in good standing, or by simulating a certificate page failure, and verifying entity details are still fully returned.

**Acceptance Scenarios**:

1. **Given** a WY entity with standing values that are not all "Good", **When** the entity details endpoint is called, **Then** the system still attempts the certificate page; the WY SOS site rejects the request, and the system returns `certificate` as `null`.
2. **Given** a WY entity in good standing but the certificate page returns an error (timeout, server error), **When** the entity details endpoint is called, **Then** entity details are returned with `certificate.available` set to `false` and `certificate.error` describing the failure.
3. **Given** a WY entity in good standing but the cloud storage upload fails, **When** the response is returned, **Then** entity details are complete, `certificate.available` is `true` (PDF was downloaded successfully), and `certificate.error` describes the upload failure.
4. **Given** the Filing ID extracted from a WY entity does not match the expected format (e.g., contains non-alphanumeric characters beyond the hyphen), **When** the certificate retrieval is attempted, **Then** the system skips the certificate download and logs a warning.

---

### User Story 3 - Diagnostics for Certificate Retrieval Flow (Priority: P3)

As an operations engineer, I want the certificate retrieval flow to produce screenshots at key milestones, so that I can troubleshoot failures in the multi-page certificate navigation.

**Why this priority**: Diagnostics support production troubleshooting but are not required for the certificate functionality itself.

**Independent Test**: Can be tested by triggering a WY certificate retrieval and verifying screenshot files exist for each milestone (certificate page load, entity verification page, after Print click).

**Acceptance Scenarios**:

1. **Given** a WY entity details request with diagnostics enabled, **When** the certificate flow executes, **Then** screenshots are captured at key milestones: certificate search page, entity verification page, and after the Print/download action.
2. **Given** diagnostics are enabled, **When** the certificate screenshots are captured, **Then** they are stored alongside other entity details diagnostics under the same correlation ID folder.

---

### Edge Cases

- What happens when the Filing ID entered on the certificate page does not match any entity? The certificate page returns an error; the system captures the error, logs it, and returns `certificate` as `null` without failing the overall entity details response.
- What happens when the WY SOS certificate page is unavailable or times out? The system returns entity details with `certificate.error` populated and continues normally.
- What happens when the downloaded file is not a PDF? The system validates the downloaded filename ends in `.pdf`; if not, the download is discarded and logged as a warning.
- What happens when the entity is not in good standing (e.g., standings include "Delinquent")? The system still attempts the certificate page; the WY SOS site will reject the request if the entity is ineligible. The system captures the rejection, logs it, and returns `certificate` as `null`.
- What happens when the Filing ID format is unexpected? The system validates the Filing ID matches the expected WY format (`YYYY-NNNNNNNNN`) before attempting the certificate page. Invalid formats are skipped with a warning log.
- What happens when cloud storage is disabled? The certificate PDF is saved locally but not uploaded; `certificate.storageUrl` is `null`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: During a WY entity details scrape, the system MUST always attempt to retrieve the Certificate of Good Standing, regardless of the entity's standing values. The WY SOS site determines eligibility; the system does not pre-filter based on standing fields.
- **FR-002**: The system MUST navigate to `https://wyobiz.wyo.gov/Business/ViewCertificate.aspx`, enter the entity's Filing ID in the "Filing ID" text field, and click the Search button to load the entity verification page.
- **FR-003**: On the entity verification page, the system MUST click the "Print" button to trigger the certificate PDF download. The system does NOT need to verify the displayed entity details against the previously scraped entity — the Filing ID lookup is deterministic.
- **FR-004**: The system MUST validate that the downloaded file is a PDF (filename ends in `.pdf`) before proceeding with storage. Non-PDF downloads MUST be discarded with a warning log.
- **FR-005**: The system MUST validate the Filing ID against the expected WY format before navigating to the certificate page. Filing IDs that do not match the expected pattern MUST be skipped with a warning log.
- **FR-006**: When cloud storage is enabled, the system MUST upload the certificate PDF to the cloud storage account at the path `{correlationId}/{filingId}-certificateofgoodstanding.pdf` within the configured blob container.
- **FR-007**: The system MUST return the cloud storage URL and filename of the uploaded certificate in the existing `certificate` field of the WY entity details response, using the same `CertificateResult` model as CO certificates.
- **FR-008**: The system MUST return entity details even if the certificate retrieval fails — the certificate is a supplementary artifact, not a prerequisite for the response.
- **FR-009**: The system MUST persist the certificate storage URL in the entity details audit trail record.
- **FR-010**: The system MUST capture screenshots at key milestones during the certificate flow when diagnostics are enabled: certificate search page loaded, entity verification page displayed, and after the Print/download action.
- **FR-011**: The certificate retrieval MUST reuse the same browser context and Playwright session used for the entity details scrape to avoid additional CAPTCHA challenges or session overhead.
- **FR-012**: The certificate retrieval MUST be defined as an extension to the existing WY entity details JSON-driven scraping flow definition, consistent with the JSON scraping engine architecture (feature 006).
- **FR-013**: The certificate retrieval MUST have its own independent 15-second timeout. If the timeout elapses, the certificate attempt is abandoned and the entity details response is returned with `certificate.error` describing the timeout. This timeout is independent of the entity details scrape timeout.
- **FR-014**: For WY entities, `CertificateResult.Available` MUST be `true` only when the certificate PDF was successfully downloaded. If the WY SOS site rejects the Filing ID or the download fails, `Available` MUST be `false`. This differs from CO where `Available` indicates the presence of the certificate link on the details page.

### Key Entities

- **CertificateResult** (existing): Represents the certificate download outcome — `Available`, `StorageUrl`, `FileName`, `Error`. Currently always `null` for WY; this feature populates it when the entity qualifies.
- **WY Entity Details Flow Definition** (extended): The existing `wy-entity-details.json` scraping flow gains additional steps for certificate retrieval via the ViewCertificate.aspx page.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: WY entity details responses for entities in good standing include a populated `certificate` field with a valid storage URL and filename.
- **SC-002**: Certificate retrieval completes within its own independent 15-second timeout budget. This is separate from the entity details scrape timeout — the total WY entity details request time may be up to the sum of both timeouts.
- **SC-003**: Existing WY entity details functionality (details, registered agent, parties, history documents) continues to work identically when the entity is not eligible for a certificate.
- **SC-004**: Existing CO entity details certificate functionality is unaffected by this change.
- **SC-005**: Certificate retrieval failures do not block or degrade the entity details response — entity details are always returned regardless of certificate outcome.

## Assumptions

- The WY SOS Certificate of Good Standing page (`ViewCertificate.aspx`) does not require CAPTCHA to access. The page accepts the Filing ID directly and returns a verification page followed by a PDF download on "Print" click.
- The Filing ID displayed on the WY entity details page (e.g., `2017-000757675`) is the same identifier accepted by the certificate page's Filing ID field.
- Eligibility for a Certificate of Good Standing is determined by the WY SOS site itself, not pre-filtered by the system. The system always attempts the certificate page regardless of standing values (including null standings). If the WY SOS site rejects the request (e.g., entity not eligible), the system handles the rejection gracefully.
- The certificate PDF download is triggered by clicking the "Print" button on the verification page, which produces a file named `CertOfGoodStanding.pdf` via a browser download event.
- The existing cloud storage infrastructure (Azure Blob Storage, `IBlobStorageClient`) from feature 003 is reused for certificate storage.
- The existing `CertificateResult` model from feature 004 is reused without modification for WY certificates.
- The certificate flow can share the same Playwright browser context as the entity details scrape, avoiding additional session setup or CAPTCHA resolution.

## Clarifications

### Session 2026-03-26

- Q: How should the system determine certificate eligibility when one or more standing fields is null? → A: Always attempt the certificate page regardless of standing values (including nulls). Let the WY SOS site determine eligibility.
- Q: Should certificate retrieval share the entity details request timeout or have its own? → A: Independent 15-second timeout for certificate retrieval, separate from the entity details scrape timeout.
- Q: Should the system verify that the entity shown on the certificate verification page matches the scraped entity before clicking Print? → A: No. Skip verification and click Print directly. The Filing ID lookup is deterministic.
- Q: What should `CertificateResult.Available` mean for WY entities (no inline link on details page)? → A: `Available` = `true` only when the certificate PDF was successfully downloaded. `false` if the WY SOS site rejects the request or the download fails.
