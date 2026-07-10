# Research: PDF Proxy Streaming Endpoint

**Feature**: 011-pdf-proxy-streaming
**Date**: 2026-03-26

## R-001: Azure Blob Storage Streaming Download Strategy

### Decision: Use `BlobClient.DownloadStreamingAsync()` to stream blob content directly to the HTTP response without full-memory buffering

### Rationale
The Azure.Storage.Blobs SDK provides two download APIs:
- `DownloadContentAsync()` — downloads the entire blob into memory as `BinaryData`. Unsuitable for streaming PDFs up to 50 MB.
- `DownloadStreamingAsync()` — returns a `BlobDownloadStreamingResult` with a `Content` property of type `Stream` that can be piped directly to `HttpResponse.Body`. This is the streaming-first approach required by FR-004 and the project architecture conventions.

The `BlobDownloadStreamingResult` also provides:
- `Details.ContentType` — used for `Content-Type` header (FR-002)
- `Details.ContentLength` — used for `Content-Length` header
- `Details.ETag` — used for conditional requests and caching (FR-013)

The download method will be added to `IBlobStorageClient` as:
```csharp
Task<BlobDownloadResult?> DownloadBlobAsync(string blobPath, string? ifNoneMatch, CancellationToken cancellationToken);
```

Where `BlobDownloadResult` is a new record:
```csharp
public sealed record BlobDownloadResult(
    Stream Content,
    string ContentType,
    long ContentLength,
    string ETag,
    string FileName);
```

### Alternatives Considered
- **`DownloadContentAsync()` then write to response**: Buffers entire blob in memory. Rejected per FR-004 (streaming-first, no full-file buffering).
- **SAS token redirect**: Generate a time-limited SAS URL and redirect the client. Rejected because users may not have network access to the storage account (the entire reason for this feature).
- **Azure CDN/Front Door proxy**: Over-engineering for a test UI. Rejected per YAGNI.

## R-002: ASP.NET Core Streaming Response Pattern

### Decision: Use `Results.Stream()` with the blob download stream for chunked response delivery

### Rationale
ASP.NET Core minimal APIs provide `Results.Stream(stream, contentType, fileDownloadName)` which:
1. Sets `Content-Type` from the provided parameter
2. Sets `Content-Disposition` with the filename
3. Copies the stream to the response body using chunked transfer
4. Disposes the stream when complete

This aligns with the existing endpoint pattern where handlers return `IResult`. The handler returns `Results.Stream()` for successful downloads and `Results.Problem()` for errors.

For conditional requests (FR-013), the handler checks:
1. Compare the request's `If-None-Match` header with the blob's ETag
2. If they match → return `Results.StatusCode(304)` (Not Modified)
3. If no match → stream with `Cache-Control: public, max-age=86400, immutable` and `ETag` header

### Alternatives Considered
- **`FileStreamResult` via controller**: The project uses minimal APIs, not controllers. Rejected.
- **Manual `Response.Body.WriteAsync()`**: Lower-level than needed; `Results.Stream()` handles disposal and headers correctly. Rejected per KISS.
- **`Results.File(stream, ...)`**: Also viable but `Results.Stream()` is the preferred minimal API pattern for arbitrary streams that may not be seekable. Selected `Results.Stream()`.

## R-003: Blob Path Validation and Security

### Decision: Compile-time `[GeneratedRegex]` pattern allowlist matching `{segment}/{filename}` with restricted characters

### Rationale
Per FR-010, blob paths must be validated to prevent path traversal and restrict access to the configured container only. The existing upload convention uses `{correlationId}/{filename}.pdf` where:
- `correlationId` is a GUID-like trace identifier (alphanumeric + hyphens)
- `filename` is sanitized to alphanumeric, dash, dot, and underscore

The validation regex:
```csharp
[GeneratedRegex(@"^[a-zA-Z0-9\-]+/[a-zA-Z0-9\-_.]+$")]
private static partial Regex BlobPathPattern();
```

This enforces:
- Exactly one `/` separator (one directory level)
- No `..` sequences (impossible given the character class)
- No leading `/` (starts with alphanumeric)
- Only safe characters: `a-zA-Z0-9`, `-`, `_`, `.`

Additional server-side checks:
- Reject empty or whitespace-only paths → 400
- Reject paths > 1024 characters → 400

### Alternatives Considered
- **Deny-list approach** (block `..` and `\`): Brittle — new attack vectors may bypass the deny list. Rejected.
- **URL-decode then validate**: The path comes from ASP.NET route binding which already decodes. No double-decoding needed.

## R-004: URL Rewriting Strategy in Entity Details Handler

### Decision: Server-side URL rewriting in `GetEntityDetailsHandler` — replace blob storage base URL with API proxy base URL before returning the response

### Rationale
Per FR-008 and the clarification session, URL rewriting happens server-side. The handler constructs proxy URLs by:
1. Taking the existing `storageUrl` (e.g., `https://account.blob.core.windows.net/diagnostics/{correlationId}/{filename}.pdf`)
2. Extracting the blob path portion (`{correlationId}/{filename}.pdf`)
3. Constructing the proxy URL: `{apiBaseUrl}/api/v1/documents/{correlationId}/{filename}.pdf`

The API base URL is derived from `HttpContext.Request` (`Scheme` + `Host`) so it works in any deployment environment without configuration.

The rewriting applies to:
- `certificate.storageUrl`
- Each `historyDocuments[].storageUrl`

Implementation approach — a helper method in the handler:
```csharp
private string? ToProxyUrl(string? storageUrl, string correlationId)
{
    if (storageUrl is null) return null;
    var blobPath = ExtractBlobPath(storageUrl);
    return $"{_baseUrl}/api/v1/documents/{blobPath}";
}
```

### Alternatives Considered
- **Client-side rewriting in Angular**: Rejected per clarification — server rewrites; UI renders as-is.
- **Middleware that intercepts all responses**: Over-engineering; only entity-details needs rewriting. Rejected per SRP.
- **Configuration-based base URL**: Would require environment-specific config. Using `HttpContext.Request` is self-configuring. Selected.

## R-005: Resilience and Timeout Strategy for Blob Downloads

### Decision: Dedicated `ResiliencePipeline` for downloads with 3 retries + 30-second timeout, matching the existing upload pipeline

### Rationale
Per FR-014, the download stream must timeout at 30 seconds. The existing `AzureBlobStorageClient` already uses a `ResiliencePipeline` for uploads with identical parameters:
- 3 retry attempts
- 1-second exponential backoff with jitter
- `ShouldHandle`: `RequestFailedException` with status 408, 429, 500, 502, 503, 504
- 30-second overall timeout

A separate `_downloadPipeline` field will be created using the same `BuildDownloadPipeline()` factory method pattern. This keeps upload and download pipelines independent (different operations may need different tuning in the future).

When the timeout fires:
1. Polly cancels the download stream via `CancellationToken`
2. The handler catches `TimeoutRejectedException`
3. Returns `504 Gateway Timeout` with RFC 9457 Problem Details

When the blob is not found:
1. `BlobClient.DownloadStreamingAsync()` throws `RequestFailedException` with status 404
2. The handler catches and returns `404 Not Found` with Problem Details

When the storage account is unreachable:
1. After retries are exhausted, Polly rethrows the last `RequestFailedException`
2. The handler catches and returns `502 Bad Gateway` with Problem Details

### Alternatives Considered
- **Reuse the upload pipeline instance**: Technically possible since pipelines are thread-safe, but conflates upload and download concerns. Rejected per SRP.
- **No retry on downloads**: Downloads are idempotent (GET), so retries are safe and improve resilience. Kept retries.

## R-006: HTTP Caching Headers for Immutable Content

### Decision: Set `Cache-Control: public, max-age=86400, immutable` and `ETag` on all successful responses; support `If-None-Match` for 304

### Rationale
Per FR-013, stored PDFs are immutable (certificates and annual reports never change after upload). The caching strategy:

**Response headers on 200 OK**:
```
Cache-Control: public, max-age=86400, immutable
ETag: "{blob-etag}"
Content-Type: application/pdf
Content-Disposition: inline; filename="{filename}"
```

**Conditional request handling**:
1. Client sends `If-None-Match: "{previous-etag}"`
2. Handler calls `BlobClient.GetPropertiesAsync()` to get current ETag (lightweight, no content download)
3. If ETag matches → return `304 Not Modified` (no body, no Content-Disposition)
4. If ETag differs → proceed with full download stream

The `GetPropertiesAsync()` call is cheap (metadata-only, no content transfer) and avoids downloading the full blob when the client already has it cached.

**Implementation note**: `BlobDownloadStreamingResult.Details.ETag` is of type `Azure.ETag` which must be converted to string for the HTTP header. Use `eTag.ToString()` which includes the quotes.

### Alternatives Considered
- **No caching**: Would re-download identical files on every browser tab open. Wasteful for immutable content. Rejected.
- **Cache-Control without ETag**: Cannot support conditional requests and 304. Rejected; ETag is free since the blob SDK provides it.
- **Server-side response cache middleware**: Overkill for a streaming endpoint — would buffer the response. Rejected.

## R-007: OpenTelemetry Observability Pattern

### Decision: Follow existing `EntityDetailsLogMessages` pattern — static partial class with `[LoggerMessage]` attributes + custom OTel metrics

### Rationale
Per FR-012, the proxy endpoint must emit structured logs and OpenTelemetry metrics. Following existing conventions:

**Logging** — new `DocumentLogMessages` static partial class:
- EventId range: 3000–3099 (next available after entity-details 2000–2099 range)
- Messages: `DocumentStreamStarted`, `DocumentStreamCompleted`, `DocumentStreamFailed`, `DocumentNotFound`, `DocumentStreamTimeout`
- Parameters: `blobPath`, `statusCode`, `bytesStreamed`, `durationMs`

**Metrics** — via `System.Diagnostics.Metrics.Meter`:
- Counter: `bizscraper.documents.requests` (tags: `status`, `blob_path_prefix`)
- Histogram: `bizscraper.documents.duration_ms` (tags: `status`)

This follows the existing pattern where features define their own metrics using the OpenTelemetry-compatible `Meter` API.

### Alternatives Considered
- **Reuse entity-details log messages class**: Violates SRP — document streaming is a separate concern. Rejected.
- **Middleware-level logging only**: Would miss blob-specific context (path, bytes streamed). Rejected.
