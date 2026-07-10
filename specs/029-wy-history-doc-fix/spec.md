# Feature Specification: WY Filing History Document Download Fix

**Feature Branch**: `029-wy-history-doc-fix`  
**Created**: 2025-07-24  
**Status**: Draft  
**Input**: User description: "WY Filing History documents are not being downloaded"

## Problem Statement

When a user views entity details for a Wyoming (WY) business, the filing history section lists documents with download links. Clicking these links fails because the system returns the original source website URLs directly to the user's browser. These URLs require authenticated session cookies from the scraping session that the user does not have, making the documents inaccessible.

The root cause is a gap in the document handling pipeline: WY filing history documents are downloaded during scraping (using the scraper's authenticated session) and saved locally for diagnostics, but they are never uploaded to cloud storage. As a result, the download references returned to the user point to the original source URLs instead of proxied cloud storage URLs.

Other document types (e.g., certificates, DE entity documents) already follow the correct pattern: download from source → upload to cloud storage → return a proxy URL. WY filing history documents need to follow this same pattern.

## Clarifications

### Session 2026-04-03

- Q: Should the blob upload step be implemented generically in the entity detail scraper (all states) or as WY-specific post-processing? → A: Generic — upload in the entity detail scraper so all states benefit automatically.
- Q: Should history documents be uploaded sequentially or in parallel? → A: Sequential — one at a time, matching the existing download loop pattern.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View and Download WY Filing History Documents (Priority: P1)

A user searches for a Wyoming business entity and opens its detail page. The filing history section displays a list of historical filings (e.g., annual reports, amendments, articles of incorporation). Each filing has a "Filing Document" download link. When the user clicks a download link, the PDF document opens or downloads successfully.

**Why this priority**: This is the core bug fix. Without it, filing history documents are completely inaccessible to users, which is the primary value of the WY entity details feature.

**Independent Test**: Search for a WY business entity with known filing history, open entity details, click any filing document link, and confirm the PDF renders or downloads.

**Acceptance Scenarios**:

1. **Given** a WY entity detail page with filing history documents, **When** the user clicks a filing document link, **Then** the PDF document loads successfully in the document viewer or downloads to the user's device.
2. **Given** a WY entity detail page with multiple filing history entries, **When** the user clicks different document links in sequence, **Then** each document loads independently and correctly.
3. **Given** a WY entity detail page, **When** the user views the filing history section, **Then** all document download links point to proxied cloud storage URLs (not raw source website URLs).

---

### User Story 2 - Graceful Handling of Unavailable Documents (Priority: P2)

When a filing history document cannot be retrieved from the source (e.g., the source returns an error, the document is empty, or the download times out), the user sees a clear, informative error message instead of a broken link or a cryptic failure.

**Why this priority**: Some documents on the source website may be temporarily unavailable or malformed. Users need clear feedback so they know the issue is with the source and not the system.

**Independent Test**: Simulate a filing history document that returns an error from the source website and confirm the user sees an appropriate error message.

**Acceptance Scenarios**:

1. **Given** a filing history document that cannot be downloaded from the source, **When** the user views the entity details, **Then** the document entry displays a descriptive error message (e.g., "Document unavailable" or "Download timed out").
2. **Given** a filing history document that downloads as empty (zero bytes), **When** the system processes the document, **Then** the document entry displays an error message rather than presenting a broken download link.
3. **Given** multiple filing history documents where some succeed and some fail, **When** the user views the entity details, **Then** successful documents are downloadable and failed documents show individual error messages without affecting each other.

---

### User Story 3 - Documents Stored in Cloud for Consistent Access (Priority: P3)

Filing history documents, once successfully downloaded from the source, are stored in cloud storage and served through the system's document proxy. This ensures consistent, reliable access and decouples user access from the availability of the source website.

**Why this priority**: Cloud storage provides durability and consistent access patterns. This is implicitly required by the proxy URL approach but is a distinct user benefit worth specifying.

**Independent Test**: Download a WY filing document via the UI, then verify the document is accessible through its proxy URL independently of the source website.

**Acceptance Scenarios**:

1. **Given** a successfully downloaded filing history document, **When** the system completes entity detail retrieval, **Then** the document is stored in cloud storage under the appropriate location for the request.
2. **Given** a stored filing history document, **When** the user accesses it via the proxy URL, **Then** the document is served with the correct content type and is a valid PDF.

### Edge Cases

- What happens when the source website returns a non-PDF content type (e.g., HTML error page) instead of the document? The system MUST detect this and report an error rather than storing a non-PDF file.
- What happens when a document download times out? The system MUST record a timeout error on the individual document and continue processing remaining documents.
- What happens when the cloud storage upload fails after a successful download? The system MUST report an error for that document rather than returning a broken proxy URL.
- What happens when a filing history entry has no download URL? The system MUST pass through the entry without error, displaying the filing metadata (title, date) without a download link.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST upload each successfully downloaded WY filing history document to cloud storage after downloading it from the source.
- **FR-002**: System MUST replace the original source URL with a proxy URL for each document that was successfully uploaded to cloud storage.
- **FR-003**: System MUST preserve the existing error handling for individual document download failures (content type validation, empty body detection, timeout handling).
- **FR-004**: System MUST continue processing remaining documents if a single document upload to cloud storage fails, recording an error only for the failed document.
- **FR-005**: System MUST store each document under a path that includes the request correlation identifier and the original file name, consistent with the existing cloud storage path conventions.
- **FR-006**: System MUST serve uploaded filing history documents through the existing document proxy endpoint with the correct PDF content type.
- **FR-007**: System MUST retain the local diagnostic copy of downloaded documents (existing behavior) in addition to uploading to cloud storage.
- **FR-008**: The upload-to-cloud-storage step MUST be implemented generically in the entity detail scraper so that any state's history documents are automatically uploaded without per-state post-processing.
- **FR-009**: History document uploads MUST proceed sequentially (one at a time), consistent with the existing per-document download loop.

### Key Entities

- **Filing History Document**: A PDF document associated with a historical filing event for a WY business entity. Has attributes: event title, filing date, file name, download URL (source or proxy), and optional error message.
- **Document Proxy URL**: A system-generated URL that serves a cloud-stored document through the system's authenticated document proxy endpoint, decoupling user access from the source website.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of successfully downloaded WY filing history documents are accessible to users via their download links (zero broken links for documents that were available at the source).
- **SC-002**: Users can open any available WY filing history document within the existing document viewer without encountering authentication or access errors.
- **SC-003**: Documents that fail to download from the source display a meaningful error message, with zero silent failures or broken links.
- **SC-004**: The document download and storage process completes within the existing per-document timeout window, adding no perceptible delay to the overall entity detail retrieval.

## Assumptions

- The existing document proxy endpoint and cloud storage infrastructure are available and functioning correctly (proven by certificate and DE entity document downloads).
- WY filing history documents are PDF files served by the source website's document retrieval endpoint.
- The scraper's authenticated browser session provides access to the source document URLs at the time of entity detail retrieval.
- The cloud storage upload pattern (download → upload → proxy URL) already used for other document types is the correct approach for WY filing history documents.
- The upload step is implemented generically in the entity detail scraper; while the immediate motivation is WY, any state using the JSON-driven entity detail pattern with history documents will automatically benefit.
