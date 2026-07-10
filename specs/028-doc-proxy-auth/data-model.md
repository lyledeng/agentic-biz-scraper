# Data Model: Document Proxy Authenticated Download

**Feature**: 028-doc-proxy-auth | **Date**: 2026-04-03

---

## Overview

This feature introduces no new backend entities or database changes. All changes are Angular-only UI state models and a new service.

---

## New Angular Models

### DocumentViewerState

Per-document UI state for tracking inline PDF viewer lifecycle.

```typescript
/** Tracks the loading / display / error state of a single inline PDF viewer. */
interface DocumentViewerState {
  /** Unique key combining document index and download label. */
  key: string;
  /** Current fetch status. */
  status: 'idle' | 'loading' | 'loaded' | 'error';
  /** Blob URL created via URL.createObjectURL() — null until loaded. */
  blobUrl: string | null;
  /** User-facing error message — null unless status is 'error'. */
  errorMessage: string | null;
}
```

**Field Descriptions**:

| Field | Type | Description |
|-------|------|-------------|
| `key` | `string` | Composite key: `${sectionType}-${docIndex}-${downloadLabel}` to uniquely identify each download link |
| `status` | `enum` | `idle` = not yet clicked, `loading` = fetch in progress, `loaded` = PDF displayed, `error` = fetch failed |
| `blobUrl` | `string \| null` | Blob URL for the `<object>` element; set on successful fetch, revoked on cleanup |
| `errorMessage` | `string \| null` | Categorized error message shown in the inline error area |

### State Transitions

```text
idle ──[click]──► loading ──[success]──► loaded
                     │                      │
                     │                      └──[click another]──► loading (revoke prev blobUrl)
                     │
                     └──[error]──► error ──[retry]──► loading
```

---

## Existing Models (Unchanged)

### DownloadReference (Angular)

```typescript
interface DownloadReference {
  label: string;
  proxyUrl: string | null;
  fileName: string;
  error: string | null;
}
```

No changes. The `proxyUrl` field continues to be the relative API path (e.g., `/api/v1/documents/screenshots/...`). The `DocumentProxyService` prepends the API base URL before fetching.

### DownloadReference (C# API)

```csharp
public sealed record DownloadReference
{
    public required string Label { get; init; }
    public string? ProxyUrl { get; init; }
    public required string FileName { get; init; }
    public string? Error { get; init; }
}
```

No changes needed on the API side.

### UnifiedEntityDetailResponse (Angular)

```typescript
interface UnifiedEntityDetailResponse {
  details: DetailSection;
  registeredAgent: AgentSection | null;
  certificate: CertificateSection | null;
  parties: PartyEntry[] | null;
  documents: DocumentEntry[] | null;
}
```

No changes. The component will maintain a `Map<string, DocumentViewerState>` parallel to the existing response structure to track viewer state per download link.

---

## New Angular Service

### DocumentProxyService

```typescript
@Injectable({ providedIn: 'root' })
class DocumentProxyService {
  fetchDocument(proxyUrl: string): Observable<Blob>;
}
```

| Method | Input | Output | Description |
|--------|-------|--------|-------------|
| `fetchDocument` | `proxyUrl: string` (relative API path) | `Observable<Blob>` | Fetches PDF via `HttpClient` with `responseType: 'blob'`. MSAL interceptor attaches bearer token automatically. |

---

## Database Changes

None. This feature has no backend data model impact.

---

## Relationships

```text
EntityDetailsComponent
  ├── uses DocumentProxyService.fetchDocument()
  ├── manages Map<string, DocumentViewerState>
  ├── creates blob URL → embedded in <object type="application/pdf">
  └── revokes blob URLs on ngOnDestroy()

DocumentProxyService
  └── uses HttpClient.get() with responseType: 'blob'
      └── MSAL interceptor attaches Authorization: Bearer <token>
          └── API /api/v1/documents/{**blobPath} streams PDF
```
