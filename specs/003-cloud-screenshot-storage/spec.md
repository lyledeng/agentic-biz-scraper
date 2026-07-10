# Feature Specification: Cloud Screenshot Storage & Per-Request Folder Organization

**Feature Branch**: `003-cloud-screenshot-storage`  
**Created**: 2026-03-24  
**Status**: Draft  
**Input**: User description: "I want to refine my screenshot saving strategy. I want to save the screenshots to a cloud storage account, all arranged in the foldername tied to the search request. I also want the screenshots saved locally to be organized in a foldername tied to the search request."

## Clarifications

### Session 2026-03-24

- Q: Should the cloud storage URL where screenshots are stored be persisted to the audit trail? → A: Yes. The cloud storage folder URL for the request's diagnostic artifacts must be added to the audit trail entry alongside the existing correlation ID.
- Q: Should the application auto-create the cloud storage container at startup if it doesn't exist, or require it to be pre-provisioned? → A: Auto-create the container at startup if it doesn't exist.
- Q: Should the system retry failed cloud uploads or treat them as single fire-and-forget attempts? → A: Retry up to 3 times with exponential backoff before giving up.
- Q: Which authentication method should be used for Azure Blob Storage? → A: Connection string only, stored in Key Vault for deployed environments and .NET User Secrets for local development.
- Q: Should local diagnostic artifacts be retained or deleted after successful cloud upload? → A: Always retain local copies; cleanup is an external operational concern.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Organize Local Screenshots in Per-Request Folders (Priority: P1)

As an operator triaging a scraping issue, I want all screenshots from a single search request grouped into a folder named after that request's correlation ID so that I can find and review them without sorting through a flat directory of hundreds of files.

**Why this priority**: This is the foundation that makes both local and cloud storage usable. Without per-request grouping, finding the right screenshots for a specific request requires scanning filenames. This change also establishes the folder-naming convention that cloud upload will reuse.

**Independent Test**: Trigger a search request. Confirm that all screenshots (milestone and error) are written into a subfolder named with the request's correlation ID under the configured local diagnostics output path, rather than into the flat root directory.

**Acceptance Scenarios**:

1. **Given** screenshot capture is enabled, **When** a search request completes, **Then** all screenshots for that request are saved in a subfolder named `{correlationId}` under the local diagnostics output path.
2. **Given** screenshot capture is enabled and the per-request subfolder does not yet exist, **When** the first screenshot for a request is captured, **Then** the subfolder is automatically created.
3. **Given** multiple concurrent search requests produce screenshots, **When** screenshots are saved, **Then** each request's screenshots are isolated in their own correlation-ID subfolder.
4. **Given** a trace file is produced for the same request, **When** the trace is saved, **Then** it is also placed in the same per-request subfolder alongside the screenshots.
5. **Given** screenshot capture is disabled but a failure occurs, **When** the forced error screenshot is captured, **Then** it is still placed in a per-request subfolder.

---

### User Story 2 — Upload Screenshots to Cloud Storage (Priority: P2)

As an operator, I want screenshots from each scraping session automatically uploaded to a cloud storage account so that I can access diagnostic artifacts from any environment without SSH or kubectl access to the pod filesystem.

**Why this priority**: Cloud storage enables centralized access to diagnostics across environments and team members. It depends on the per-request folder structure from User Story 1 being in place.

**Independent Test**: Enable cloud upload via configuration. Trigger a search request. Confirm that all screenshots appear in the cloud storage container under a folder path matching the request's correlation ID.

**Acceptance Scenarios**:

1. **Given** cloud upload is enabled, **When** a search request produces screenshots, **Then** each screenshot is uploaded to the configured cloud storage container in a folder path matching the request's correlation ID.
2. **Given** cloud upload is enabled, **When** a screenshot is captured, **Then** the screenshot is saved locally first (per US-1) and then uploaded to cloud storage.
3. **Given** cloud upload is disabled, **When** screenshots are produced, **Then** no upload attempt is made and no cloud-related overhead is incurred.
4. **Given** cloud upload is enabled but the upload fails (network error, permission denied, timeout), **When** the upload is attempted, **Then** a warning is logged and the local screenshot is retained — the primary search response is not affected.

---

### User Story 3 — Upload Trace Archives to Cloud Storage (Priority: P3)

As an operator, I want trace archives also uploaded to the same cloud storage container alongside screenshots so that all diagnostic artifacts for a request are accessible from one centralized location.

**Why this priority**: Traces complement screenshots for deep debugging. Once the cloud upload pipeline exists for screenshots, extending it to traces is a natural increment.

**Independent Test**: Enable cloud upload via configuration. Trigger a search that produces a trace. Confirm the trace `.zip` appears in the cloud storage container in the same per-request folder as the screenshots.

**Acceptance Scenarios**:

1. **Given** cloud upload is enabled and tracing is active, **When** a trace archive is produced, **Then** it is uploaded to the cloud storage container in the same per-request folder as the screenshots.
2. **Given** cloud upload is enabled but the trace upload fails, **When** the upload is attempted, **Then** a warning is logged and the local trace file is retained.

---

### User Story 4 — Configure Cloud Storage Settings (Priority: P3)

As a system administrator, I want to control cloud storage upload behavior through configuration so that I can enable or disable uploads per environment and point to different storage accounts without code changes.

**Why this priority**: Configuration flexibility is needed for production readiness but depends on the upload capability being implemented first.

**Independent Test**: Change the cloud storage enable/disable toggle and the container name in configuration. Restart the application. Verify the new settings take effect.

**Acceptance Scenarios**:

1. **Given** cloud upload is configured with a container name and connection details, **When** the application starts, **Then** it validates the configuration at startup and fails fast if required values are missing when upload is enabled.
2. **Given** cloud upload is disabled, **When** the application starts, **Then** no cloud storage client is initialized and no connection is attempted.
3. **Given** cloud upload settings are changed, **When** the application is restarted, **Then** the new settings take effect without code changes.

---

### User Story 5 — Persist Cloud Storage URL to Audit Trail (Priority: P2)

As an operator reviewing audit trail records, I want each search audit entry to include the cloud storage URL where that request's diagnostic artifacts are stored so that I can navigate directly from the audit record to the screenshots and traces.

**Why this priority**: The audit trail is the primary entry point for investigating past requests. Having a direct link to cloud-stored diagnostic artifacts eliminates the need to manually construct blob URLs from correlation IDs.

**Independent Test**: Enable cloud upload. Trigger a search request. Query the audit trail for that request. Confirm the record includes a non-null cloud storage URL pointing to the per-request folder in the blob container.

**Acceptance Scenarios**:

1. **Given** cloud upload is enabled and a search request completes, **When** the audit trail entry is written, **Then** it includes the cloud storage folder URL for that request's diagnostic artifacts.
2. **Given** cloud upload is disabled, **When** the audit trail entry is written, **Then** the cloud storage URL field is null.
3. **Given** cloud upload is enabled but the upload fails, **When** the audit trail entry is written, **Then** the cloud storage URL is still populated with the expected URL (the artifacts may not have been uploaded, but the intended location is recorded).

---

### Edge Cases

- What happens when the cloud storage container does not exist? The system auto-creates it at startup.
- What happens when the cloud upload takes longer than expected? Uploads have a per-file timeout and retry up to 3 times with exponential backoff; after exhausting retries, a warning is logged and the upload is abandoned. Slow uploads never block the search response.
- What happens when the local disk is full? Same behavior as today — log a warning and continue. If the local disk write fails, cloud upload is also skipped for that artifact (the design uploads from the local file path, not from memory). Both failures are logged independently.
- What happens when the correlation ID contains characters invalid for cloud storage folder names? The same sanitization applied to local filenames is applied to cloud folder paths.
- What happens when cloud and local storage both fail? Both failures are logged independently; the primary search response is always returned.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST organize local diagnostic artifacts (screenshots and traces) into per-request subfolders named by correlation ID under the configured local output path.
- **FR-002**: System MUST support uploading screenshots to a cloud storage container after they are saved locally, organized into per-request folders matching the local folder structure.
- **FR-003**: System MUST support uploading trace archives to the same cloud storage container, in the same per-request folder as the screenshots.
- **FR-004**: System MUST support enabling or disabling cloud upload independently from local diagnostics via a configuration toggle. The default when no configuration is provided MUST be disabled.
- **FR-005**: System MUST support configuring the cloud storage container name via application configuration.
- **FR-006**: System MUST support configuring the cloud storage connection string via application configuration, sourced from Azure Key Vault in deployed environments and .NET User Secrets for local development. No managed identity or alternative authentication methods are required.
- **FR-007**: System MUST NOT allow cloud upload failures (network errors, authentication failures, timeouts) to propagate as exceptions to the caller — the primary search response must always be returned.
- **FR-008**: System MUST log a warning when a cloud upload fails, including the artifact name and error details.
- **FR-009**: System MUST validate cloud storage configuration at application startup when upload is enabled, failing fast if required settings are missing or invalid. If the configured container does not exist, the system MUST create it automatically.
- **FR-010**: System MUST sanitize the correlation ID for use as a cloud storage folder name, applying the same character-replacement rules used for local filesystem paths.
- **FR-011**: Cloud uploads MUST include a per-file timeout to prevent slow uploads from degrading search response times. Each upload MUST be retried up to 3 times with exponential backoff before the system logs a warning and gives up.
- **FR-012**: Cloud uploads MUST NOT delay the HTTP response returned to the caller. Uploads occur inline within the diagnostics capture methods (which are already non-blocking to the search response) after the local save completes.
- **FR-013**: System MUST persist the cloud storage folder URL for the request's diagnostic artifacts to the audit trail entry when cloud upload is enabled. When cloud upload is disabled, this field MUST be null.
- **FR-014**: System MUST populate the cloud storage URL in the audit trail with the intended folder URL even if the upload itself fails, so that operators can check cloud storage for partial uploads.
- **FR-015**: System MUST always retain local diagnostic artifacts after cloud upload completes, regardless of upload success or failure. Local cleanup is an external operational concern and out of scope.

### Key Entities

- **Diagnostic Artifact**: A file (trace `.zip` or screenshot `.png`) produced during a scraping session: saved locally and optionally uploaded to cloud storage.
- **Per-Request Folder**: A directory (local) or virtual folder path (cloud) named by the sanitized correlation ID that groups all diagnostic artifacts for a single search request.
- **Cloud Storage Configuration**: Settings governing cloud upload behavior — enable/disable toggle, container name, and connection details.
- **Audit Trail Extension**: The existing `SearchAuditEntry` entity gains a new nullable field (`DiagnosticsUrl`) containing the cloud storage folder URL for the request's diagnostic artifacts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All diagnostic artifacts for a single search request are located in one per-request folder, both locally and in cloud storage, findable by correlation ID alone.
- **SC-002**: Operators can access screenshots and traces from cloud storage without requiring pod filesystem access.
- **SC-003**: Cloud upload failures cause zero impact on search response correctness or latency.
- **SC-004**: Cloud uploads complete within 10 seconds per file under normal network conditions.
- **SC-005**: Switching cloud upload on or off is a configuration-only change requiring no code modification or redeployment.

## Assumptions

- The existing diagnostics infrastructure from feature `002-scraping-diagnostics` (tracing, screenshots, milestone capture, local output path, `DiagnosticsOptions`, correlation ID propagation) is in place and working.
- Cloud storage is an Azure Blob Storage account. The connection string is stored in Azure Key Vault for deployed environments and .NET User Secrets for local development.
- The cloud storage container is pre-provisioned or can be created by the application at startup.
- Trace archives (`.zip` files) are treated identically to screenshots for upload purposes — they are local files uploaded to cloud storage after creation.
- The per-request folder naming convention (`{sanitized-correlation-id}`) applies uniformly to both local filesystem directories and cloud storage virtual folder paths.
- Cloud upload latency is acceptable for fire-and-forget background work; real-time streaming of screenshots to cloud storage is not required.
- Local diagnostic artifacts are always retained after cloud upload. Cleanup of local artifacts (e.g., age-based eviction, volume size limits) is an external operational concern handled outside the application.
- The existing `SearchAuditEntry` table will require a schema migration (new nullable column) via Liquibase, following the established changelog versioning convention.
- The correlation ID is already persisted in the audit trail (confirmed in feature 001). This feature adds only the cloud storage URL.
