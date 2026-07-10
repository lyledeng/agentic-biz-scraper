# Data Model: Cloud Screenshot Storage & Per-Request Folder Organization

**Feature**: `003-cloud-screenshot-storage`
**Date**: 2026-03-24

## Entities

### 1. CloudStorageOptions (New)

**Location**: `src/CososWebScraping.Api/Common/Configuration/DiagnosticsOptions.cs` (nested class or companion class)

| Property | Type | Default | Validation | Description |
|----------|------|---------|------------|-------------|
| `Enabled` | `bool` | `false` | — | Master toggle for cloud upload. When false, no blob client is initialized. |
| `ConnectionString` | `string` | `""` | Required when `Enabled=true` | Azure Blob Storage connection string. Sourced from Key Vault (deployed) or User Secrets (local). |
| `ContainerName` | `string` | `"diagnostics"` | Required when `Enabled=true`, non-empty | Blob container name. Auto-created at startup if missing. |

**Relationships**: Owned by `DiagnosticsOptions` as a sub-property `CloudStorage`.

**Configuration path**: `Playwright:Diagnostics:CloudStorage`

---

### 2. DiagnosticsOptions (Modified)

**Location**: `src/CososWebScraping.Api/Common/Configuration/DiagnosticsOptions.cs`

| Property | Type | Default | Change |
|----------|------|---------|--------|
| `TracingMode` | `TracingMode` | `Always` | Unchanged |
| `ScreenshotsEnabled` | `bool` | `true` | Unchanged |
| `OutputPath` | `string` | `"diagnostics"` | Unchanged |
| `CloudStorage` | `CloudStorageOptions` | `new()` | **NEW** — nested cloud storage configuration |

---

### 3. SearchAuditEntry (Modified)

**Location**: `src/CososWebScraping.Api/Domain/Entities/SearchAuditEntry.cs`

| Property | Type | Default | Change |
|----------|------|---------|--------|
| `Id` | `Guid` | `Guid.NewGuid()` | Unchanged |
| `SearchTerm` | `string` | required | Unchanged |
| `ResultCount` | `int` | — | Unchanged |
| `Truncated` | `bool` | — | Unchanged |
| `PagesScraped` | `int` | — | Unchanged |
| `DurationMs` | `long` | — | Unchanged |
| `Status` | `string` | required | Unchanged |
| `ErrorDetail` | `string?` | `null` | Unchanged |
| `CreatedAt` | `DateTimeOffset` | `UtcNow` | Unchanged |
| `CorrelationId` | `string` | required | Unchanged |
| `DiagnosticsUrl` | `string?` | `null` | **NEW** — cloud storage folder URL for this request's artifacts |

---

### 4. IBlobStorageClient (New Interface)

**Location**: `src/CososWebScraping.Api/Common/Interfaces/IBlobStorageClient.cs`

```csharp
public interface IBlobStorageClient
{
    Task UploadAsync(string blobPath, string localFilePath, CancellationToken cancellationToken);
  Uri? GetFolderUrl(string folderPath);
}
```

| Method | Parameters | Returns | Description |
|--------|------------|---------|-------------|
| `UploadAsync` | `blobPath` (virtual blob path, e.g. `{correlationId}/{filename}`), `localFilePath` (absolute local path), `cancellationToken` | `Task` | Uploads a local file to the configured blob container. Includes retry (3×, exponential backoff). |
| `GetFolderUrl` | `folderPath` (raw correlation ID — the method applies sanitization internally via `SanitizeFileNameSegment` rules) | `Uri?` | Returns the full URL to the virtual folder in the blob container, or `null` when cloud upload is disabled (`NullBlobStorageClient`). Used for the audit trail `DiagnosticsUrl`. |

---

### 5. AzureBlobStorageClient (New Implementation)

**Location**: `src/CososWebScraping.Api/Infrastructure/Storage/AzureBlobStorageClient.cs`

| Dependency | Type | Source |
|------------|------|--------|
| `BlobContainerClient` | Azure SDK | Created from `CloudStorageOptions.ConnectionString` + `ContainerName` |
| `ResiliencePipeline` | Polly 8.x | Built internally — 3 retries, exponential backoff, 30s timeout |
| `ILogger<AzureBlobStorageClient>` | Microsoft.Extensions.Logging | DI |

**State transitions**: None (stateless — each call is independent).

---

### 6. NullBlobStorageClient (New — No-op Implementation)

**Location**: `src/CososWebScraping.Api/Infrastructure/Storage/NullBlobStorageClient.cs`

No-op implementation of `IBlobStorageClient`. Registered when `CloudStorage.Enabled` is `false`. `UploadAsync` returns `Task.CompletedTask`. `GetFolderUrl` returns `null`.

---

## Database Schema Change

### Table: `dbo.SearchAuditEntries`

**Migration**: `1.0.2603.1-add-diagnostics-url-column.sql`

```sql
-- liquibase formatted sql

-- changeset cososwebscraping:1.0.2603.1-add-diagnostics-url-column
ALTER TABLE dbo.SearchAuditEntries
    ADD DiagnosticsUrl NVARCHAR(2000) NULL;
```

**Column details**:
- Name: `DiagnosticsUrl`
- Type: `NVARCHAR(2000)`
- Nullable: Yes
- Default: `NULL`
- Purpose: Stores the Azure Blob Storage folder URL where diagnostic artifacts for this request are located. NULL when cloud upload is disabled.

---

## Relationships

```
PlaywrightOptions
  └── DiagnosticsOptions
        ├── TracingMode
        ├── ScreenshotsEnabled
        ├── OutputPath
        └── CloudStorageOptions (NEW)
              ├── Enabled
              ├── ConnectionString
              └── ContainerName

IBlobStorageClient (interface)
  ├── AzureBlobStorageClient (when Enabled=true)
  └── NullBlobStorageClient  (when Enabled=false)

PlaywrightBusinessEntityScraper
  ├── depends on → IBlobStorageClient (NEW)
  ├── depends on → IOptions<PlaywrightOptions>
  └── produces → local files + cloud uploads

SearchBusinessEntitiesHandler
  ├── depends on → IBusinessEntityScraper
  ├── depends on → AuditTrailRepository
  └── writes → SearchAuditEntry.DiagnosticsUrl (NEW)

AuditTrailRepository
  └── persists → SearchAuditEntry (including DiagnosticsUrl)
```

## Validation Rules

| Entity | Rule | Enforced At |
|--------|------|-------------|
| `CloudStorageOptions` | `ConnectionString` non-empty when `Enabled=true` | Startup (`ValidateOnStart`) |
| `CloudStorageOptions` | `ContainerName` non-empty when `Enabled=true` | Startup (`ValidateOnStart`) |
| `DiagnosticsUrl` | Must be well-formed URL or null | Application logic (constructed, not user input) |
| `DiagnosticsUrl` | Max 2000 chars | Database column constraint |
| Blob path segments | Sanitized via `SanitizeFileNameSegment` | Application logic (existing method) |
