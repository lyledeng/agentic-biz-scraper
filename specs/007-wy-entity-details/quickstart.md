# Quickstart: Wyoming Entity Details Support

**Feature**: 007-wy-entity-details  
**Date**: 2026-03-25

## Prerequisites

- .NET 10 SDK installed
- Azure Playwright workspace access (token in Key Vault or user secrets)
- Azure SQL connection string (or Testcontainers for local dev)
- Azure Blob Storage connection string

## Build & Run

```powershell
# Build
dotnet build BizScraper.slnx

# Run
dotnet run --project src/BizScraper.Api

# Test
dotnet test BizScraper.slnx
```

## API Usage

### Retrieve WY Entity Details

```bash
curl -k "https://localhost:8443/api/v1/entity-details?state=WY&detailsUrl=https://wyobiz.wyo.gov/Business/FilingDetails.aspx?eFNum=040210024178155224029087218229046059197210140039"
```

### Retrieve CO Entity Details (updated — state now required)

```bash
curl -k "https://localhost:8443/api/v1/entity-details?state=CO&detailsUrl=https://www.sos.state.co.us/biz/BusinessEntityDetail.do?masterFileId=20181871974&entityId2=20181871974&fileId=20181871974&srchTyp=ENTITY"
```

## Key Files

| File | Purpose |
|------|---------|
| `src/BizScraper.Api/Infrastructure/Scraping/Definitions/wy-entity-details.json` | WY scraping flow definition |
| `src/BizScraper.Api/Features/EntityDetails/Endpoints/EntityDetailsEndpoints.cs` | Endpoint with state parameter |
| `src/BizScraper.Api/Features/EntityDetails/Handlers/GetEntityDetailsHandler.cs` | State-aware handler |
| `src/BizScraper.Api/Features/EntityDetails/Models/PartyResult.cs` | New party model |
| `src/BizScraper.Api/Features/EntityDetails/Models/HistoryDocumentResult.cs` | New history document model |
| `deploy/liquibase/changelog/ddl/2.0.2603.3-add-state-column-to-entity-detail-audit.sql` | DB migration |

## Breaking Changes

- The `state` query parameter is now **required** on `GET /api/v1/entity-details`
- Existing callers must add `&state=CO` to their requests
- Response JSON gains new nullable fields: `subStatus`, `standingTax`, `standingRA`, `standingOther`, `inactiveDate`, `termOfDuration`, `formedIn`, `latestAnnualReportYear`, `annualReportExempt`, `licenseTaxPaid`, `parties`, `historyDocuments`
- New fields are `null` for CO requests — no impact on existing parsers that ignore unknown fields
