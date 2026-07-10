# Quickstart: German Hardcopy Printout Retrieval with FAB Translation

**Feature**: 015-de-hardcopy-printout
**Date**: 2026-03-27

## Prerequisites

- .NET 10 SDK installed
- Azure Playwright workspace access (token in Key Vault or user secrets)
- Azure SQL connection string (or Testcontainers for local dev)
- Azure Blob Storage connection string
- FAB Document Translator agent configured on FAB dashboard (see `contracts/fab-document-translator-config.md`)
- FAB Document Translator endpoint URL and auth token in user secrets or appsettings

## Configuration

Add the FAB Document Translator section to `appsettings.Development.json` or user secrets:

```json
{
  "FabDocumentTranslator": {
    "EndpointUrl": "https://<fab-host>/api/agents/<agent-id>/invoke",
    "AuthToken": "your-api-key-here",
    "TimeoutSeconds": 60
  }
}
```

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

### Retrieve a Hardcopy Printout

```bash
curl -k -X POST "https://localhost:8443/api/v1/germany-search/current-hardcopy-printout" \
  -H "Content-Type: application/json" \
  -d '{"searchTerm":"Claes und Rohde","registrationId":"Paderborn HRA 3059"}' \
  --output hardcopy.pdf
```

**Response**: Binary PDF (English translation) with headers:
- `X-Document-Url`: Blob storage URL of translated PDF
- `X-Original-Document-Url`: Blob storage URL of original German PDF
- `X-Correlation-Id`: Correlation ID for audit traceability

### Error Cases

```bash
# Missing registration → 404
curl -k -X POST "https://localhost:8443/api/v1/germany-search/current-hardcopy-printout" \
  -H "Content-Type: application/json" \
  -d '{"searchTerm":"NonExistent","registrationId":"Invalid HRA 0000"}'

# Missing fields → 400
curl -k -X POST "https://localhost:8443/api/v1/germany-search/current-hardcopy-printout" \
  -H "Content-Type: application/json" \
  -d '{"searchTerm":""}'
```

## Test UI Usage

1. Navigate to the test-ui (`http://localhost:4200`)
2. Go to the Germany Search section
3. Search for a company (e.g., "Claes und Rohde")
4. Click the **AD** button on a result row
5. The English-translated PDF opens in a new browser tab

## Key Files

| File | Purpose |
|------|---------|
| `src/BizScraper.Api/Features/GermanySearch/Endpoints/GermanySearchEndpoints.cs` | POST endpoint registration |
| `src/BizScraper.Api/Features/GermanySearch/Handlers/GetHardcopyPrintoutHandler.cs` | Orchestrates scrape→translate→PDF→store |
| `src/BizScraper.Api/Features/GermanySearch/Commands/GetHardcopyPrintoutCommand.cs` | LiteBus command |
| `src/BizScraper.Api/Features/GermanySearch/Models/HardcopyPrintoutRequest.cs` | API request body |
| `src/BizScraper.Api/Common/Configuration/FabDocumentTranslatorOptions.cs` | FAB translator config |
| `src/BizScraper.Api/Common/Interfaces/IDocumentTranslator.cs` | Translation abstraction |
| `src/BizScraper.Api/Infrastructure/DocumentTranslation/FabDocumentTranslator.cs` | FAB HTTP integration |
| `src/BizScraper.Api/Infrastructure/Pdf/MarkdownToPdfConverter.cs` | Markdig → HTML → Playwright PDF |

## New Dependencies

| Package | Version | License | Purpose |
|---------|---------|---------|---------|
| Markdig | 1.1.2 | BSD-2-Clause | Markdown → HTML conversion |

## Breaking Changes

None. This feature adds a new endpoint (`POST /api/v1/germany-search/current-hardcopy-printout`) without modifying existing endpoints or response shapes.
