# Feature Specification: WY History Document Cloud Storage

**Feature Branch**: `008-wy-history-doc-storage`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "Download all WY entity detail history documents and upload them to cloud storage account, replacing WY SOS URLs with cloud storage URLs in the response."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Download and Upload All WY History Documents (Priority: P1)

As an API consumer, I want all history documents listed on a Wyoming entity's details page to be downloaded from the WY Secretary of State site and uploaded to our cloud storage account, so that I have persistent, reliable access to those documents without depending on the WY SOS site's availability.

**Why this priority**: This is the core feature. Without downloading and reuploading history documents, consumers have no reliable access to WY entity filing documents — the WY SOS URLs may be session-bound, rate-limited, or unavailable after the scraping session ends.

**Independent Test**: Can be fully tested by calling the entity details endpoint with `state=WY` for an entity that has history documents, and verifying each document's `storageUrl` in the response points to the cloud storage account rather than the WY SOS site.

**Acceptance Scenarios**:

1. **Given** a WY entity details page lists 3 history documents, **When** the entity details endpoint is called with `state=WY`, **Then** all 3 documents are downloaded from the WY SOS site and uploaded to the cloud storage account.
2. **Given** a WY entity details page lists 10 or more history documents, **When** the entity details endpoint is called, **Then** all documents are downloaded and uploaded — there is no artificial limit on the number of documents processed.
3. **Given** history documents are successfully uploaded, **When** the response is returned, **Then** each history document's `storageUrl` field contains the cloud storage URL (not the original WY SOS URL).
4. **Given** the original WY SOS document URL, **When** it is downloaded, **Then** the document is downloaded and saved without excessive memory allocation. Playwright's `APIRequestContext` buffers the response body in memory, which is acceptable given typical document sizes (< 10 MB per annual report PDF).

---

### User Story 2 - Organize Documents in Per-Request Folders (Priority: P1)

As an operator, I want history documents stored in cloud storage to be organized in a folder structure tied to the request's correlation ID, so that I can locate documents and trace them back to specific API requests.

**Why this priority**: Without organized storage, documents from different requests would be mixed together, making retrieval and audit difficult. This directly supports the existing per-request folder convention from feature 003.

**Independent Test**: Can be tested by triggering a WY entity details request and verifying that the uploaded blobs in cloud storage are located under a path that includes the correlation ID.

**Acceptance Scenarios**:

1. **Given** a WY entity details request with a correlation ID, **When** documents are uploaded, **Then** blobs are stored under the path `{correlationId}/{fileName}` within the configured container.
2. **Given** the document filename from the WY SOS scraping response (e.g., `2025-original-annual-report-11347360.pdf`), **When** the document is uploaded, **Then** the blob name preserves the original filename.

---

### User Story 3 - Handle Document Download Failures Gracefully (Priority: P2)

As an API consumer, I want the system to handle individual document download or upload failures without failing the entire entity details response, so that I still receive entity details and any successfully processed documents.

**Why this priority**: Resilience is critical for production use but the core happy-path download must work first.

**Independent Test**: Can be tested by simulating a failed document download (e.g., a document URL that returns 404) and verifying the response still contains entity details and the failing document has an error field populated.

**Acceptance Scenarios**:

1. **Given** 3 history documents are listed and 1 fails to download (network error or HTTP error), **When** the response is returned, **Then** the 2 successfully uploaded documents have cloud storage URLs and the failed document retains an error message describing the failure.
2. **Given** a document download fails, **When** the failure is logged, **Then** the log entry includes the correlation ID, document filename, and error details.
3. **Given** all document downloads fail, **When** the response is returned, **Then** the entity details are still returned with each history document entry containing an error message.

---

### User Story 4 - Persist Document Storage URLs to Audit Trail (Priority: P2)

As an operator reviewing past requests, I want the audit trail to record that history documents were uploaded and where they are stored, so that I can trace document artifacts back to specific API requests.

**Why this priority**: Audit traceability complements the core download feature but is not required for basic functionality.

**Independent Test**: Can be tested by performing a WY entity details request, then querying the audit trail and verifying it contains a reference to the cloud storage location for the history documents.

**Acceptance Scenarios**:

1. **Given** a WY entity details request that downloads history documents, **When** the audit trail entry is written, **Then** the entry includes the cloud storage folder URL where documents were stored.
2. **Given** cloud upload is disabled, **When** the audit trail entry is written, **Then** the document storage URL field is null.

---

### Edge Cases

- What happens when a WY entity has no history documents? The `historyDocuments` array in the response is empty. No download or upload is attempted.
- What happens when the cloud storage account is unreachable? Document upload failures are logged, the response includes the original WY SOS URLs with an error note for each affected document, and the primary entity details response is still returned.
- What happens when a downloaded document is zero bytes or corrupt? The system logs a warning, skips the upload for that document, and populates the error field for that entry.
- What happens when a WY document URL returns a non-PDF response (e.g., an HTML error page)? The system validates the content type of the downloaded response. If it is not a PDF, the system logs a warning, skips upload, and populates the error field.
- What happens when two concurrent requests download documents for the same entity? Each request uses its own correlation ID as the folder prefix, so documents are stored independently without conflicts.
- What happens when the document filename contains special characters? The filename is sanitized for cloud storage compatibility using the same rules applied to other blob names in the existing cloud storage infrastructure.
- What happens when a document download times out? The per-document download timeout is enforced; the system logs a warning, populates the error field, and continues with remaining documents.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST download ALL history documents listed on the WY entity details page sequentially (one at a time, in listed order). There MUST be no hard-coded limit on the number of documents processed.
- **FR-002**: Each downloaded history document MUST be uploaded to the configured Azure Blob Storage container under the path `{correlationId}/{fileName}`.
- **FR-003**: After successful upload, the `storageUrl` field in the response for each history document MUST be replaced with the cloud storage blob URL. The original WY SOS URL MUST NOT appear in the response for successfully uploaded documents.
- **FR-004**: Document downloads MUST be performed using the active Playwright browser context that navigated the entity details page, to inherit session cookies required by the WY SOS `GetImages.aspx` endpoints. Each document MUST be saved locally first to the per-request diagnostics folder (`{correlationId}/{fileName}`), then uploaded to cloud storage from the local file. Local copies MUST be retained after upload, consistent with the diagnostics persistence pattern from feature 003.
- **FR-005**: If a document download fails (network error, HTTP error, timeout), the system MUST log a warning, populate the `error` field on that history document entry, and continue processing remaining documents. The entity details response MUST NOT fail.
- **FR-006**: If a document upload to cloud storage fails after successful download, the system MUST retry up to 3 times with exponential backoff. If all retries fail, the `error` field MUST be populated and the `storageUrl` MUST retain the original WY SOS URL as a fallback.
- **FR-007**: The system MUST validate the content type of each downloaded document. If the response is not a PDF (e.g., HTML error page), the system MUST skip the upload, log a warning, and populate the `error` field.
- **FR-008**: The system MUST persist the cloud storage folder URL for the uploaded documents to the audit trail entry for the request.
- **FR-009**: The system MUST reuse the existing cloud storage infrastructure (connection configuration, blob client, container auto-creation, retry policies) established by the cloud screenshot storage feature (003).
- **FR-010**: The system MUST support disabling document download and upload via configuration. When disabled, the response returns the original WY SOS URLs unchanged.
- **FR-011**: Document filenames in cloud storage MUST match the filename provided from the WY SOS scraping response (e.g., `2025-original-annual-report-11347360.pdf`).
- **FR-012**: The system MUST apply a per-document download timeout to prevent slow WY SOS responses from blocking the entire request.

### Key Entities

- **HistoryDocumentResult** (existing, extended behavior): The existing history document model from spec 007 with fields `eventTitle`, `date`, `storageUrl`, `fileName`, and `error`. The `storageUrl` field is updated to contain the cloud storage URL after successful upload. The `error` field is populated when download or upload fails.
- **Document Storage Path**: The blob path `{correlationId}/{fileName}` within the configured Azure Blob Storage container.
- **Audit Trail Extension**: The existing audit trail gains document storage folder URL tracking, consistent with the cloud screenshot/diagnostics storage pattern from spec 003.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All history documents listed on a WY entity details page are downloaded and available in cloud storage after a successful entity details request.
- **SC-002**: Response `storageUrl` fields point to cloud storage URLs for all successfully uploaded documents.
- **SC-003**: Individual document download or upload failures do not prevent the entity details response from being returned.
- **SC-004**: Document download and upload completes within 30 seconds per document under normal network conditions.
- **SC-005**: Existing CO entity details and WY entity details functionality (fields, CAPTCHA, screenshots, diagnostics) remain unaffected.

## Assumptions

- The existing cloud storage infrastructure from feature 003 (Azure Blob Storage client, connection string from Key Vault, container auto-creation, retry policies) is in place and reusable.
- The WY SOS document URLs (`GetImages.aspx` endpoints) return PDF content directly and require the active Playwright browser session context (session cookies) established during entity details page navigation. Direct HTTP calls without session cookies are not expected to work.
- The existing `HistoryDocumentResult` model from spec 007 already has `storageUrl`, `fileName`, and `error` fields that can be reused without structural changes.
- The correlation ID is already propagated through the WY entity details flow and is available at the point where document download occurs.
- Document sizes are reasonable (typically under 10 MB per annual report PDF) and do not require chunked upload or special large-blob handling.
- The existing per-request folder convention (`{correlationId}/`) from feature 003 applies to document storage as well.
- History document download and cloud upload is scoped to WY entities only. CO entity details do not have a history documents section; extending this feature to CO would be a separate effort if ever needed.
- No additional diagnostic screenshots are required during the document download phase. The existing entity details page screenshots (from spec 007: page load, CAPTCHA, details extraction) provide sufficient context. Download failures are captured via structured logging with correlation ID and error details.

## Clarifications

### Session 2026-03-26

- Q: How should history documents be downloaded from the WY SOS site — via Playwright browser context, direct HTTP, or hybrid? → A: Download via the active Playwright browser context to inherit session cookies from the entity details page navigation.
- Q: Should history documents be downloaded sequentially or concurrently? → A: Sequential — download one document at a time in listed order.
- Q: Should downloaded history documents be saved locally on the pod filesystem in addition to cloud storage? → A: Yes — save locally first (like screenshots), then upload to cloud; retain local copies.
- Q: Should this feature extend to CO entity history documents or is it WY-only? → A: WY-only; CO history documents are out of scope.
- Q: Should diagnostic screenshots be captured during the document download phase? → A: No extra screenshots; existing entity details page screenshots are sufficient.
