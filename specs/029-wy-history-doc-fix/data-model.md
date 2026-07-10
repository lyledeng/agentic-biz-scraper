# Data Model: WY Filing History Document Download Fix

**Feature**: 029-wy-history-doc-fix  
**Date**: 2026-04-03

## Existing Entities (no schema changes)

### HistoryDocumentResult

Record used internally by the scraping engine. **No field changes required** — the existing fields support the upload flow.

| Field | Type | Description | Change |
|-------|------|-------------|--------|
| `EventTitle` | `string` | Filing event name (e.g., "2025 Original Annual Report - 11347360") | None |
| `Date` | `string` | Filing date (e.g., "05/20/2025") | None |
| `StorageUrl` | `string?` | **Before fix**: raw WY SOS URL. **After fix**: proxy URL (`/api/v1/documents/{blobPath}`) | Value changes (not schema) |
| `FileName` | `string?` | Sanitized PDF filename (e.g., `2025-original-annual-report-11347360.pdf`) | None |
| `Error` | `string?` | Error message if download or upload failed | None |
| `LocalFilePath` | `string?` | Local diagnostics path (`[JsonIgnore]`, transient) | None |

**Key change**: After the fix, `StorageUrl` will contain a proxy URL (e.g., `https://host/api/v1/documents/{correlationId}/2025-original-annual-report-11347360.pdf`) instead of the raw WY SOS URL (e.g., `https://wyobiz.wyo.gov/Business/GetImages.aspx?sid=...`).

### DocumentEntry (API response model)

Mapped from `HistoryDocumentResult` by `EntityDetailMapper.MapHistoryDocuments()`. **No changes required** — the mapper already passes `storageUrl` through to `ProxyUrl`.

| Field | Type | Description | Change |
|-------|------|-------------|--------|
| `Title` | `string?` | Filing event title | None |
| `Date` | `string?` | Filing date | None |
| `Downloads` | `DownloadReference[]` | Array of download references | None |

### DownloadReference (nested in DocumentEntry)

| Field | Type | Description | Change |
|-------|------|-------------|--------|
| `Label` | `string?` | Display label ("Filing Document") | None |
| `ProxyUrl` | `string?` | **Before fix**: raw WY SOS URL. **After fix**: proxy URL | Value changes (not schema) |
| `FileName` | `string?` | PDF filename | None |
| `Error` | `string?` | Error message if unavailable | None |

## Data Flow (before → after)

### Before (broken)

```
WY SOS website → APIRequest.GetAsync() → local file (diagDir) → HistoryDocumentResult.StorageUrl = raw WY URL
                                                                  ↓
EntityDetailMapper.MapHistoryDocuments() → DownloadReference.ProxyUrl = raw WY URL (inaccessible to user)
```

### After (fixed)

```
WY SOS website → APIRequest.GetAsync() → local file (diagDir) → blobStorageClient.UploadAsync(blobPath, localPath)
                                                                  ↓
                                                         HistoryDocumentResult.StorageUrl = proxy URL
                                                                  ↓
EntityDetailMapper.MapHistoryDocuments() → DownloadReference.ProxyUrl = proxy URL (accessible via document proxy)
```

## Blob Storage Path Convention

| Component | Value | Example |
|-----------|-------|---------|
| Blob prefix | `{correlationId}` | `f8eaff94843c473fba64337f38b409d2` |
| Blob path | `{correlationId}/{fileName}` | `f8eaff94843c473fba64337f38b409d2/2025-original-annual-report-11347360.pdf` |
| Proxy URL | `/api/v1/documents/{correlationId}/{fileName}` | `/api/v1/documents/f8eaff94843c473fba64337f38b409d2/2025-original-annual-report-11347360.pdf` |

## State Transitions

### Per-Document Lifecycle

```
[Extracted from WY page]
    ↓ (has storageUrl + fileName)
[Downloading] → APIRequest.GetAsync(storageUrl)
    ↓ success                    ↓ failure (timeout/bad content-type/empty)
[Downloaded Locally]          [Error Recorded]
    ↓ (localFilePath set)        → StorageUrl = null, Error = message
[Uploading to Blob]
    ↓ success                    ↓ failure (blob upload error)
[Upload Complete]             [Error Recorded]
    → StorageUrl = proxyUrl      → StorageUrl = null, Error = message
```
