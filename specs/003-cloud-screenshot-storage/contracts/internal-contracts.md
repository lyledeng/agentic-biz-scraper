# Contracts: Cloud Screenshot Storage & Per-Request Folder Organization

**Feature**: `003-cloud-screenshot-storage`
**Date**: 2026-03-24

## External API Impact

**No changes to external API contracts.** The existing `GET /api/v1/business-search` endpoint response schema is unchanged. This feature only affects internal diagnostics infrastructure and the audit trail database schema.

## Internal Contracts

### 1. IBlobStorageClient Interface Contract

```csharp
namespace CososWebScraping.Api.Common.Interfaces;

public interface IBlobStorageClient
{
    /// <summary>
    /// Uploads a local file to the configured blob container.
    /// Retries up to 3 times with exponential backoff on transient failures.
    /// Throws on permanent failure after retries are exhausted.
    /// </summary>
    Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the full URL to a virtual folder in the blob container.
    /// Applies sanitization to the input folderPath internally.
    /// Returns null when cloud upload is disabled (NullBlobStorageClient).
    /// </summary>
    Uri? GetFolderUrl(string folderPath);
}
```

**Guarantees**:
- `UploadAsync` retries transient `RequestFailedException` (HTTP 408, 429, 5xx) up to 3 times with exponential backoff.
- `UploadAsync` times out after 30 seconds per attempt.
- `GetFolderUrl` is a pure computation (no I/O) — sanitizes the input `folderPath` using the same character-replacement rules as `SanitizeFileNameSegment`, then returns a `Uri` for `"{containerUri}/{sanitizedFolderPath}"`. Callers pass the raw correlation ID.
- `NullBlobStorageClient` (when cloud upload disabled): `UploadAsync` returns `Task.CompletedTask`; `GetFolderUrl` returns `null`.

### 2. Configuration Contract

**Path**: `Playwright:Diagnostics:CloudStorage`

```json
{
  "Enabled": false,
  "ConnectionString": "",
  "ContainerName": "diagnostics"
}
```

**Validation rules** (enforced at startup via `ValidateOnStart`):
- When `Enabled` is `true`: `ConnectionString` must be non-empty, `ContainerName` must be non-empty.
- When `Enabled` is `false`: No validation. No blob client initialized.

### 3. Audit Trail Schema Contract

**Column addition**: `DiagnosticsUrl NVARCHAR(2000) NULL`

- Populated with the Azure Blob Storage folder URL when cloud upload is enabled.
- `NULL` when cloud upload is disabled.
- Populated with the intended URL even if the upload itself fails (per FR-014).

### 4. Local Filesystem Contract

**Before** (flat):
```
{outputPath}/{correlationId}_{timestamp}_{milestone}.{ext}
```

**After** (per-request subfolder):
```
{outputPath}/{correlationId}/{timestamp}_{milestone}.{ext}
```

This is a **breaking change for external tools** that parse the flat directory structure. The change is intentional and documented in the spec.
