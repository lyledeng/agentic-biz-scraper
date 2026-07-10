# Feature Specification: WY Certificate of Good Standing Fix

**Feature Branch**: `020-wy-cert-good-standing-fix`  
**Created**: 2026-03-31  
**Status**: Draft  
**Input**: User description: "WY Business Entity Search Details is not pulling the Certificate of Good Standing. When pulling the details, on the left pane select 'Good Standing Certificates Online'. Enter the filing ID of the entity and click search. On the details click print to download the certificate of good standing."

## Problem Statement

The WY entity details endpoint (formerly `/api/v1/entity-details?state=WY`, now `/api/v2/execute-script`) does not return the Certificate of Good Standing for eligible entities. The v1 endpoint is deprecated and will be removed in this feature branch. The certificate flow was implemented in feature 009 and the `us-wy-entity-details.json` definition includes certificate group actions (actions 014–017), but the certificate PDF is never successfully downloaded in production.

### Root Cause Analysis (from live site exploration)

The WY Secretary of State Certificate of Good Standing page (`https://wyobiz.wyo.gov/Business/ViewCertificate.aspx`) has two sections:

1. **Generate** — Enter a Filing ID to look up and print a certificate
2. **Validate** — Enter a Certificate ID to validate an existing certificate

The current scraping flow navigates to the page, fills the Filing ID, clicks Search, and then attempts to download from the Print button (`#MainContent_ContinueButton`). Issues identified:

- **Missing condition on the download action**: The CO certificate flow uses an `element-exists` condition before attempting download. The WY flow does not — when the Print button is absent (entity not in good standing), the download handler silently returns and the response indicates no certificate, but no explicit "not eligible" reason is captured.
- **No detection of "not eligible" alert**: When an entity is not in good standing, the WY SOS site displays an alert: *"Certificates may only be printed when the entity Standing is 'Good'."* The current flow does not detect or report this.
- **The downloaded filename from WY SOS is `CertOfGoodStanding.pdf`**: The definition uses `filenamePattern: "${idNumber}-certificateofgoodstanding.pdf"` which renames it correctly, but doesn't account for the server-supplied filename in validation.

### Verified Flow (live Playwright exploration, 2026-03-31)

**Entity in good standing** (Filing ID `2017-000757675` — Wendy's Technology, LLC):
1. Navigate to `ViewCertificate.aspx`
2. Enter Filing ID in the Generate section textbox (`#MainContent_txtFilingID`)
3. Click Search button (`#MainContent_SearchButton`)
4. Verification page shows entity details (Name, Filing ID, Type, Status, Standings)
5. Print button (`#MainContent_ContinueButton`, `<input type="submit">`) is present
6. Clicking Print triggers a download of `CertOfGoodStanding.pdf`

**Entity NOT in good standing** (Filing ID `2015-000679087` — Wendy S. Jacobs, LLC):
1. Steps 1–3 same as above
2. Verification page shows entity details with Standing - Tax = "Delinquent"
3. **No Print button** — instead, an alert box appears: *"Certificates may only be printed when the entity Standing is 'Good'. Please contact the Business Division at Business@wyo.gov."*

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Download Certificate for Eligible WY Entity (Priority: P1)

As an API consumer, I want the WY entity details response to include a Certificate of Good Standing PDF when the entity is eligible, so that I receive the certificate automatically without visiting the WY SOS website.

**Why this priority**: This is the core bug fix — the certificate flow exists but does not successfully deliver the PDF. Without this fix, the `certificate` field is always empty for WY entities.

**Independent Test**: Call `POST /api/v2/execute-script` with the WY entity details script for Filing ID `2017-000757675` (Wendy's Technology, LLC — all standings "Good"). Verify the response `certificate` field contains `available: true`, a valid `storageUrl`, and a `fileName` ending in `certificateofgoodstanding.pdf`.

**Acceptance Scenarios**:

1. **Given** a WY entity in good standing (Filing ID `2017-000757675`), **When** the entity details endpoint is called, **Then** the certificate flow navigates to ViewCertificate.aspx, enters the Filing ID, clicks Search, clicks Print on the verification page, downloads the PDF, and returns `certificate.available = true` with a valid storage URL.
2. **Given** a WY entity in good standing and cloud storage is enabled, **When** the certificate is downloaded, **Then** the PDF is uploaded to blob storage and the response `certificate.storageUrl` points to the uploaded file.
3. **Given** a WY entity in good standing, **When** the certificate flow completes, **Then** diagnostic screenshots are captured at: certificate search page loaded, verification page displayed, and after Print/download.

---

### User Story 2 - Report Ineligibility for Non-Good-Standing Entities (Priority: P2)

As an API consumer, I want the response to clearly indicate when a WY entity is not eligible for a Certificate of Good Standing, so that I know the certificate was attempted but the entity does not qualify.

**Why this priority**: Distinguishing "certificate not attempted" from "entity not eligible" is important for downstream consumers. Currently both cases result in an empty certificate field with no explanation.

**Independent Test**: Call `POST /api/v2/execute-script` with the WY entity details script for Filing ID `2015-000679087` (Wendy S. Jacobs, LLC — Standing Tax = "Delinquent"). Verify the response `certificate` field contains `available: false` with an `error` message indicating ineligibility.

**Acceptance Scenarios**:

1. **Given** a WY entity with Standing - Tax = "Delinquent" (Filing ID `2015-000679087`), **When** the entity details endpoint is called, **Then** the certificate flow navigates to ViewCertificate.aspx, enters the Filing ID, clicks Search, detects the missing Print button and the "not eligible" alert, and returns `certificate.available = false` with an error indicating the entity is not in good standing.
2. **Given** a WY entity not in good standing, **When** the certificate flow detects ineligibility, **Then** the entity details (name, status, registered agent, parties, history documents) are still returned without interruption.
3. **Given** a WY entity not in good standing, **When** the verification page displays the ineligibility alert, **Then** a screenshot is captured of the verification page showing the alert message.

---

### User Story 3 - Graceful Failure for Certificate Flow Errors (Priority: P3)

As an API consumer, I want the entity details to be returned even if the certificate flow encounters unexpected errors (timeouts, page errors, network failures), so that a certificate issue never blocks entity details retrieval.

**Why this priority**: Resilience is essential for production, but the core download and ineligibility detection must work first.

**Independent Test**: Simulate a certificate page timeout (e.g., by pointing to an unreachable host) and verify the entity details response is returned with `certificate.error` populated.

**Acceptance Scenarios**:

1. **Given** a WY entity in good standing but the certificate page times out, **When** the entity details endpoint is called, **Then** entity details are returned with `certificate.available = false` and `certificate.error` describing the timeout.
2. **Given** a WY entity in good standing but the certificate download fails, **When** the entity details endpoint is called, **Then** entity details are returned with `certificate.available = false` and `certificate.error` describing the download failure.
3. **Given** a WY entity details request with an invalid Filing ID format, **When** the certificate flow is evaluated, **Then** the certificate flow is skipped entirely and the entity details are returned without a certificate field.

---

### Edge Cases

- What happens when the Filing ID returns no results on the certificate page? The system captures the error state and returns `certificate.available = false` with an appropriate error.
- What happens when the WY SOS certificate page is temporarily down? The certificate flow times out (15-second budget), and entity details are returned without a certificate.
- What happens when the Search button triggers a page load but the verification page content differs from expected? The download action checks for the Print button via its selector; if absent, the download is skipped.
- What happens when cloud storage upload fails after a successful PDF download? Entity details are returned with `certificate.available = true` (PDF was downloaded) but `certificate.error` describes the upload failure.
- What happens when the downloaded file is not a PDF? The download handler validates the file extension and discards non-PDF files, returning `certificate.available = false`.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The WY certificate flow in `us-wy-entity-details.json` MUST add an `element-exists` condition on the download action (action-017) to check for the Print button (`#MainContent_ContinueButton`) before attempting the download, consistent with the CO certificate flow pattern.
- **FR-002**: The WY certificate flow MUST detect the "not eligible" alert displayed when the entity is not in good standing. When the alert is present and the Print button is absent, the system MUST capture the reason and report `certificate.available = false` with a descriptive error.
- **FR-003**: The WY certificate flow MUST add an extraction action after the Search click (action-016) to detect whether the Print button is present or the ineligibility alert is shown, and store an eligibility indicator for downstream use.
- **FR-004**: The download action MUST use the existing `DownloadActionHandler` which supports clicking both links and buttons (`<input type="submit">`). No changes to the download handler are needed.
- **FR-005**: The `us-wy-entity-details.json` definition MUST be reviewed and updated to ensure action ordering is correct and unambiguous — actions with duplicate `order` values (e.g., action-009 and action-009b both at order 9) MUST be assigned distinct, sequential order values.
- **FR-006**: The certificate flow MUST capture a screenshot of the verification page regardless of whether the Print button is present, to support diagnostics for both eligible and ineligible entities.
- **FR-007**: The certificate flow MUST continue to use the 15-second independent timeout. Certificate timeouts MUST NOT cancel the parent entity details request.
- **FR-008**: Entity details (name, status, registered agent, parties, history documents) MUST always be returned regardless of certificate flow outcome — certificate retrieval is supplementary.
- **FR-009**: The `filenamePattern` in the download action MUST remain `${idNumber}-certificateofgoodstanding.pdf` to consistently name certificates by Filing ID, regardless of the server-supplied filename (`CertOfGoodStanding.pdf`).
- **FR-010**: The deprecated `/api/v1/business-search` endpoint MUST be fully removed in this feature branch — it serves CO and WY only (DE was never available via v1 business-search). If a 410 Gone tombstone or residual v1 route registration for entity-details still exists, it MUST also be removed. All requests MUST use the `/api/v2/execute-script` endpoint with the JSON-driven scraping engine. **Note**: The shared `Features/EntityDetails/` code (models, logging, metrics, `IEntityDetailScraper`, `JsonDrivenEntityDetailScraper`) is used by the v2 execute-script flow and MUST be retained — only the v1 route/endpoint registration is removed.
- **FR-011**: All existing v1 integration and E2E tests that reference `/api/v1/business-search` MUST be migrated to call `/api/v2/execute-script` with the equivalent JSON definition payload (`us-co-business-search`, `us-wy-business-search`). No v1 test coverage may be deleted without equivalent v2 replacement. **Note**: v1 entity-details tests were already removed in spec 018 — no entity-details test migration is needed.
- **FR-012**: After v1 removal, the `/api/v2/execute-script` endpoint MUST be validated end-to-end for all three state entity-details definitions: `us-co-entity-details.json`, `us-wy-entity-details.json`, and `de-de-entity-details.json`. Each MUST return entity details correctly via the JSON-driven scraping engine.

### Key Entities

- **CertificateResult** (existing): Represents the certificate download outcome — `Available`, `StorageUrl`, `FileName`, `Error`. Currently always empty for WY; this fix populates it correctly.
- **WY Entity Details Flow Definition** (`us-wy-entity-details.json`): The existing JSON-driven scraping flow with certificate group actions that need correction.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: WY entity details requests for entities in good standing (e.g., Filing ID `2017-000757675`) return a populated `certificate` field with `available: true` and a valid storage URL.
- **SC-002**: WY entity details requests for entities not in good standing (e.g., Filing ID `2015-000679087`) return `certificate.available = false` with an error message indicating ineligibility.
- **SC-003**: Existing WY entity details functionality (details, registered agent, parties, history documents) continues to work identically regardless of certificate flow outcome.
- **SC-004**: Certificate flow diagnostic screenshots are captured at each milestone (search page, verification page, after download) and stored under the correlation ID.
- **SC-005**: All existing unit and integration tests for the WY entity details and certificate flow continue to pass.
- **SC-006**: All former v1 business-search tests (CO, WY) are migrated to `/api/v2/execute-script` and pass with equivalent assertions. (v1 entity-details tests were already removed in spec 018.)
- **SC-007**: Entity details requests via `/api/v2/execute-script` for CO (`us-co-entity-details.json`), WY (`us-wy-entity-details.json`), and DE (`de-de-entity-details.json`) all return correct entity details end-to-end after v1 removal.

## Clarifications

### Session 2026-03-31

- Q: Should this feature branch remove the deprecated v1 endpoint code entirely, or only update spec references to v2? → A: Remove v1 endpoint code entirely in this feature branch.
- Q: Should existing v1 integration/E2E tests be migrated to v2 or deleted? → A: Migrate existing v1 tests to use `/api/v2/execute-script`.
- Q: Should v1 endpoint removal apply to all states or only WY? → A: Remove the entire `/api/v1/entity-details` endpoint for all states (CO, WY, DE). Ensure `/api/v2/execute-script` works for all entity details.

## Assumptions

- The WY SOS website (`https://wyobiz.wyo.gov/Business/ViewCertificate.aspx`) page structure — Generate section with Filing ID textbox, Search button, verification page with Print button or ineligibility alert — remains stable.
- The `DownloadActionHandler` correctly handles `<input type="submit">` buttons (confirmed by code review — it uses `Locator.ClickAsync()` which works for any clickable element).
- The certificate flow reuses the same Playwright browser context as the entity details scrape and does not encounter a separate CAPTCHA on the certificate page (confirmed by live site exploration — no CAPTCHA on ViewCertificate.aspx).
- The `#MainContent_ContinueButton` selector for the Print button and `#MainContent_txtFilingID` / `#MainContent_SearchButton` selectors for the search form are stable ASP.NET server IDs.
- Cloud storage configuration and blob upload functionality are working correctly (the certificate flow issue is isolated to the scraping definition, not the storage layer).
