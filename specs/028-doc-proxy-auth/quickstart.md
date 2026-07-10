# Quickstart: Document Proxy Authenticated Download

**Feature**: 028-doc-proxy-auth | **Date**: 2026-04-03

---

## Prerequisites

- Node.js 18+ and npm
- Angular CLI 17+
- API running locally (or accessible at configured `apiBaseUrl`)
- MSAL app registration configured (see `specs/025-entra-id-auth/`)

## Quick Verification

1. Start the API:
   ```bash
   cd src/BizScraper.Api
   dotnet run
   ```

2. Start the test-ui:
   ```bash
   cd src/test-ui
   npx ng serve
   ```

3. Navigate to `http://localhost:4200`, sign in with your Entra ID credentials.

4. Search for a WY entity with documents (e.g., "Wendy's" in WY).

5. Click on an entity to view details.

6. In the Documents section, click any document's download link.

7. **Expected**: The PDF renders inline on the entity-details page in an `<object>` element. No 401 error. No new tab opens.

8. **Error scenario**: If the document fails to load, an error message with a Retry button appears in place of the PDF viewer.

## Key Files

| File | Purpose |
|------|---------|
| `src/test-ui/src/app/core/services/document-proxy.service.ts` | NEW — Fetches PDFs via HttpClient (MSAL-intercepted) |
| `src/test-ui/src/app/shared/models/document-viewer.model.ts` | NEW — DocumentViewerState interface for per-document viewer state |
| `src/test-ui/src/app/features/entity-details/entity-details.component.ts` | MODIFIED — Inline viewer state management, OnDestroy cleanup |
| `src/test-ui/src/app/features/entity-details/entity-details.component.html` | MODIFIED — Replaced `<a href>` with click handlers + inline `<object>` |
| `src/test-ui/src/app/features/entity-details/entity-details.component.css` | MODIFIED — Styles for inline PDF viewer and error state |
| `src/test-ui/nginx.conf` | MODIFIED — Added `object-src blob:` to Content-Security-Policy |

## Architecture Notes

- **No API changes**: The document proxy endpoint (`GET /api/v1/documents/{**blobPath}`) is unchanged. The fix is entirely in the Angular test-ui.
- **MSAL interceptor**: Already configured to attach tokens to all requests matching `${environment.apiBaseUrl}/*`. Using `HttpClient` instead of `<a href>` ensures the token is included.
- **Memory management**: Blob URLs are tracked and revoked via `ngOnDestroy()` to prevent memory leaks.
