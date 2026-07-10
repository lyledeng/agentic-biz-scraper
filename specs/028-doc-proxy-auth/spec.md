# Feature Specification: Document Proxy Authenticated Download

**Feature Branch**: `028-doc-proxy-auth`  
**Created**: 2026-04-03  
**Status**: Draft  
**Input**: User description: "When the API downloads documents, the pdf proxy url is used as a link in the test-ui to allow the user to view the document in a new browser window. This is generating an HTTP 401 now after the Entra ID authentication was implemented."

## User Scenarios & Testing

### User Story 1 — View Document Inline (Priority: P1)

As a test-ui user viewing entity details, when I click a document download link (certificate of good standing, filing history, or translated PDF), the document is fetched with my authentication credentials and displayed inline on the entity-details page — even though the API requires Entra ID authentication.

**Why this priority**: This is the only user-facing capability blocked by the 401 regression. All document links in the entity-details view are broken.

**Independent Test**: Navigate to a WY entity with history documents, click any document link, and verify the PDF renders inline on the entity-details page without an authentication error.

**Acceptance Scenarios**:

1. **Given** a signed-in user is viewing entity details with document downloads, **When** they click a document link, **Then** the PDF is fetched via `HttpClient` and displayed inline on the same page without an HTTP 401 error.
2. **Given** a signed-in user is viewing a WY entity with a certificate of good standing, **When** they click the certificate link, **Then** the certificate PDF renders inline on the entity-details page.
3. **Given** a signed-in user is viewing a DE entity with translated and original documents, **When** they click either document link, **Then** the PDF renders inline on the entity-details page.
4. **Given** a user whose session has expired clicks a document link, **Then** the system initiates a re-authentication flow rather than showing a raw 401 error page.

---

### User Story 2 — (Merged into US1)

US2 (inline preview) has been merged into US1 since the inline-only presentation mode was selected. The error-handling acceptance scenario is captured in US1 via FR-004.

---

### Edge Cases

- What happens when the user's Entra ID token expires between loading entity details and clicking a document link?
- How does the system handle a document proxy URL that returns a 404 (blob not found)?
- What happens when the document proxy URL returns a 502 or 504 (storage error or timeout)?
- How does the system handle concurrent document opens (user rapidly clicks multiple document links)?

## Requirements

### Functional Requirements

- **FR-001**: The test-ui MUST include the user's authentication credentials when fetching PDF documents from the API.
- **FR-002**: The test-ui MUST NOT open authenticated document URLs via direct browser navigation that bypasses the authentication layer.
- **FR-003**: When a document is fetched successfully, the test-ui MUST render the PDF inline on the entity-details page using a blob URL embedded in an `<object>` element.
- **FR-004**: When a document fetch fails, the test-ui MUST display an inline error message in the viewer area for the specific document that failed, indicating the nature of the failure (authentication error, not found, server error), with a Retry button to re-attempt the fetch.
- **FR-005**: The document viewing experience MUST work for all document types currently supported: WY filing history documents, WY/CO certificates of good standing, and DE original/translated PDFs.
- **FR-006**: The test-ui MUST revoke blob URLs created via `URL.createObjectURL()` when the inline viewer is closed, collapsed, or the component is destroyed (`OnDestroy`), to prevent memory leaks.

## Success Criteria

### Measurable Outcomes

- **SC-001**: All document links in the entity-details view successfully load PDFs without HTTP 401 errors for authenticated users.
- **SC-002**: Users can view any document inline within 5 seconds of clicking the link (excluding network latency to the upstream storage).
- **SC-003**: Document links work consistently across all supported entity types and states (CO, WY, IA, DE).
- **SC-004**: Expired-session scenarios result in a re-authentication prompt rather than a raw error page.

## Assumptions

- The existing MSAL interceptor and `protectedResourceMap` configuration correctly matches the API base URL and attaches tokens to `HttpClient` requests.
- The document proxy endpoint (`GET /api/v1/documents/{**blobPath}`) continues to require the `BizScraperAccess` authorization policy (bearer token with `access_as_user` scope or `BizScraper.Execute` role).
- The API document proxy endpoint streams PDFs with `Content-Type: application/pdf` and appropriate headers — no changes are needed on the API side.
- The primary fix is on the Angular test-ui side: replacing direct `<a href>` navigation with `HttpClient`-based document fetching.
- Browser support for displaying blob URLs or embedded PDF viewers is sufficient for the target audience (modern Chrome/Edge).
- Both US1 (401 fix) and US2 (inline preview) are in scope for this feature branch.
- PDF presentation is inline only — documents are fetched via `HttpClient` and embedded on the entity-details page; no new-tab open.
- Blob URLs created for inline PDF display are revoked automatically via Angular `OnDestroy` lifecycle hook when the viewer closes or the user navigates away.
- Only one inline PDF viewer is open at a time — clicking a new document link closes (and revokes the blob URL of) any previously open viewer.

## Clarifications

### Session 2026-04-03

- Q: Should US2 (inline preview) be included or deferred to a later feature? → A: Include both US1 and US2 in this branch.
- Q: How should the PDF be presented to the user? → A: Inline only — fetch and embed in entity-details page.
- Q: How should the inline viewer handle blob URL memory cleanup? → A: Revoke on viewer close/navigation (automatic cleanup via Angular `OnDestroy`).
- Q: What error UI pattern should be used when a document fetch fails? → A: Inline error message in the viewer area with a Retry button.
