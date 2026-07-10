# Data Model: Entity Details Retrieval & Certificate of Good Standing Download

**Feature**: 004-entity-details-endpoint
**Date**: 2026-03-24

## Entities

### EntityDetailAuditEntry (Persistence Entity)

**Table**: `dbo.EntityDetailAuditEntries`
**Purpose**: Dedicated audit trail for entity detail retrieval requests. Each row represents one API call and its outcome.

| Field | C# Type | SQL Type | Nullable | Description |
|-------|---------|----------|----------|-------------|
| Id | `Guid` | `UNIQUEIDENTIFIER` | NOT NULL (PK) | Auto-generated unique ID |
| DetailsUrl | `string` | `NVARCHAR(2000)` | NOT NULL | The CO SOS entity details URL that was requested |
| EntityIdNumber | `string?` | `NVARCHAR(50)` | NULL | Entity ID number scraped from the page (null on failure before scraping) |
| EntityName | `string?` | `NVARCHAR(500)` | NULL | Entity legal name scraped from the page |
| EntityStatus | `string?` | `NVARCHAR(50)` | NULL | Entity status (e.g., "Good Standing", "Delinquent") |
| CertificateUrl | `string?` | `NVARCHAR(2000)` | NULL | Cloud storage URL of the downloaded certificate PDF (FR-011) |
| DiagnosticsUrl | `string?` | `NVARCHAR(2000)` | NULL | Cloud storage URL for diagnostic artifacts folder |
| DurationMs | `long` | `BIGINT` | NOT NULL | Total request duration in milliseconds |
| Status | `string` | `NVARCHAR(50)` | NOT NULL | Outcome status (Success, ValidationError, ScrapingError, UpstreamError, Busy, Error) |
| ErrorDetail | `string?` | `NVARCHAR(2000)` | NULL | Error message when Status is not Success |
| CorrelationId | `string` | `NVARCHAR(100)` | NOT NULL | Request trace identifier for cross-cutting correlation |
| CreatedAt | `DateTimeOffset` | `DATETIMEOFFSET` | NOT NULL | UTC timestamp of when the audit entry was created |

**C# class** (follows `SearchAuditEntry` pattern):
```csharp
namespace CososWebScraping.Api.Domain.Entities;

public sealed class EntityDetailAuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string DetailsUrl { get; init; }
    public string? EntityIdNumber { get; init; }
    public string? EntityName { get; init; }
    public string? EntityStatus { get; init; }
    public string? CertificateUrl { get; init; }
    public string? DiagnosticsUrl { get; init; }
    public long DurationMs { get; init; }
    public required string Status { get; init; }
    public string? ErrorDetail { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

**Liquibase changelog** (`1.0.2603.2-create-entity-detail-audit-table.sql`):
```sql
-- liquibase formatted sql

-- changeset cososwebscraping:1.0.2603.2-create-entity-detail-audit-table
CREATE TABLE dbo.EntityDetailAuditEntries (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    DetailsUrl NVARCHAR(2000) NOT NULL,
    EntityIdNumber NVARCHAR(50) NULL,
    EntityName NVARCHAR(500) NULL,
    EntityStatus NVARCHAR(50) NULL,
    CertificateUrl NVARCHAR(2000) NULL,
    DiagnosticsUrl NVARCHAR(2000) NULL,
    DurationMs BIGINT NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    ErrorDetail NVARCHAR(2000) NULL,
    CorrelationId NVARCHAR(100) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL
);

-- rollback DROP TABLE dbo.EntityDetailAuditEntries;
```

---

## API Models (Records)

### EntityDetailRequest

**Purpose**: Thin wrapper for the incoming query parameter, with URL validation.

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Models;

public sealed record EntityDetailRequest(string DetailsUrl);
```

### EntityDetailResponse

**Purpose**: Top-level API response returned to callers.

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Models;

public sealed record EntityDetailResponse(
    EntityDetailResult Details,
    RegisteredAgentResult? RegisteredAgent,
    CertificateResult? Certificate);
```

### EntityDetailResult

**Purpose**: Represents the scraped "Details" table from the CO SOS entity page.

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Models;

public sealed record EntityDetailResult(
    string Name,
    string Status,
    string FormationDate,
    string IdNumber,
    string Form,
    string? PeriodicReportMonth,
    string Jurisdiction,
    string? PrincipalOfficeStreetAddress,
    string? PrincipalOfficeMailingAddress);
```

**Field nullability rationale**: PeriodicReportMonth, PrincipalOfficeStreetAddress, and PrincipalOfficeMailingAddress are nullable because some entity types or jurisdictions may not have these fields populated.

### RegisteredAgentResult

**Purpose**: Represents the scraped "Registered Agent" table.

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Models;

public sealed record RegisteredAgentResult(
    string? Name,
    string? StreetAddress,
    string? MailingAddress);
```

**All fields nullable**: Some entities may not have a registered agent or may have partial information.

### CertificateResult

**Purpose**: Represents the certificate of good standing status and download result.

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Models;

public sealed record CertificateResult(
    bool Available,
    string? StorageUrl,
    string? FileName,
    string? Error);
```

- `Available`: `true` when the "Get a certificate of good standing" link exists on the page
- `StorageUrl`: Cloud storage URL of the uploaded PDF (null if unavailable, not downloaded, or upload failed)
- `FileName`: The constructed filename (e.g., `20051063025-certificateofgoodstanding.pdf`)
- `Error`: Error message if the certificate was available but download/upload failed (FR-009: details are still returned)

---

## Internal Models

### EntityDetailScrapeResult

**Purpose**: Internal result from the scraper, not exposed via API. Contains all scraped data and the certificate download result.

```csharp
namespace CososWebScraping.Api.Features.EntityDetails.Models;

public sealed record EntityDetailScrapeResult(
    EntityDetailResult Details,
    RegisteredAgentResult? RegisteredAgent,
    bool CertificateAvailable,
    string? CertificateLocalPath);
```

- `CertificateLocalPath`: Temporary local file path of the downloaded PDF (for subsequent blob upload by the handler). Null if certificate was not available or download failed.

---

## Relationships

```text
EntityDetailResponse (API boundary)
├── EntityDetailResult        1:1 (always present on success)
├── RegisteredAgentResult     0:1 (nullable — may not exist on page)
└── CertificateResult         0:1 (nullable — only when cert link exists)

EntityDetailAuditEntry (persistence)
└── Standalone row per request, no FK relationships
    ├── CertificateUrl        explicit column (FR-011)
    └── DiagnosticsUrl        explicit column (mirrors SearchAuditEntry)
```

---

## JSON Serialization Context Registration

New types must be registered in `AppJsonSerializerContext.cs`:

```csharp
[JsonSerializable(typeof(EntityDetailResponse))]
[JsonSerializable(typeof(EntityDetailResult))]
[JsonSerializable(typeof(RegisteredAgentResult))]
[JsonSerializable(typeof(CertificateResult))]
```
