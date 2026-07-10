# Data Model: PDF Proxy Streaming Endpoint

**Feature**: 011-pdf-proxy-streaming
**Date**: 2026-03-26

## Entities

### No New Persistence Entities

This feature does not introduce any new database tables or audit trail entries. The proxy endpoint is stateless — it streams blobs from Azure Blob Storage to the HTTP response without persisting any state.

Existing entities are **unmodified**:
- `EntityDetailAuditEntry` — already captures `CertificateUrl` and `DiagnosticsUrl`; the URL values will now be proxy URLs instead of direct blob URLs, but the column type and semantics are unchanged.

---

## API Models (Records)

### StreamDocumentQuery

**Purpose**: CQRS query representing a document streaming request.

```csharp
namespace BizScraper.Api.Features.Documents.Models;

public sealed record StreamDocumentQuery(
    string BlobPath,
    string? IfNoneMatch,
    bool ForceDownload) : IQuery<IResult>;
```

| Field | Type | Description |
|-------|------|-------------|
| BlobPath | `string` | Validated blob path within the configured container (e.g., `{correlationId}/{filename}.pdf`) |
| IfNoneMatch | `string?` | Client-provided ETag for conditional request (`If-None-Match` header); null if not present |
| ForceDownload | `bool` | When `true`, sets `Content-Disposition: attachment` instead of `inline` (FR-003 fallback) |

---

### BlobDownloadResult

**Purpose**: Returned by `IBlobStorageClient.DownloadBlobAsync()` — contains the stream handle and metadata needed by the handler to construct the HTTP response.

```csharp
namespace BizScraper.Api.Common.Interfaces;

public sealed record BlobDownloadResult(
    Stream Content,
    string ContentType,
    long ContentLength,
    string ETag,
    string FileName) : IDisposable, IAsyncDisposable
{
    public void Dispose() => Content.Dispose();
    public async ValueTask DisposeAsync() => await Content.DisposeAsync().ConfigureAwait(false);
}
```

| Field | Type | Description |
|-------|------|-------------|
| Content | `Stream` | The blob content stream (from `DownloadStreamingAsync`) — must be disposed after streaming |
| ContentType | `string` | Content type from blob metadata; defaults to `application/pdf` if not set |
| ContentLength | `long` | Byte length of the blob content |
| ETag | `string` | Azure Blob Storage ETag (includes surrounding quotes, e.g., `"0x8D..."`) |
| FileName | `string` | File name extracted from the blob path (last segment after `/`) |

---

## Interface Changes

### IBlobStorageClient (Modified)

Two new methods added to the existing interface:

```csharp
public interface IBlobStorageClient
{
    // EXISTING
    Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken);
    Uri? GetFolderUrl(string folderPath);

    // NEW — Feature 011
    Task<BlobDownloadResult?> DownloadBlobAsync(string blobPath, CancellationToken cancellationToken);
    Task<string?> GetBlobETagAsync(string blobPath, CancellationToken cancellationToken);
}
```

| Method | Returns | Description |
|--------|---------|-------------|
| `DownloadBlobAsync` | `BlobDownloadResult?` | Downloads blob as a stream; returns `null` if blob does not exist (404 from storage) |
| `GetBlobETagAsync` | `string?` | Gets the current ETag without downloading content; used for conditional request handling (304) |

**NullBlobStorageClient** implementations return `null` / `null` (cloud storage disabled).

---

## Validation Model

### BlobPathValidator

**Purpose**: Static validation utility implementing FR-010 pattern allowlist.

```csharp
namespace BizScraper.Api.Features.Documents.Validation;

public static partial class BlobPathValidator
{
    [GeneratedRegex(@"^[a-zA-Z0-9\-]+/[a-zA-Z0-9\-_.]+$")]
    private static partial Regex ValidBlobPathPattern();

    public static bool IsValid(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath)) return false;
        if (blobPath.Length > 1024) return false;
        return ValidBlobPathPattern().IsMatch(blobPath);
    }
}
```

**Rules enforced**:
- Not null, empty, or whitespace → 400
- Max 1024 characters → 400
- Exactly one `/` separator
- Only characters: `a-zA-Z0-9`, `-`, `_`, `.`
- No `..` (impossible given character class)
- No leading `/`

---

## State Transitions

This feature is stateless — no entity state transitions apply. Each request is an independent blob stream operation.

---

## Relationship Diagram

```text
┌─────────────────┐     GET /api/v1/documents/{**blobPath}
│  Angular Test UI │ ──────────────────────────────────────────┐
│  (entity-details)│                                           │
└─────────────────┘                                           ▼
                                                    ┌──────────────────┐
                                                    │ DocumentEndpoints│
                                                    │  (validation +   │
                                                    │   route binding) │
                                                    └────────┬─────────┘
                                                             │
                                                             ▼
                                                    ┌──────────────────┐
                                                    │StreamDocument    │
                                                    │Handler           │
                                                    │  (CQRS query)   │
                                                    └────────┬─────────┘
                                                             │
                                              ┌──────────────┴──────────────┐
                                              │                             │
                                              ▼                             ▼
                                    ┌─────────────────┐          ┌──────────────────┐
                                    │IBlobStorageClient│          │DocumentLog       │
                                    │.DownloadBlobAsync│         │Messages           │
                                    │.GetBlobETagAsync │          │(OTel metrics)    │
                                    └────────┬────────┘          └──────────────────┘
                                             │
                                             ▼
                                    ┌─────────────────┐
                                    │Azure Blob Storage│
                                    │(diagnostics     │
                                    │ container)       │
                                    └─────────────────┘
```

```text
URL Rewriting Flow (entity-details response):

  GetEntityDetailsHandler
    │
    ├── Upload certificate → blob storage
    ├── Upload history docs → blob storage
    ├── Construct storageUrl = proxy URL (/api/v1/documents/{path})
    │   instead of direct blob URL (https://account.blob.core.windows.net/...)
    └── Return EntityDetailResponse with proxy URLs
```
