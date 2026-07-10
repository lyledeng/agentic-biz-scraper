# Research: Cloud Screenshot Storage & Per-Request Folder Organization

**Feature**: `003-cloud-screenshot-storage`
**Date**: 2026-03-24

## R1: Azure.Storage.Blobs SDK for .NET

### Decision
Use `Azure.Storage.Blobs` (latest stable, currently v12.x) via Central Package Management. Register `BlobServiceClient` as singleton; use `BlobContainerClient` for all operations.

### Rationale
- Official Azure SDK for .NET, actively maintained, supports .NET 10.
- `BlobServiceClient` is thread-safe and designed for singleton registration.
- `BlobContainerClient.CreateIfNotExistsAsync()` provides atomic container auto-creation.
- `BlobClient.UploadAsync(Stream, overwrite: true)` handles individual file uploads.
- Connection-string constructor: `new BlobServiceClient(connectionString)`.

### Alternatives Considered
- **Azure.Storage.Blobs.Batch**: Overkill — we upload files one at a time as they're produced, not in bulk.
- **Flurl.Http to Blob REST API**: Would bypass SDK benefits (retry policies, connection pooling, SAS handling). Rejected.
- **Azure.Storage.Files.Shares**: File shares use SMB semantics; Blob Storage is the correct fit for unstructured artifact storage.

### Key API Surface

```csharp
// Startup — container auto-creation
var blobServiceClient = new BlobServiceClient(connectionString);
var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken);

// Upload — per file
var blobClient = containerClient.GetBlobClient($"{correlationId}/{filename}");
await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

// Folder URL construction
var folderUrl = $"{containerClient.Uri}/{correlationId}";
```

---

## R2: Retry Strategy for Cloud Uploads

### Decision
Use Polly 8.x `ResiliencePipeline` (already in project) with a dedicated pipeline for blob uploads: 3 retries, exponential backoff (1s, 2s, 4s base delays), jitter enabled. Wrap only transient Azure SDK exceptions.

### Rationale
- The project already uses Polly 8.6.6 with `ResiliencePipelineBuilder` for scraper retries. Following the same pattern ensures consistency.
- Azure SDK throws `RequestFailedException` with HTTP status codes. Only transient codes (408, 429, 500, 502, 503, 504) should trigger retry.
- Exponential backoff with jitter prevents thundering herd on shared storage accounts.
- Per-file timeout of 30s prevents slow uploads from accumulating.

### Alternatives Considered
- **Azure SDK built-in retry**: The SDK has its own retry policy via `BlobClientOptions.Retry`. However, it doesn't provide the same observability (logging on each retry attempt) that Polly's `OnRetry` callback gives us. We can use both — SDK retry for transport-level retries, Polly for application-level retry orchestration. Decision: use Polly only, disable SDK retry to avoid double-retry.
- **No retry (fire-and-forget)**: Spec requires 3× retry. Rejected.

### Implementation Pattern

```csharp
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = new PredicateBuilder().Handle<RequestFailedException>(ex =>
            ex.Status is 408 or 429 or 500 or 502 or 503 or 504)
    })
    .AddTimeout(TimeSpan.FromSeconds(30))
    .Build();
```

---

## R3: Per-Request Subfolder Strategy

### Decision
Modify `BuildDiagnosticArtifactPath` to include a `{sanitizedCorrelationId}` subdirectory between the output root and the filename. The same correlation ID subfolder is used as the blob virtual folder prefix.

### Rationale
- Current path: `{outputDir}/{correlationId}_{timestamp}_{milestone}.{ext}` (flat)
- New path: `{outputDir}/{correlationId}/{timestamp}_{milestone}.{ext}` (subfolder)
- The correlation ID is already sanitized via `SanitizeFileNameSegment` — same sanitization is valid for both filesystem directories and blob virtual folder names.
- `TryEnsureDiagnosticsOutputDirectory` already calls `Directory.CreateDirectory`, which handles nested paths.
- Blob Storage uses `/` in blob names as virtual folder separators — no actual directory creation needed.

### Alternatives Considered
- **Hash-based subfolder** (e.g., first 2 chars of correlation ID as prefix): Adds complexity for no benefit in this scale. Rejected.
- **Date-based subfolder** (e.g., `2026/03/24/{correlationId}/`): Would help with time-based browsing but adds complexity. The correlation ID is sufficient for lookup. Rejected for YAGNI.

### Impact on Existing Code
- `BuildDiagnosticArtifactPath`: Add `correlationId` as subdirectory in path construction.
- `TryEnsureDiagnosticsOutputDirectory`: Now takes correlationId to create per-request subdirectory, or the caller appends it.
- Existing unit tests for `BuildDiagnosticArtifactPath`: Must be updated to expect the new path structure.

---

## R4: Async Upload Pattern (Non-Blocking)

### Decision
Upload artifacts inline within `CaptureScreenshotAsync` and `StopTracingAsync` using fire-and-forget with `Task.Run` + error swallowing. The upload does NOT block the search response because the diagnostics methods are already called in a non-critical path (errors are caught, logged, and swallowed).

### Rationale
- The existing diagnostics methods (`CaptureScreenshotAsync`, `StopTracingAsync`) already swallow all exceptions and log warnings. Cloud upload follows the same pattern.
- The scraper's `ExecuteWithDiagnosticsAsync` calls diagnostics in a `try/catch/finally` that never propagates diagnostic failures.
- Using `Task.Run` for fire-and-forget could cause uploads to be abandoned if the process shuts down. However, since local files are always retained (spec requirement), this is acceptable.
- Alternative: collect upload tasks and await them in `ExecuteWithDiagnosticsAsync` finally block. This is better because it keeps uploads within the request lifetime and allows the DI container to manage the blob client lifecycle properly. **Chosen approach**: collect upload tasks, await them in the `finally` block of `ExecuteWithDiagnosticsAsync`.

### Alternatives Considered
- **Background queue (Channel\<T\>)**: Adds a hosted service, queue management, and shutdown coordination. Over-engineered for the current scale (max 1 concurrent request). Rejected for YAGNI.
- **`IHostedService` with background upload queue**: Same as above. Adds complexity without benefit at current scale.
- **Inline await in each capture method**: Would add upload latency to the scraping flow. However, since diagnostics are already async and non-blocking to the caller, the upload latency only affects the `finally` block duration, not the search response. This is acceptable and simpler than fire-and-forget. **This is the chosen approach** — inline await within the diagnostic methods, with try/catch/log/swallow around the upload call.

### Chosen Pattern

```csharp
// In CaptureScreenshotAsync, after local save:
if (cloudStorageEnabled)
{
    try
    {
        await _blobStorageClient.UploadAsync(containerName, $"{correlationId}/{filename}", screenshotPath, cancellationToken);
    }
    catch (Exception ex)
    {
        logger.CloudUploadFailed("screenshot", screenshotPath, ex);
    }
}
```

---

## R5: Audit Trail Schema Migration

### Decision
Add a nullable `NVARCHAR(2000)` column `DiagnosticsUrl` to `dbo.SearchAuditEntries` via a new Liquibase changeset `1.0.2603.1-add-diagnostics-url-column.sql`.

### Rationale
- Following the existing Liquibase convention: `{version}-{description}.sql` with `-- liquibase formatted sql` header.
- 2000 characters accommodates the longest reasonable Azure Blob Storage URL (account + container + correlation ID).
- Nullable because the field is empty when cloud upload is disabled.
- The master file `master-1.0.2603.0.yaml` gets a new include entry.

### Migration SQL

```sql
-- liquibase formatted sql

-- changeset cososwebscraping:1.0.2603.1-add-diagnostics-url-column
ALTER TABLE dbo.SearchAuditEntries
    ADD DiagnosticsUrl NVARCHAR(2000) NULL;
```

---

## R6: Configuration Design

### Decision
Nest cloud storage settings under `Playwright:Diagnostics:CloudStorage` as a new `CloudStorageOptions` sub-class within `DiagnosticsOptions`.

### Rationale
- Follows the existing pattern: `Playwright` → `Diagnostics` → sub-properties.
- Keeps all diagnostics config grouped under one section.
- `Enabled` defaults to `false` (opt-in).

### Configuration Shape

```json
{
  "Playwright": {
    "Diagnostics": {
      "TracingMode": "Always",
      "ScreenshotsEnabled": true,
      "OutputPath": "diagnostics",
      "CloudStorage": {
        "Enabled": false,
        "ConnectionString": "",
        "ContainerName": "diagnostics"
      }
    }
  }
}
```

### Options Validation
- When `Enabled` is `true`: `ConnectionString` and `ContainerName` must be non-empty (fail-fast at startup).
- When `Enabled` is `false`: No validation needed, no blob client initialized.

---

## R7: IBlobStorageClient Abstraction

### Decision
Define `IBlobStorageClient` in `Common/Interfaces/` with a single `UploadAsync` method. Implement in `Infrastructure/Storage/AzureBlobStorageClient`. Register conditionally in DI based on `CloudStorage.Enabled`.

### Rationale
- Follows Dependency Inversion (Constitution II): scraper depends on abstraction, not Azure SDK.
- Enables unit testing with NSubstitute mock.
- Follows existing pattern: `IBusinessEntityScraper` in `Common/Interfaces/`, implementation in `Infrastructure/`.
- Conditional registration: when disabled, register a no-op implementation or don't register at all (scraper checks config before calling).

### Interface Design

```csharp
public interface IBlobStorageClient
{
    Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken);
    Uri? GetFolderUrl(string folderPath);
}
```

- `blobPath`: virtual path in the container (e.g., `{correlationId}/{filename}`).
- `localFilePath`: absolute path to the local file to upload.
- `GetFolderUrl`: accepts the **raw** correlation ID, applies sanitization internally (same rules as `SanitizeFileNameSegment`), and returns the full `Uri` for the per-request folder (for audit trail). Returns `null` in the `NullBlobStorageClient` implementation.

### DI Registration Pattern

```csharp
var cloudStorageOptions = builder.Configuration
    .GetSection("Playwright:Diagnostics:CloudStorage")
    .Get<CloudStorageOptions>();

if (cloudStorageOptions?.Enabled == true)
{
    // Validate + register real client
    builder.Services.AddSingleton<IBlobStorageClient>(sp => 
        new AzureBlobStorageClient(cloudStorageOptions, logger));
}
else
{
    builder.Services.AddSingleton<IBlobStorageClient, NullBlobStorageClient>();
}
```
