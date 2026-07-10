# Data Model: CO Entity Certified Document Download

**Feature**: 034-co-entity-doc-download  
**Date**: 2026-04-08

## Changes to Existing Models

### EntityDetailResponse (CO extension)

The CO entity details response gains a new `certifiedDocumentsUrl` field alongside existing `certificateUrl`. No new C# model types are needed — the field flows through as a string property.

#### Output Compose Block (us-co-entity-details.json)

| Property | Variable | Type | Description |
|----------|----------|------|-------------|
| details | details | object | Entity detail fields (existing) |
| registeredAgent | registeredAgent | object | Registered agent fields (existing) |
| certificateUrl | certificateLocalPath | string? | Certificate of good standing proxy URL (existing) |
| **certifiedDocumentsUrl** | **certifiedDocumentsLocalPath** | **string?** | **Certified copies "all documents" proxy URL (new)** |

#### Mapped Response (EntityDetailMapper.MapCoDetails)

The mapper reads `certifiedDocumentsUrl` from the raw compose output. When present and non-empty, it is added as a second `DownloadReference` entry in `Certificate.Downloads` array with label "Certified Documents". If `certificateUrl` is empty but `certifiedDocumentsUrl` is present, the `Certificate` section is still created with just the certified docs entry. This reuses the existing `CertificateSection` and `DownloadReference` types with no model changes.

**Decision**: Map as additional `DownloadReference` in `Certificate.Downloads` array. This keeps the response shape flat, reuses existing types, and the test UI already iterates `certificate.downloads` for rendering buttons.

### New Variables (us-co-entity-details.json)

| Variable | Source | Description |
|----------|--------|-------------|
| certifiedDocumentsViewerUrl | extracted | URL from "open your form in a new window" href on certificate print page |
| certifiedDocumentsLocalPath | extracted | Proxy URL after post-processing upload (or null/error) |

### New Actions (us-co-entity-details.json)

| Action ID | Order | Type | Description | Condition |
|-----------|-------|------|-------------|-----------|
| action-005 | 5 | click | Click "Get certified copies of documents" link | element-exists: `a:has-text('Get certified copies of documents')` |
| action-006 | 6 | screenshot | Capture certified copies list page | element-exists: `a:has-text('All documents')` |
| action-007 | 7 | click | Click "All documents" link, wait for networkidle | element-exists: `a:has-text('All documents')` |
| action-008 | 8 | extract | Extract CertificationViewer.do URL from "open your form in a new window" href | element-exists: `a[href*='CertificationViewer.do']` |

### Post-Processing (GenericScriptScraper)

After action execution, CO entity detail post-processing will:
1. Check if `certifiedDocumentsViewerUrl` exists in the output
2. If present, download the PDF via `browserContext.APIRequest.GetAsync()`
3. Validate content-type and non-empty body
4. Save locally as `{idNumber}-alldocuments.pdf`
5. Upload to blob storage under `{blobPrefix}/{filename}`
6. Replace `certifiedDocumentsLocalPath` with proxy URL
7. On failure, set `certifiedDocumentsLocalPath` to null and log the error

## No New Database Changes

No schema changes required. The certified documents URL is returned in the response but NOT stored in the audit table (consistent with how `certificateUrl` is stored — it's derived from the blob path at response time).

## No New C# Model Types

The `certifiedDocumentsUrl` is a string that flows through the compose block and mapper. The `UnifiedEntityDetailResponse` may need a minor extension to carry it, but this is a single property addition, not a new type.
