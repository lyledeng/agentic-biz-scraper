# Quickstart: CO SOS Business Entity Search API

**Feature**: 001-cosos-business-search-api

## Prerequisites

- .NET 10 SDK
- Azure Playwright workspace access token (via Key Vault or User Secrets)
- Azure SQL Managed Instance connection string (for audit trail)

## Local Setup

### 1. Clone and switch to feature branch

```bash
git checkout 001-cosos-business-search-api
```

### 2. Restore dependencies

```bash
dotnet restore CososWebScraping.slnx
```

### 3. Configure User Secrets

```bash
cd src/CososWebScraping.Api
dotnet user-secrets init
dotnet user-secrets set "Playwright:AccessToken" "<your-azure-playwright-token>"
dotnet user-secrets set "ConnectionStrings:AuditTrail" "<your-azure-sql-connection-string>"
```

### 4. Run the API

```bash
dotnet run --project src/CososWebScraping.Api
```

The API starts on `https://localhost:8443`.

### 5. Test a search

```bash
curl -k "https://localhost:8443/api/v1/business-search?name=Acme+Construction+Colorado"
```

Expected response:

```json
{
  "results": [
    {
      "documentNumber": "",
      "name": "ACME CONSTRUCTION & PILE COMPANY, Colorado Authority Terminated August 1, 2002",
      "event": "Batch Event",
      "status": "Effective",
      "form": "TRDNM",
      "formationDate": "08/01/2002",
      "detailsUrl": "https://www.sos.state.co.us/biz/BusinessEntityDetail.do?..."
    }
  ],
  "totalCount": 2,
  "siteTotalCount": 2,
  "truncated": false,
  "pagesScraped": 1,
  "searchTerm": "Acme Construction Colorado"
}
```

## Error Responses

| Scenario | HTTP Status | Try |
|----------|-------------|-----|
| Empty search term | 400 | `curl -k "https://localhost:8443/api/v1/business-search?name="` |
| Too-broad search | 422 | `curl -k "https://localhost:8443/api/v1/business-search?name=a"` |
| Concurrent request while busy | 503 | Send two requests simultaneously |

All errors return RFC 9457 Problem Details (`application/problem+json`).

## Running Tests

```bash
# Unit tests
dotnet test tests/CososWebScraping.UnitTests

# Integration tests (requires Testcontainers / Docker)
dotnet test tests/CososWebScraping.IntegrationTests

# End-to-end tests (requires running API + Azure Playwright workspace)
dotnet test tests/CososWebScraping.EndToEndTests
```

## Configuration Reference

| Setting | Description | Default |
|---------|-------------|---------|
| `Playwright:BrowserEndpoint` | Azure Playwright WSS endpoint | (from appsettings) |
| `Playwright:AccessToken` | Bearer token for Playwright workspace | (from Key Vault / User Secrets) |
| `Playwright:RequestTimeoutSeconds` | Overall request timeout | 120 |
| `Playwright:MaxPages` | Maximum pages to scrape | 10 |
| `ConnectionStrings:AuditTrail` | Azure SQL MI connection string | (from Key Vault / User Secrets) |
