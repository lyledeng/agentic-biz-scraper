# Data Model: WY Certificate of Good Standing

**Feature**: 009-wy-certificate-good-standing
**Date**: 2026-03-26

## Overview

This feature does not introduce any new data models or modify existing ones. The WY certificate retrieval reuses the existing models established in features 004 (CO certificate) and 006 (JSON scraping engine).

## Existing Models (Reused As-Is)

### CertificateResult

**File**: `src/BizScraper.Api/Features/EntityDetails/Models/CertificateResult.cs`

```csharp
public sealed record CertificateResult(
    bool Available,
    string? StorageUrl,
    string? FileName,
    string? Error);
```

| Field | WY Semantics |
|-------|-------------|
| `Available` | `true` only when the certificate PDF was successfully downloaded (per clarification Q4) |
| `StorageUrl` | Cloud storage URL after upload, e.g., `{containerUrl}/{correlationId}/{filingId}-certificateofgoodstanding.pdf` |
| `FileName` | `{filingId}-certificateofgoodstanding.pdf`, e.g., `2017-000757675-certificateofgoodstanding.pdf` |
| `Error` | Populated when download succeeded but upload failed; null on full success |

### EntityDetailScrapeResult

**File**: `src/BizScraper.Api/Features/EntityDetails/Models/EntityDetailScrapeResult.cs`

```csharp
public sealed record EntityDetailScrapeResult(
    EntityDetailResult Details,
    RegisteredAgentResult? RegisteredAgent,
    bool CertificateAvailable,
    string? CertificateLocalPath,
    IReadOnlyList<PartyResult>? Parties = null,
    IReadOnlyList<HistoryDocumentResult>? HistoryDocuments = null);
```

The `CertificateAvailable` and `CertificateLocalPath` fields are already populated by `MapToResult()` based on the `certificateLocalPath` variable in the action context. No changes needed.

## Flow Definition Extension

### wy-entity-details.json — New Actions (Appended)

The following actions are appended after the existing `historyDocuments` extraction and before the `output` declaration:

| Step | Action Type | Description |
|------|-------------|-------------|
| 1 | `navigate` | Navigate to `https://wyobiz.wyo.gov/Business/ViewCertificate.aspx` |
| 2 | `screenshot` | Capture certificate search page |
| 3 | `fill` | Enter Filing ID (`${idNumber}`) in the Filing ID textbox |
| 4 | `click` | Click Search button, wait for networkidle |
| 5 | `screenshot` | Capture entity verification page |
| 6 | `download` | Click Print button, capture PDF, validate `.pdf`, store as `certificateLocalPath` |
| 7 | `screenshot` | Capture post-download state |

### New Variable

| Variable | Source | Purpose |
|----------|--------|---------|
| `certificateLocalPath` | `extracted` | Local path to the downloaded certificate PDF (set by `download` action's `storeAs`) |

This variable already exists in the CO flow definition and is already handled by `MapToResult()` and `GetEntityDetailsHandler`.

## Blob Storage Path Convention

Follows the existing per-request folder convention from feature 003:

```
{containerName}/{correlationId}/{filingId}-certificateofgoodstanding.pdf
```

Example:
```
playwright-diagnostics/abc-123-def/2017-000757675-certificateofgoodstanding.pdf
```

## Audit Trail

No schema changes. The existing `CertificateUrl` column in the entity details audit table (added in feature 004) stores the cloud storage URL of the uploaded certificate PDF. The handler already persists this for CO; the same code path handles WY.

## Database Changes

**None** — no Liquibase changelogs required for this feature.
