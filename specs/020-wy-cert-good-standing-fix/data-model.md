# Data Model: WY Certificate of Good Standing Fix

**Feature**: 020-wy-cert-good-standing-fix  
**Date**: 2026-03-31

---

## Entities (No New Entities — Existing Only)

This feature modifies no data model entities. All changes are to flow
definition JSON and mapper code. The following existing entities are
relevant:

### CertificateResult (existing — `EntityDetails/Models/`)

| Field | Type | Description |
|-------|------|-------------|
| Available | bool | Whether the certificate was successfully downloaded |
| StorageUrl | string? | Proxy URL to the uploaded certificate PDF |
| FileName | string? | Filename of the certificate PDF |
| Error | string? | Error message if download failed or entity ineligible |

### CertificateSection (existing — `Common/Models/`)

| Field | Type | Description |
|-------|------|-------------|
| Available | bool | Whether the certificate is available |
| Downloads | List\<DownloadReference\>? | Certificate download references |
| Error | string? | Error message if certificate unavailable |

### UnifiedEntityDetailResponse (existing — `Common/Models/`)

| Field | Type | Description |
|-------|------|-------------|
| Details | DetailSection | Core entity information |
| RegisteredAgent | AgentSection? | Registered agent info |
| Certificate | CertificateSection? | **WY: Currently always null — BUG** |
| Parties | List\<PartyEntry\>? | Officers, directors |
| Documents | List\<DocumentEntry\>? | History documents |

---

## Flow Definition Changes (`us-wy-entity-details.json`)

### New Variable

| Variable | Source | Description |
|----------|--------|-------------|
| `certificateError` | extracted | Error message when entity is not eligible for certificate |

### Action Changes

| Action ID | Change | Description |
|-----------|--------|-------------|
| action-009b | Order 9 → 10 | Fix duplicate order; renumber subsequent actions |
| action-010 | Order 10 → 11 | Renumber |
| action-011 | Order 11 → 12 | Renumber |
| action-012 | Order 12 → 13 | Renumber |
| action-013 | Order 13 → 14 | Renumber |
| action-016½ (new) | Order 17 | Extract certificate eligibility after search click |
| action-017 (download) | Add condition | `element-exists` for `#MainContent_ContinueButton` |

### New Action: Certificate Eligibility Check (action-016½)

```json
{
  "id": "action-016b",
  "order": 17,
  "type": "extract",
  "group": "certificate",
  "description": "Check certificate eligibility — detect ineligibility alert",
  "javascript": "() => { const alert = document.body.innerText || ''; if (alert.includes('Certificates may only be printed when the entity Standing')) { return 'Entity is not in good standing. Certificates may only be printed when the entity Standing is Good.'; } return ''; }",
  "storeAs": "certificateError"
}
```

### Modified Action: Download with Condition (action-017)

```json
{
  "id": "action-017",
  "order": 18,
  "type": "download",
  "group": "certificate",
  "description": "Download WY certificate PDF from Print button",
  "linkSelector": "#MainContent_ContinueButton",
  "validateExtension": ".pdf",
  "filenamePattern": "${idNumber}-certificateofgoodstanding.pdf",
  "storeAs": "certificateLocalPath",
  "condition": {
    "type": "element-exists",
    "selector": "#MainContent_ContinueButton"
  },
  "screenshot": {
    "milestone": "wy-cert-03-after-download"
  }
}
```

---

## Mapper Changes (`EntityDetailMapper.cs`)

### `MapWyDetails` — Add Certificate Mapping

**Current**: Certificate section is not mapped (always `null`).

**Fix**: Read `certificateUrl` and `certificateError` from the composed
output and map to `CertificateSection`:

```csharp
var certificateUrl = output.GetStringOrDefault("certificateUrl");
var certificateError = output.GetStringOrDefault("certificateError");

Certificate = !string.IsNullOrEmpty(certificateUrl)
    ? new CertificateSection
    {
        Available = true,
        Downloads = [new DownloadReference
        {
            Label = "Certificate of Good Standing",
            ProxyUrl = certificateUrl,
            FileName = certificateUrl.Contains('/', StringComparison.Ordinal)
                ? certificateUrl[(certificateUrl.LastIndexOf('/') + 1)..]
                : "certificate.pdf",
        }]
    }
    : !string.IsNullOrEmpty(certificateError)
        ? new CertificateSection { Available = false, Error = certificateError }
        : null
```

### Output Compose Update (`us-wy-entity-details.json`)

Add `certificateError` to the compose block:

```json
"compose": {
  "details": "details",
  "registeredAgent": "registeredAgent",
  "parties": "parties",
  "historyDocuments": "historyDocuments",
  "certificateUrl": "certificateLocalPath",
  "certificateError": "certificateError"
}
```

---

## V1 Endpoint Removal

### Files to Delete

| File | Reason |
|------|--------|
| `Features/BusinessSearch/Endpoints/BusinessSearchEndpoints.cs` | Only v1 route registration |

### Files to Modify

| File | Change |
|------|--------|
| `Program.cs` | Remove `app.MapBusinessSearchEndpoints()` call |

### Files to Retain

| File/Folder | Reason |
|-------------|--------|
| `Features/BusinessSearch/Models/` | Used by v2 SearchResultMapper |
| `Features/BusinessSearch/Exceptions/` | Used by GlobalExceptionHandler, ExecuteScriptHandler, engine |
| `Features/BusinessSearch/Logging/` | Used by PlaywrightBrowserManager, AzureBlobStorageClient |
| `Features/BusinessSearch/Metrics/` | Used by search scrapers |
| `Features/EntityDetails/` (all) | Used by v2 JsonDrivenEntityDetailScraper |
