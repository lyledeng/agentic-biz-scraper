# Contracts: Document Proxy Authenticated Download

**Feature**: 028-doc-proxy-auth | **Date**: 2026-04-03

---

## No New or Modified API Contracts

This feature makes no changes to the API contract surface. The existing
document proxy endpoint continues to work as specified:

- **Endpoint**: `GET /api/v1/documents/{**blobPath}` — unchanged
- **Authentication**: Bearer token required (BizScraperAccess policy) — unchanged
- **Response**: PDF stream with caching headers — unchanged

See `specs/current-specs/contracts/document-proxy.md` for the authoritative contract.

## Client-Side Behavioural Change

The **Angular test-ui** changes how it invokes the document proxy:

| Aspect | Before (broken) | After (fixed) |
|--------|-----------------|---------------|
| HTTP mechanism | `<a href>` (browser navigation) | `HttpClient.get()` (XHR with MSAL interceptor) |
| Auth header | Not attached | `Authorization: Bearer <token>` (automatic via MsalInterceptor) |
| User experience | New browser tab with 401 error | Inline PDF on entity-details page |
| Memory | N/A | Blob URLs revoked on component destroy |
