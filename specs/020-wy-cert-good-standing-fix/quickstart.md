# Quickstart: WY Certificate of Good Standing Fix

**Feature**: 020-wy-cert-good-standing-fix  
**Date**: 2026-03-31

---

## What Changed

1. **WY certificate flow fixed** in `us-wy-entity-details.json`:
   - Added `element-exists` condition on the download action
   - Added ineligibility alert detection with error reporting
   - Fixed duplicate action ordering (action-009/009b)
   - Added `certificateError` variable to output compose

2. **WY certificate mapping added** to `EntityDetailMapper.MapWyDetails()`:
   - Certificate section now populated in v2 response (was always `null`)
   - Reports `certificate.available = true` with download URL, or
   - Reports `certificate.available = false` with error message

3. **V1 business-search endpoint removed**:
   - `GET /api/v1/business-search` route removed
   - All integration tests migrated to `POST /api/v2/execute-script`
   - k6 tests migrated to v2

## Testing

### WY Entity with Certificate (Good Standing)

```bash
curl -k -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{
    "definition": "us-wy-entity-details",
    "parameters": {
      "uniqueKey": "<base64url key for filing 2017-000757675>"
    }
  }'
```

Expected: `data.certificate.available = true` with a download URL.

### WY Entity without Certificate (Not Good Standing)

```bash
curl -k -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{
    "definition": "us-wy-entity-details",
    "parameters": {
      "uniqueKey": "<base64url key for filing 2015-000679087>"
    }
  }'
```

Expected: `data.certificate.available = false` with error message.

### V2 Business Search (replaces v1)

```bash
curl -k -X POST https://localhost:8443/api/v2/execute-script \
  -H "Content-Type: application/json" \
  -d '{
    "definition": "us-co-business-search",
    "parameters": { "searchTerm": "Wendy" }
  }'
```

### Verify V1 Removed

```bash
curl -k https://localhost:8443/api/v1/business-search?state=CO&name=Test
# Expected: 404 Not Found
```

## Running Tests

```bash
dotnet test tests/BizScraper.UnitTests/
dotnet test tests/BizScraper.IntegrationTests/
```
