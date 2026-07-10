# Internal Contracts: PDF Proxy Streaming

**Feature**: 011-pdf-proxy-streaming
**Date**: 2026-03-26

## CQRS Query Contract

### StreamDocumentQuery

```csharp
public sealed record StreamDocumentQuery(
    string BlobPath,
    string? IfNoneMatch,
    bool ForceDownload) : IQuery<IResult>;
```

**Resolved by**: `StreamDocumentHandler` via LiteBus `IQueryHandler<StreamDocumentQuery, IResult>`

**Notes**:
- Unlike other queries that return a domain model, this query returns `IResult` directly because the handler must produce different HTTP results (200 stream, 304, 400, 404, 502, 504) with headers and a streaming body.
- The `BlobPath` is already validated by `BlobPathValidator` before the query is dispatched.

---

## Handler Contract

### StreamDocumentHandler

```
Input:  StreamDocumentQuery { BlobPath, IfNoneMatch, ForceDownload }
Output: IResult (one of: stream, 304, 400, 404, 502, 504)

Dependencies (constructor-injected):
  - IBlobStorageClient
  - ILogger<StreamDocumentHandler>

Note: HttpContext is NOT injected — the endpoint delegate sets Cache-Control
and ETag headers on HttpContext.Response.Headers before dispatching the query.
The handler returns IResult which the framework writes to the response.

Processing flow:
  1. Validate BlobPath via BlobPathValidator (redundant safety check)
  2. If IfNoneMatch is present:
     a. Call IBlobStorageClient.GetBlobETagAsync(blobPath, ct)
     b. If ETag matches → return 304 Not Modified
     c. If blob not found → return 404
  3. Call IBlobStorageClient.DownloadBlobAsync(blobPath, ct)
  4. If null → return 404 Not Found (Problem Details)
  5. Set response headers: Cache-Control, ETag
  6. Determine Content-Disposition: inline (default) or attachment (if ForceDownload)
  7. Return Results.Stream(content, contentType, fileName, disposition)

Error handling:
  - RequestFailedException (status 404) → 404 Not Found
  - RequestFailedException (other) → 502 Bad Gateway (after retries exhausted)
  - TimeoutRejectedException → 504 Gateway Timeout
  - All errors → RFC 9457 Problem Details body

Does NOT:
  - Authenticate or authorize the caller (no auth in test UI)
  - Cache blob content server-side (streaming only)
  - Modify or transform the blob content
  - Access any database
  - Write audit trail entries
```

---

## Storage Interface Contract

### IBlobStorageClient (Extended)

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

**AzureBlobStorageClient implementation**:
- `DownloadBlobAsync`: Uses `BlobClient.DownloadStreamingAsync()` within a `ResiliencePipeline` (3 retries, 30s timeout). Returns `null` on `RequestFailedException` with status 404.
- `GetBlobETagAsync`: Uses `BlobClient.GetPropertiesAsync()` to retrieve ETag without downloading content. Returns `null` on 404.

**NullBlobStorageClient implementation**:
- `DownloadBlobAsync`: Returns `null` (no cloud storage configured).
- `GetBlobETagAsync`: Returns `null`.

---

## Endpoint Contract

### DocumentEndpoints

```csharp
public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/documents/{**blobPath}",
                async Task<IResult> (string blobPath, bool? download, 
                    HttpContext httpContext, IQueryMediator queryMediator, 
                    CancellationToken cancellationToken) => { ... })
            .WithName("StreamDocument")
            .WithTags("Documents")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);

        return endpoints;
    }
}
```

**Route**: `GET /api/v1/documents/{**blobPath}` (catch-all route segment to capture `correlationId/filename.pdf`)

**Binding**:
- `blobPath` — from route (catch-all `**` captures `/` in path)
- `download` — from query string (optional, defaults to `false`)
- `If-None-Match` — from `HttpContext.Request.Headers` (bound manually since header binding isn't automatic for minimal APIs)

---

## URL Rewriting Contract

### GetEntityDetailsHandler (Modified)

```
CURRENT BEHAVIOR:
  storageUrl = "{containerUri}/{correlationId}/{filename}.pdf"
  → Direct Azure Blob Storage URL

NEW BEHAVIOR:
  storageUrl = "{requestBaseUrl}/api/v1/documents/{correlationId}/{filename}.pdf"
  → API proxy URL

Implementation:
  1. Extract requestBaseUrl from HttpContext.Request (Scheme + Host)
  2. After uploading certificate/history docs:
     a. Instead of: GetFolderUrl(correlationId) + filename
     b. Use: $"{requestBaseUrl}/api/v1/documents/{correlationId}/{filename}"
  3. Apply to:
     - certificate.storageUrl (CertificateResult)
     - historyDocuments[].storageUrl (HistoryDocumentResult)
     - audit entry URLs (CertificateUrl, DiagnosticsUrl remain blob URLs for internal tracing)
```

**Note**: The audit trail still stores the direct blob URL for operational purposes. Only the API response `storageUrl` values are rewritten to proxy URLs.
