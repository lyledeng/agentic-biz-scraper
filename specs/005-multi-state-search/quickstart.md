# Quickstart: Multi-State Business Entity Search

**Feature**: `005-multi-state-search`

## Prerequisites

1. .NET 10 SDK installed
2. Azure Playwright workspace credentials configured (see appsettings / user secrets)
3. FAB CAPTCHA solver agent configured on FAB dashboard (see [fab-captcha-agent-config.md](fab-captcha-agent-config.md))
4. FAB agent endpoint URL and auth token in appsettings or Key Vault

## Configuration

Add the following to `appsettings.json` (or user secrets for local dev):

```json
{
  "Playwright": {
    "BrowserEndpoint": "wss://eastus.api.playwright.microsoft.com/...",
    "AccessToken": "<playwright-token>",
    "SearchEntryUrl": "https://www.sos.state.co.us/biz/BusinessEntityCriteriaExt.do",
    "WyomingSearchUrl": "https://wyobiz.wyo.gov/Business/FilingSearch.aspx"
  },
  "FabAgent": {
    "CaptchaSolver": {
      "EndpointUrl": "https://<fab-host>/api/agents/<agent-id>/process",
      "TimeoutSeconds": 30
    }
  }
}
```

Set secret via user secrets:
```powershell
dotnet user-secrets set "FabAgent:CaptchaSolver:AuthToken" "<your-fab-token>"
```

## Run the API

```powershell
dotnet run --project src/CososWebScraping.Api
```

## Search Examples

### Colorado Search

```
GET https://localhost:8443/api/v1/business-search?state=CO&name=Wendy
```

### Wyoming Search

```
GET https://localhost:8443/api/v1/business-search?state=WY&name=Wendy
```

### Expected Response Shape (both states)

```json
{
  "results": [
    {
      "name": "...",
      "identifier": "...",
      "status": "...",
      "entityType": "...",
      "formationDate": "...",
      "detailsUrl": "...",
      "state": "WY",
      "event": null,
      "standingTax": "Good",
      "standingRA": "Good"
    }
  ],
  "totalCount": 37,
  "siteTotalCount": 37,
  "truncated": false,
  "pagesScraped": 2,
  "searchTerm": "Wendy",
  "state": "WY"
}
```

## Run Tests

```powershell
# Unit tests
dotnet test tests/CososWebScraping.UnitTests --filter "FullyQualifiedName~BusinessSearch"

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/CososWebScraping.IntegrationTests --filter "FullyQualifiedName~BusinessSearch"

# E2E tests (requires running API + Playwright workspace)
dotnet test tests/CososWebScraping.EndToEndTests --filter "FullyQualifiedName~Wyoming"
```

## Verify Known Entities

| State | Search Term | Expected Result | Entity |
|-------|-------------|-----------------|--------|
| CO | Wendy | Contains "WENDY'S INTERNATIONAL" | Document# 19871296702 |
| WY | Wendy | Contains "WEND&YLZ LLC" | Filing# 2018-000797412 |
| WY | Wendy | 37 total results, 2 pages | Pagination test |

## Swagger UI

Navigate to `https://localhost:8443/swagger` to explore the API interactively.

## Database Schema

Apply the new Liquibase changelog to add the `State` column:

```powershell
docker build -f deploy/liquibase/liquibase_dockerfile -t cososwebscraping-liquibase .
docker run --rm \
  -e LIQUIBASE_COMMAND_URL="jdbc:sqlserver://localhost:1433;databaseName=CososWebScraping;encrypt=true;trustServerCertificate=true" \
  -e LIQUIBASE_COMMAND_USERNAME="sa" \
  -e LIQUIBASE_COMMAND_PASSWORD="yourpassword" \
  cososwebscraping-liquibase
```
