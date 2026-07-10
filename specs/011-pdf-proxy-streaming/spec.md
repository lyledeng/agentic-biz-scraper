# Feature Specification: PDF Proxy Streaming Endpoint

**Feature Branch**: `011-pdf-proxy-streaming`  
**Created**: 2026-03-26  
**Status**: Draft  
**Input**: User description: "The test ui has direct links to the storage account. The user using the storage account may not have direct access to this storage account url. Add a capability so that the API can stream the pdf from the storage account, which is the link the angular test app uses."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stream a Certificate of Good Standing PDF (Priority: P1)

A test UI user views entity details and clicks the Certificate of Good Standing link. Instead of being sent directly to an Azure Blob Storage URL (which they may not have network access to), the browser requests the PDF through the BizScraper API, which streams the file from the storage account on the user's behalf.

**Why this priority**: This is the core capability — without it, users without direct storage account access cannot view any stored PDFs. Certificates of good standing are a primary deliverable of the entity-details workflow.

**Independent Test**: Can be fully tested by performing an entity detail lookup for a WY entity with a certificate available, clicking the certificate link, and verifying the PDF opens in the browser via the API proxy route.

**Acceptance Scenarios**:

1. **Given** an entity detail response includes a certificate with a `storageUrl`, **When** the user clicks the certificate link, **Then** the browser requests the PDF through the BizScraper API proxy endpoint and the PDF renders in the browser.
2. **Given** the API proxy endpoint receives a valid blob path, **When** the storage account is reachable, **Then** the API streams the PDF content with `Content-Type: application/pdf` and an appropriate `Content-Disposition` header.
3. **Given** the API proxy endpoint receives a valid blob path, **When** the blob does not exist in the storage account, **Then** the API returns a `404 Not Found` response.

---

### User Story 2 - Stream a History Document PDF (Priority: P1)

A test UI user views entity details and clicks a history document link. The browser retrieves the PDF through the BizScraper API rather than hitting the storage account directly.

**Why this priority**: History documents are as critical as certificates — WY entities may have dozens of annual reports and filings. Users need consistent access to all stored documents regardless of their network position.

**Independent Test**: Can be fully tested by performing a WY entity detail lookup, clicking any history document PDF link, and verifying the file downloads through the API.

**Acceptance Scenarios**:

1. **Given** an entity detail response includes history documents with `storageUrl` values, **When** the user clicks a history document link, **Then** the browser requests the PDF through the BizScraper API proxy endpoint.
2. **Given** a large history document (several MB), **When** the API streams the file, **Then** the response uses chunked streaming so the full file is never buffered entirely in API memory.

---

### User Story 3 - Angular Test UI Uses Proxy URLs (Priority: P1)

The Angular test UI entity details page renders certificate and history-document links using the API proxy endpoint instead of pointing directly to Azure Blob Storage URLs. This happens transparently — the API response already contains proxy-ready URLs as the `storageUrl` values, or the UI rewrites them.

**Why this priority**: The UI must be updated alongside the API so users never encounter inaccessible direct blob links. This is tightly coupled to P1 functionality.

**Independent Test**: Can be tested by inspecting the rendered `<a>` elements on the entity details page and confirming they point to the BizScraper API domain, not the storage account domain.

**Acceptance Scenarios**:

1. **Given** the entity details page is loaded, **When** a certificate or history document has a `storageUrl`, **Then** the rendered link targets the BizScraper API proxy endpoint.
2. **Given** the entity details page is loaded, **When** a user clicks a PDF link, **Then** the PDF opens in a new browser tab via the API proxy, not via the storage account.

---

### User Story 4 - Graceful Error Handling for Unavailable Documents (Priority: P2)

When a requested blob cannot be streamed — because it does not exist, the storage account is unreachable, or the blob path is invalid — the API returns a clear error and the UI provides meaningful feedback to the user.

**Why this priority**: Error states are secondary to the happy path but critical for a good user experience and for diagnosing data integrity issues.

**Independent Test**: Can be tested by requesting a proxy URL with a non-existent blob path and verifying the error response.

**Acceptance Scenarios**:

1. **Given** a proxy request for a blob path that does not exist, **When** the API attempts to stream, **Then** the API returns `404 Not Found` with an RFC 9457 Problem Details body.
2. **Given** a proxy request where the storage account is unreachable, **When** the API attempts to stream, **Then** the API returns `502 Bad Gateway` with an RFC 9457 Problem Details body.
3. **Given** a proxy request with a missing or empty blob path parameter, **When** the API validates the input, **Then** the API returns `400 Bad Request`.

---

### Edge Cases

- What happens when the blob path contains URL-encoded characters or special characters? The proxy must decode and pass through correctly.
- What happens when the certificate or history document was uploaded to a different container or the container does not exist? Return `404`.
- What happens when the file in storage is not a PDF (e.g., a corrupted upload)? The API streams whatever content-type the blob reports; it does not enforce PDF-only.
- What happens when two concurrent users request the same blob simultaneously? Both requests stream independently — no caching or queuing required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The API MUST expose a new endpoint that accepts a blob path and streams the corresponding file from Azure Blob Storage to the caller.
- **FR-002**: The streaming endpoint MUST set `Content-Type` based on the blob's content type stored in Azure (or default to `application/pdf` for PDF files).
- **FR-003**: The streaming endpoint MUST set `Content-Disposition` to `inline` with the original filename to allow in-browser viewing, with a fallback `attachment` option via `?download=true` query parameter for forced download.
- **FR-004**: The API MUST stream the blob content directly to the HTTP response without buffering the entire file in memory.
- **FR-005**: The streaming endpoint MUST return `404 Not Found` (RFC 9457 Problem Details) when the requested blob does not exist.
- **FR-006**: The streaming endpoint MUST return `400 Bad Request` when the blob path parameter is missing or empty.
- **FR-007**: The streaming endpoint MUST return `502 Bad Gateway` (RFC 9457 Problem Details) when the storage account is unreachable.
- **FR-008**: The entity-details API response MUST provide `storageUrl` values that point to the new proxy endpoint rather than directly to Azure Blob Storage. The URL rewriting MUST happen server-side in the entity-details handler; the Angular UI renders `storageUrl` as-is with no client-side URL manipulation.
- **FR-009**: The Angular test UI MUST render the `storageUrl` values as-is for all certificate and history document links. Since FR-008 ensures these are proxy URLs via server-side rewriting, no client-side URL manipulation is required.
- **FR-010**: The proxy endpoint MUST only allow access to blobs within the configured storage container — it MUST NOT allow traversal to other containers or storage accounts. Blob paths MUST be validated against a pattern allowlist: exactly one directory separator, no `..` sequences, no leading `/`, and only alphanumeric, dash, dot, and underscore characters. This matches the existing `{correlationId}/{filename}.pdf` upload convention.
- **FR-011**: The proxy endpoint MUST apply the same rate limiting as other API endpoints. Rate limiting is inherited from the global ASP.NET Core rate limiting middleware configured in `Program.cs`; no endpoint-specific rate limit configuration is required.
- **FR-012**: The proxy endpoint MUST emit structured logs per request (blob path, response status, bytes streamed, duration) and custom OpenTelemetry metrics (request counter, download duration histogram), following the existing observability patterns used by business-search and entity-details features.
- **FR-013**: The proxy endpoint MUST set HTTP caching headers for immutable content: `Cache-Control: public, max-age=86400, immutable` and return the blob's `ETag`. Subsequent requests with a matching `If-None-Match` header MUST return `304 Not Modified` without re-streaming the blob content.
- **FR-014**: The proxy endpoint MUST enforce a 30-second timeout on the blob download stream. If the storage account does not deliver the full content within 30 seconds, the endpoint MUST cancel the stream and return `504 Gateway Timeout` (RFC 9457 Problem Details).

### Key Entities

- **Blob Path**: The path within the configured Azure Blob Storage container that identifies a specific file (e.g., `{correlationId}/{filename}.pdf`).
- **Proxy URL**: An API-routed URL of the form `/api/v1/documents/{blobPath}` that the test UI uses instead of a direct storage URL.
- **Storage URL**: The Azure Blob Storage URL (e.g., `https://{account}.blob.core.windows.net/{container}/{blobPath}`) that is now accessed server-side only.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view all certificate and history document PDFs through the API proxy without requiring direct network access to the Azure storage account.
- **SC-002**: PDF links in the test UI open correctly in the browser within the time it takes to download the file from storage (no significant additional latency beyond network round-trip).
- **SC-003**: The API proxy endpoint handles concurrent requests without memory pressure — peak memory usage does not exceed the baseline by more than the size of actively streamed chunks.
- **SC-004**: 100% of certificate and history document links rendered in the test UI point to the API proxy endpoint, not to Azure Blob Storage directly.
- **SC-005**: All error scenarios (missing blob, unreachable storage, invalid path) return appropriate HTTP status codes and RFC 9457 Problem Details responses.

## Assumptions

- The existing Azure Blob Storage connection (configured in `CloudStorageOptions`) provides sufficient read permissions for the API to stream blobs — no additional storage account configuration or SAS tokens are needed.
- The configured storage container (`diagnostics`) holds all certificate and history document files. No additional containers need to be accessed.
- The proxy endpoint does not require authentication in this iteration, consistent with the current test UI design (no authentication in UI — per existing spec 010).
- The maximum individual file size for certificates and history documents is under 50 MB — standard PDF documents are typically well within this range.
- The API and test UI are deployed behind the same ingress, so the proxy endpoint is reachable from the browser at the same base URL the test UI already uses for API calls.

## Clarifications

### Session 2026-03-26

- Q: Where should storageUrl values be transformed from direct blob URLs into proxy URLs? → A: API rewrites server-side — the entity-details handler constructs proxy URLs before returning the response; the Angular UI renders storageUrl as-is with no URL manipulation.
- Q: Should the proxy streaming endpoint emit structured logs and metrics for each document download? → A: Full observability — structured log per request (blob path, status, bytes, duration) plus OpenTelemetry counter and duration histogram metrics, following existing feature patterns.
- Q: How should the proxy endpoint validate blob paths to prevent path traversal? → A: Pattern allowlist — allow only paths matching `{segment}/{filename}` (one folder depth, alphanumeric/dash/dot/underscore characters, no `..`), matching the existing correlationId/filename.pdf convention.
- Q: Should the proxy endpoint use HTTP caching headers for repeated document requests? → A: Immutable caching — set `Cache-Control: public, max-age=86400, immutable` and return the blob's ETag; subsequent requests return 304 Not Modified if the ETag matches.
- Q: What should happen if a blob download from storage takes too long mid-stream? → A: 30-second timeout — cancel the blob stream and return 504 Gateway Timeout; matches the existing 30-second upload timeout in the blob storage client.
