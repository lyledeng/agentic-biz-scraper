# Data Model: WY History Document Cloud Storage

**Feature**: 008-wy-history-doc-storage  
**Date**: 2026-03-26

## Entities

### HistoryDocumentResult (existing — no structural changes)

The existing record from spec 007 is reused without modification. The `StorageUrl` field is overwritten at runtime with the cloud storage URL after successful upload.

| Field | Type | Description |
|-------|------|-------------|
| EventTitle | string | Document event title (e.g., "2025 Original Annual Report - 11347360") |
| Date | string | Filing date (e.g., "05/20/2025") |
| StorageUrl | string? | Initially WY SOS URL; replaced with Azure Blob Storage URL after upload. Retains WY SOS URL if upload fails. |
| FileName | string? | Slugified filename (e.g., "2025-original-annual-report-11347360.pdf") |
| Error | string? | Null on success; populated with error message if download or upload fails |

**Source file**: `src/BizScraper.Api/Features/EntityDetails/Models/HistoryDocumentResult.cs`

### EntityDetailAuditEntry (modified — add DocumentStorageUrl)

| Field | Type | Description | Change |
|-------|------|-------------|--------|
| Id | Guid | Primary key | Existing |
| DetailsUrl | string | The entity details URL | Existing |
| EntityIdNumber | string? | Entity filing/ID number | Existing |
| EntityName | string? | Entity name | Existing |
| EntityStatus | string? | Entity status | Existing |
| CertificateUrl | string? | Certificate of good standing URL | Existing |
| DiagnosticsUrl | string? | Diagnostics folder URL in cloud storage | Existing |
| **DocumentStorageUrl** | **string?** | **Cloud storage folder URL for history documents** | **NEW** |
| DurationMs | long | Request processing duration in milliseconds | Existing |
| Status | string | Outcome status (Success, Error, etc.) | Existing |
| ErrorDetail | string? | Error message if any | Existing |
| CorrelationId | string | Request correlation ID | Existing |
| CreatedAt | DateTimeOffset | Timestamp | Existing |
| StateCode | string? | State code (CO, WY) | Existing |

**Source file**: `src/BizScraper.Api/Domain/Entities/EntityDetailAuditEntry.cs`

### EntityDetailScrapeResult (existing — no changes needed)

The existing record already includes `IReadOnlyList<HistoryDocumentResult>? HistoryDocuments`. No changes needed.

## Data Flow

```
WY SOS Details Page
    │
    ▼
[JSON Scraping Engine]──→ Extract ALL history documents (no 3-doc limit)
    │
    ▼
EntityDetailScrapeResult.HistoryDocuments (with WY SOS URLs)
    │
    ▼
[GetEntityDetailsHandler] ── For each document sequentially:
    │
    ├──→ Download via Playwright browser context (APIRequestContext.GetAsync)
    │
    ├──→ Validate content-type = application/pdf
    │
    ├──→ Save to local: {diagOutputPath}/{correlationId}/{fileName}
    │
    ├──→ Upload to blob: {correlationId}/{fileName}
    │
    ├──→ On success: Replace storageUrl with blob URL
    │
    └──→ On failure: Log warning, set error field, keep original URL
    │
    ▼
EntityDetailResponse.HistoryDocuments (with cloud storage URLs)
    │
    ▼
Audit Trail: DocumentStorageUrl = blob folder URL
```

## Database Changes

### New Column: DocumentStorageUrl

**Table**: `dbo.EntityDetailAuditEntries`  
**Column**: `DocumentStorageUrl NVARCHAR(2000) NULL`  
**Liquibase changeset**: `2.0.2603.4-add-document-storage-url-to-entity-detail-audit`

**Rollback**: `ALTER TABLE dbo.EntityDetailAuditEntries DROP COLUMN DocumentStorageUrl;`

## Blob Storage Path Convention

```
{containerName}/
└── {sanitizedCorrelationId}/
    ├── 20260326T..._wy-detail-01-page-load.png      ← Existing (screenshots)
    ├── 20260326T..._wy-detail-02-after-captcha.png   ← Existing (screenshots)
    ├── 20260326T..._wy-detail-03-after-extraction.png ← Existing (screenshots)
    ├── trace.zip                                       ← Existing (trace)
    ├── 2025-original-annual-report-11347360.pdf       ← NEW (history document)
    ├── 2024-original-annual-report-09899282.pdf       ← NEW (history document)
    └── 2023-original-annual-report-08502883.pdf       ← NEW (history document)
```

Documents share the same per-request correlation ID folder as screenshots and traces.
