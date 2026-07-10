# Feature Specification: CO Entity Certified Document Download

**Feature Branch**: `034-co-entity-doc-download`  
**Created**: 2026-04-08  
**Status**: Draft  
**Input**: User description: "I want to enhance the CO SOS Entity Document download. When we are on the entity details page, if 'Get certified copies of documents' link is available, web scraper for co detail should click on that link. On the document list, take a screenshot and click on 'All documents' and download the pdf. Follow all conventions for doc downloads."

## Discovery Notes

Live exploration of the CO SOS website (entity: SCHOLER WENDYS COLORADO LLC, ID# 20201222644) confirmed a 3-page navigation flow: Entity Details → Certified Copies Selection (`CertificationCopiesSelect.do`) → Certificate Print (`CertificationPrint.do`). The PDF is NOT a direct download — it is served via a dynamically-generated `CertificationViewer.do` URL found in the "open your form in a new window" href on the Certificate Print page. The `certId` parameter is server-generated per request and cannot be predicted.

For full navigation details, selectors, and URL patterns, see [research.md — R-001](research.md#r-001-co-sos-certified-copies-navigation-flow).

## Clarifications

### Session 2026-04-08

- Q: How should the certified documents URL be integrated into the CO entity details response? → A: Add a new `certifiedDocumentsUrl` field as a flat sibling to `certificateUrl` in the output compose block. In the mapped response, it is added as a second `DownloadReference` entry in the existing `Certificate.Downloads` array with label "Certified Documents" (reuses existing types, no new model).
- Q: Which download strategy should the scraper use to obtain the PDF from the certificate print page? → A: Extract the `CertificationViewer.do` href from the "open your form in a new window" link, then fetch the PDF directly via the browser API request context (reuses session cookies), matching the WY history document download pattern.
- Q: Should the test UI be updated to show the new certified documents download? → A: Yes. Add a "Certified Documents" download button alongside the existing certificate download in the CO entity details view.
- Q: Should the certified documents download run before or after the certificate download, and does the scraper need to navigate back? → A: Run certified documents download AFTER certificate download (action-004). Certificate download doesn't navigate away from the details page, so certified documents actions append as new actions (action-005+). No back-navigation needed since it's the last step.

## User Scenarios & Testing *(mandatory)*

<!--
  IMPORTANT: User stories should be PRIORITIZED as user journeys ordered by importance.
  Each user story/journey must be INDEPENDENTLY TESTABLE - meaning if you implement just ONE of them,
  you should still have a viable MVP (Minimum Viable Product) that delivers value.
  
  Assign priorities (P1, P2, P3, etc.) to each story, where P1 is the most critical.
  Think of each story as a standalone slice of functionality that can be:
  - Developed independently
  - Tested independently
  - Deployed independently
  - Demonstrated to users independently
-->

### User Story 1 - Download All Certified Document Copies for a CO Entity (Priority: P1)

A user retrieves entity details for a Colorado business entity. When the entity details page on the CO SOS website contains a "Get certified copies of documents" link, the scraper automatically clicks that link, navigates to the document list page ("Certified Copies"), takes a screenshot of the document list for diagnostics, clicks the "All documents" link to navigate to the certificate page, extracts the PDF viewer URL from the "open your form in a new window" link, downloads the PDF, and stores it in cloud storage. The response includes a proxy URL for the downloaded document alongside the existing entity details and certificate of good standing.

**Why this priority**: This is the core feature — acquiring the full filing document history as a single PDF download. It extends the existing CO entity details scraper with a new document artifact that provides comprehensive filing records for the entity.

**Independent Test**: Call the entity details endpoint for a known CO entity that has the "Get certified copies of documents" link available (e.g., "SCHOLER WENDYS COLORADO LLC", ID# 20201222644). Verify the response includes a proxy URL for the downloaded "all documents" PDF, the PDF is valid, and a screenshot of the document list page was captured.

**Acceptance Scenarios**:

1. **Given** a CO entity details page with a "Get certified copies of documents" link, **When** the scraper processes the entity details, **Then** the scraper clicks the link, navigates to the document list page, and takes a screenshot of the document list.
2. **Given** the scraper is on the document list page, **When** an "All documents" link is available, **Then** the scraper clicks it, downloads the resulting PDF, and uploads the PDF to cloud storage.
3. **Given** the PDF has been uploaded to cloud storage, **When** the entity details response is returned, **Then** the response includes a proxy URL for the certified documents PDF alongside existing details.
4. **Given** the downloaded PDF is uploaded to cloud storage, **When** a user accesses the proxy URL, **Then** the PDF is served with the correct content type and is a valid, non-empty document.

---

### User Story 2 - Graceful Handling When Documents Are Unavailable (Priority: P2)

When the entity details page does not contain a "Get certified copies of documents" link (e.g., the entity is delinquent or dissolved and documents are not available), the scraper skips the document download step entirely. The entity details are still returned successfully without any document download reference.

**Why this priority**: Not all entities have certified document copies available. The scraper must handle this gracefully without failing the overall entity detail retrieval.

**Independent Test**: Call the entity details endpoint for a CO entity that does NOT have a "Get certified copies of documents" link. Verify entity details are returned successfully and the certified documents field is absent or null.

**Acceptance Scenarios**:

1. **Given** a CO entity details page WITHOUT a "Get certified copies of documents" link, **When** the scraper processes the entity details, **Then** the document download step is skipped entirely and no error is reported.
2. **Given** the document download step is skipped, **When** the entity details response is returned, **Then** the response contains all other entity detail fields (details, registered agent, certificate) without a certified documents URL.

---

### User Story 3 - Error Handling for Document Download Failures (Priority: P3)

When the "Get certified copies of documents" link is present but the subsequent document download fails (e.g., the "All documents" link is missing, the download times out, the response is not a valid PDF, or cloud storage upload fails), the scraper reports the error on the document download field without failing the overall entity details request.

**Why this priority**: Download failures should not prevent the user from receiving the successfully scraped entity details and certificate. Per-artifact error isolation is a core convention used across all document downloads.

**Independent Test**: Simulate a scenario where the document list page loads but the "All documents" download times out. Verify entity details are returned successfully with an error message on the certified documents field.

**Acceptance Scenarios**:

1. **Given** the document list page loads but the "All documents" link is not present, **When** the scraper processes the page, **Then** an error is recorded for the certified documents download and the remaining entity details are returned.
2. **Given** the "All documents" PDF download times out, **When** the scraper processes the download, **Then** a timeout error is recorded for the certified documents download and entity details are still returned.
3. **Given** the "All documents" download returns a non-PDF content type (e.g., an HTML error page), **When** the scraper validates the download, **Then** a content-type error is recorded and the invalid file is not uploaded to cloud storage.
4. **Given** the PDF is downloaded successfully but the cloud storage upload fails, **When** the scraper processes the upload, **Then** an error is recorded for the certified documents download without a broken proxy URL.

### Edge Cases

- What happens when the "Get certified copies of documents" link exists but clicking it results in a page load error or redirect? The scraper captures an error screenshot, records an error for the certified documents field, and continues returning entity details.
- What happens when the document list page loads but contains no documents? The scraper takes a screenshot, records that no documents were available, and returns entity details without a document download URL.
- What happens when the "All documents" PDF is empty (zero bytes)? The scraper detects the empty file, does not upload it to cloud storage, and records an error.
- What happens when the entity has both a certificate of good standing AND certified document copies? Both downloads proceed independently — the certificate download (existing behavior) and the certified documents download (new behavior) are separate steps that do not affect each other.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST check for the presence of a "Get certified copies of documents" link on the CO entity details page after extracting entity details.
- **FR-002**: When the link is present, system MUST click the "Get certified copies of documents" link and wait for the document list page to load.
- **FR-003**: System MUST capture a screenshot of the document list page for diagnostics.
- **FR-004**: System MUST click the "All documents" link on the document list page to navigate to the certificate page.
- **FR-005**: On the certificate page, system MUST extract the `CertificationViewer.do` URL from the "open your form in a new window" link's href attribute, then download the PDF via the browser API request context (e.g., `browserContext.APIRequest.GetAsync()`), which reuses session cookies from the navigation flow. This matches the existing WY history document download pattern.
- **FR-006**: System MUST validate that the downloaded file has a PDF content type and is non-empty before uploading to cloud storage.
- **FR-007**: System MUST save the downloaded PDF locally to the diagnostics folder AND upload it to cloud storage, consistent with existing document download conventions.
- **FR-008**: System MUST construct the filename using the pattern `${idNumber}-alldocuments.pdf` where `idNumber` is the entity's ID number extracted during entity detail scraping.
- **FR-009**: System MUST return a proxy URL for the uploaded document in the entity details response as a `certifiedDocumentsUrl` field, sibling to the existing `certificateUrl`, following the existing proxy URL pattern.
- **FR-010**: When the "Get certified copies of documents" link is NOT present, system MUST skip the document download step without error.
- **FR-011**: When any step of the document download flow fails (navigation, download, validation, upload), system MUST record an error on the certified documents result and continue returning the remaining entity details.
- **FR-012**: The certified documents download MUST NOT affect the existing certificate of good standing download — both steps operate independently.
- **FR-013**: System MUST store the uploaded document under the same blob path structure used by existing document downloads (date/definition-slug/entity-label/correlation-id).
- **FR-014**: The test UI MUST display a "Certified Documents" download button alongside the existing certificate download button in the CO entity details view, using the `certifiedDocumentsUrl` proxy URL.

### Key Entities

- **Certified Document Copy**: A combined PDF containing all filed documents for a CO business entity, generated by the CO SOS "All documents" function. Has attributes: storage URL (proxy), file name, and optional error message.
- **Document List Page**: An intermediate page on the CO SOS website that displays the list of filed documents for an entity, reached by clicking "Get certified copies of documents" from the entity details page.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For CO entities with a "Get certified copies of documents" link, the combined document PDF is successfully downloaded and accessible via its proxy URL 100% of the time when the source site is available.
- **SC-002**: The document list page screenshot is captured and stored in diagnostics for every entity where the "Get certified copies of documents" link is present.
- **SC-003**: Entity detail requests for entities without the "Get certified copies of documents" link complete without errors or degradation in response time.
- **SC-004**: Download failures for certified document copies never prevent the user from receiving entity details and certificate of good standing data.

## Assumptions

- The "Get certified copies of documents" link on the CO SOS entity details page navigates to a document list page (`CertificationCopiesSelect.do`) within the same CO SOS session context.
- The "All documents" link navigates to a certificate page (`CertificationPrint.do`) that embeds a PDF viewer; the actual PDF is accessible via the "open your form in a new window" link (`CertificationViewer.do`) which serves a multi-page "Certificate of Documents Filed" PDF combining all available documents.
- The document download requires the same browser session that navigated to the entity details page (session cookies are needed).
- The existing `us-co-entity-details` JSON scraping definition will be extended with additional actions for this flow, following the same action schema.
- Cloud storage infrastructure from feature 003 is already in place and will be reused.
- The existing proxy endpoint and authentication apply to the new document download.
- The document download timeout is governed by the existing document download timeout configuration.
