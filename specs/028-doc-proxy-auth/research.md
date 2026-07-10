# Research: Document Proxy Authenticated Download

**Feature**: 028-doc-proxy-auth | **Date**: 2026-04-03

---

## R1: Root Cause — Why Document Links Return 401

**Decision**: The HTTP 401 is caused by `<a [href]="dl.proxyUrl" target="_blank">` in the entity-details template. When the browser opens a new tab via an anchor element, it issues a standard GET request without the `Authorization` header. The MSAL interceptor only attaches bearer tokens to Angular `HttpClient` requests (XHR/fetch), not to browser-level navigation.

**Evidence**:
- `auth.config.ts` line 29: `protectedResourceMap` maps `${environment.apiBaseUrl}/*` to scopes — this only affects `HttpClient`.
- `app.config.ts` registers `MsalInterceptor` as `HTTP_INTERCEPTORS` — Angular-level only.
- `DocumentEndpoints.cs` registers under `protectedGroup.RequireAuthorization("BizScraperAccess")` — requires bearer token.
- Template lines 223, 247: `<a [href]="dl.proxyUrl" target="_blank" rel="noopener noreferrer">` — direct browser navigation, no token.

**Alternatives Considered**:
1. **Query parameter token**: Append token to URL as `?token=...` — REJECTED (security risk: tokens in server logs, browser history, referer headers).
2. **Server-side session cookie**: Issue a session cookie on login — REJECTED (requires API changes, breaks stateless design, doesn't work with client-credential tokens).
3. **Temporary pre-signed URL**: API generates short-lived pre-signed blob URLs — REJECTED (requires API changes; current proxy endpoint already handles streaming).

---

## R2: Inline PDF Rendering Approach

**Decision**: Use `HttpClient` to fetch the document as a blob, create a blob URL via `URL.createObjectURL()`, and embed in an `<object type="application/pdf">` element on the entity-details page.

**Rationale**:
- `HttpClient` requests pass through the MSAL interceptor → token attached automatically.
- `responseType: 'blob'` gives us a `Blob` object suitable for `URL.createObjectURL()`.
- `<object type="application/pdf">` renders PDFs natively in Chrome/Edge without additional libraries.
- Blob URLs are same-origin, so browser PDF viewers render them without CSP issues.

**Alternatives Considered**:
1. **`<iframe src="blob:...">` instead of `<object>`** — Both work; `<object>` is slightly more semantic for embedded resources and has better fallback content support.
2. **PDF.js library** — REJECTED (adds ~500KB dependency; browser-native PDF viewer is sufficient for a test UI).
3. **Base64 data URL** — REJECTED (URL length limits; 33% size overhead; no streaming).

---

## R3: Blob URL Memory Management

**Decision**: Track all created blob URLs in an array on the component. Revoke all of them in `ngOnDestroy()`. When a user clicks a new document, revoke the previous blob URL for that viewer slot before creating a new one.

**Rationale**: Each `URL.createObjectURL()` holds a reference to the blob in browser memory until explicitly revoked or the page is unloaded. For a test UI with limited document viewing, the memory footprint is manageable, but cleanup on destroy prevents accumulation during long sessions.

**Pattern**:
```typescript
private activeBlobUrls: string[] = [];

viewDocument(proxyUrl: string): void {
  this.documentProxyService.fetchDocument(proxyUrl).subscribe(blob => {
    const blobUrl = URL.createObjectURL(blob);
    this.activeBlobUrls.push(blobUrl);
    this.inlineViewerUrl.set(blobUrl);
  });
}

ngOnDestroy(): void {
  this.activeBlobUrls.forEach(url => URL.revokeObjectURL(url));
}
```

---

## R4: Error Handling Strategy

**Decision**: Each document link gets its own loading/error state. When a fetch fails, the link area shows an error message with the HTTP status categorization and a Retry button. Clicking Retry re-fetches the same proxyUrl.

**Error Categories**:
| HTTP Status | User Message |
|-------------|-------------|
| 401 | "Authentication expired. Please sign in again." (triggers MSAL redirect) |
| 404 | "Document not found in storage." |
| 502/504 | "Storage temporarily unavailable. Please retry." |
| Other | "Failed to load document." |

**Rationale**: Per-document error state is important because entity details may have many documents (WY entities can have 10+ filing history entries). A single global error would be confusing when only one document fails.

---

## R5: Angular Service Design

**Decision**: Create a new `DocumentProxyService` under `core/services/` following the existing `ExecuteScriptService` pattern. Single method: `fetchDocument(proxyUrl: string): Observable<Blob>`.

**Rationale**:
- Isolates document fetching concern from the entity-details component.
- Uses `HttpClient.get()` with `{ responseType: 'blob' }` — MSAL interceptor attaches the token automatically since the URL matches `protectedResourceMap`.
- The `proxyUrl` from the API response is a relative path like `/api/v1/documents/...` — the `ApiConfigService.baseUrl()` is prepended.

**Alternatives Considered**:
1. **Inline `HttpClient` call in component** — REJECTED (violates SRP; harder to test).
2. **Extend `ExecuteScriptService`** — REJECTED (document fetching is a different concern from script execution).

---

## R6: CSP Considerations

**Decision**: No CSP changes needed. The existing `SecurityHeadersMiddleware` sets `X-Frame-Options: DENY` on API responses, but the inline `<object>` embeds a blob URL (not an API URL), so frame restrictions don't apply. The `Content-Security-Policy` would need `object-src blob:` if set, but the Angular app serves its own CSP from the nginx config, not from the API.

**Verification**: The Angular container's nginx config should allow `object-src blob:` in its CSP. This will be verified during implementation.
