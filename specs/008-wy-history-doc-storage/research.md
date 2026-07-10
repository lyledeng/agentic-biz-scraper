# Research: WY History Document Cloud Storage

**Feature**: 008-wy-history-doc-storage  
**Date**: 2026-03-26

## R1: Playwright Document Download via Browser Context

**Decision**: Use Playwright's `page.GotoAsync()` + download interception or `page.Context.Request` to download PDF documents from WY SOS `GetImages.aspx` URLs using the active browser session.

**Rationale**: The WY SOS `GetImages.aspx` URLs require session cookies established during the entity details page navigation. Direct HTTP calls (e.g., Flurl) would not carry these cookies. Playwright's browser context inherits all session state from the navigation that loaded the entity details page.

**Implementation approach**:
- After extracting history documents from the entity details page, iterate through each document's `storageUrl`.
- Use the Playwright page to navigate to each document URL and intercept the download response.
- Playwright's `page.Context.NewPageAsync()` or route interception can capture the download stream.
- Alternative: Use `page.Context.APIRequestContext` which automatically includes browser cookies for same-origin requests.
- The simplest approach: Use `IBrowserContext.APIRequestContext.GetAsync(url)` which sends cookies from the browser context.

**Alternatives considered**:
- Direct HTTP via Flurl: Rejected — would require manual cookie extraction from Playwright, fragile and error-prone.
- Hybrid approach: Rejected — unnecessary complexity when Playwright API request context handles it cleanly.

## R2: Sequential Download Strategy

**Decision**: Download documents one at a time in listed order.

**Rationale**: Sequential processing is simpler, more predictable on browser resource usage, and easier to debug. Given the single Playwright browser context per request and typically small documents (< 10 MB PDFs), sequential processing avoids overwhelming the WY SOS server. The existing semaphore already limits concurrent entity detail requests to 3, providing sufficient throughput protection.

**Alternatives considered**:
- Parallel download with bounded concurrency (e.g., 3 at a time): Rejected — adds complexity, potential browser context contention, and rate-limiting risk at WY SOS.
- Batched download (e.g., 3 then 3): Rejected — no measurable benefit for the expected document count (typically 1-10).

## R3: Local-First Save Then Upload Pattern

**Decision**: Save each downloaded document to the local per-request diagnostics folder first, then upload to Azure Blob Storage from the local file.

**Rationale**: This matches the existing pattern from feature 003 (screenshots/traces saved locally, then uploaded). Benefits: local copies retained for debugging, upload can retry from local file without re-downloading, consistent behavior across all diagnostics artifacts.

**Implementation approach**:
- Local path: `{diagnosticsOutputPath}/{sanitizedCorrelationId}/{fileName}`
- Blob path: `{sanitizedCorrelationId}/{fileName}`
- After local save, call `IBlobStorageClient.UploadAsync(blobPath, localFilePath, cancellationToken)`
- On upload success, replace `storageUrl` with the cloud storage URL
- On upload failure after retries, keep original WY SOS URL and populate `error` field

**Alternatives considered**:
- Stream directly from Playwright to blob storage (skip local): Rejected — loses local copy for debugging, cannot retry upload without re-downloading.
- Save locally, upload, then delete local: Rejected — user wants local copies retained.

## R4: Removing the 3-Document Limit from WY Entity Details Flow

**Decision**: Modify the `wy-entity-details.json` extraction JavaScript to remove the `docs.length >= 3` guard, allowing ALL history documents to be extracted.

**Rationale**: The spec explicitly requires no hard-coded limit. The current 3-document limit was a design choice in spec 007 that is superseded by spec 008's requirement to download all documents.

**Implementation**:
- In `wy-entity-details.json`, remove the line `if (docs.length >= 3) break;` from the history document extraction JavaScript.
- The rest of the extraction logic (parsing event title, date, URL, filename) remains unchanged.

**Alternatives considered**:
- Adding a configurable limit: Rejected — spec explicitly says no limit.

## R5: Content-Type Validation

**Decision**: Validate that each downloaded document has a PDF content type before saving and uploading.

**Rationale**: The WY SOS site may return HTML error pages instead of PDFs if a document is no longer available or the session has expired. Uploading HTML content as a `.pdf` file would be misleading.

**Implementation**:
- Check the response `Content-Type` header for `application/pdf`
- If not PDF, log a warning, set the `error` field, and skip local save + upload

**Alternatives considered**:
- Check file magic bytes: More robust but overkill — content-type header is sufficient for this use case.

## R6: Audit Trail Extension

**Decision**: Add a `DocumentStorageUrl` column (NVARCHAR(2000), nullable) to the `EntityDetailAuditEntries` table.

**Rationale**: Consistent with the existing `DiagnosticsUrl` and `CertificateUrl` columns. Gives operators a direct link to the cloud storage folder containing the downloaded documents.

**Implementation**:
- Liquibase changelog: `2.0.2603.4-add-document-storage-url-to-entity-detail-audit.sql`
- Add `ALTER TABLE dbo.EntityDetailAuditEntries ADD DocumentStorageUrl NVARCHAR(2000) NULL;`
- Rollback: `ALTER TABLE dbo.EntityDetailAuditEntries DROP COLUMN DocumentStorageUrl;`
- Update `EntityDetailAuditEntry` entity with new property
- Update `EntityDetailAuditRepository.ConfigureSaveCommand` to include the new parameter
- Update handler's `WriteAuditAsync` calls to pass the document storage URL

## R7: Configuration Toggle

**Decision**: Reuse the existing `CloudStorage.Enabled` toggle from `DiagnosticsOptions`. No separate toggle for document upload.

**Rationale**: The document upload feature uses the same Azure Blob Storage infrastructure as diagnostics. If cloud storage is disabled, documents stay with their original WY SOS URLs (which is the disabled behavior described in the spec).

**Alternatives considered**:
- Separate `DocumentUpload.Enabled` toggle: Rejected — over-engineering. The existing toggle controls all cloud upload behavior. If a separate toggle is needed later, it can be added incrementally.
