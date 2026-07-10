# Quickstart: WY Certificate of Good Standing

**Feature**: 009-wy-certificate-good-standing
**Date**: 2026-03-26

## What This Feature Does

When requesting WY entity details, the system now also retrieves the Certificate of Good Standing from the Wyoming Secretary of State website. The certificate PDF is downloaded, uploaded to cloud storage, and its URL is returned in the `certificate` field of the API response.

## How It Works

1. The existing `GET /api/v1/entity-details?detailsUrl={url}&state=WY` endpoint is called
2. Entity details are scraped as before (CAPTCHA, details, parties, history documents)
3. **NEW**: After extracting details, the system:
   - Validates the Filing ID format (`YYYY-NNNNNNNNN`)
   - Navigates to `https://wyobiz.wyo.gov/Business/ViewCertificate.aspx`
   - Enters the Filing ID and clicks Search
   - Clicks Print to download the PDF
   - Saves the PDF locally and uploads to blob storage
4. The response includes `certificate.available: true`, `certificate.storageUrl`, and `certificate.fileName`

## Example Response (WY Entity in Good Standing)

```json
{
  "details": {
    "name": "Wendy's Technology, LLC",
    "status": "Active",
    "idNumber": "2017-000757675",
    ...
  },
  "registeredAgent": { ... },
  "certificate": {
    "available": true,
    "storageUrl": "https://aksd1ilsgsa.blob.core.windows.net/playwright-diagnostics/{correlationId}/2017-000757675-certificateofgoodstanding.pdf",
    "fileName": "2017-000757675-certificateofgoodstanding.pdf",
    "error": null
  },
  "parties": [ ... ],
  "historyDocuments": [ ... ]
}
```

## Example Response (Entity Not Eligible)

```json
{
  "details": { ... },
  "registeredAgent": { ... },
  "certificate": null,
  "parties": [ ... ],
  "historyDocuments": [ ... ]
}
```

## Files Changed

| File | Change |
|------|--------|
| `src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json` | Add certificate retrieval actions (navigate, fill, click, download) |
| `src/BizScraper.Api/Infrastructure/Scraping/Engine/JsonDrivenEntityDetailScraper.cs` | Add Filing ID validation + independent 15s timeout for certificate flow |

## Files NOT Changed

- `CertificateResult.cs` — reused as-is
- `EntityDetailScrapeResult.cs` — reused as-is
- `GetEntityDetailsHandler.cs` — existing certificate upload logic handles WY automatically
- `DownloadActionHandler.cs` — works with button selectors as-is
- Audit trail schema — `CertificateUrl` column already exists
- OpenAPI contract — `certificate` field already defined

## Testing

```bash
# Call for a WY entity in good standing
curl "http://localhost:5225/api/v1/entity-details?detailsUrl=https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=094114166207167038170018020254100209134082027099&state=WY"

# Check certificate field in response
# Expected: certificate.available = true, certificate.storageUrl populated
```

## Configuration

No new configuration required. Uses existing:
- `Playwright:Diagnostics:CloudStorage:Enabled` — must be `true` for cloud upload
- `Playwright:Diagnostics:CloudStorage:ConnectionString` — Azure Blob Storage connection
- `Playwright:Diagnostics:CloudStorage:ContainerName` — target container
