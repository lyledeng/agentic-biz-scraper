# BizScraper

Internal ASP.NET Core API for multi-state business entity search and detail scraping through Azure Playwright workspaces.

## Projects

- `src/BizScraper.Api`: Minimal API implementation
- `tests/BizScraper.UnitTests`: Unit tests
- `tests/BizScraper.IntegrationTests`: WebApplicationFactory and persistence tests
- `tests/BizScraper.EndToEndTests`: Environment-driven E2E tests
- `tests/k6`: Performance scenarios

## Run

```powershell
dotnet restore BizScraper.slnx
dotnet build BizScraper.slnx
dotnet run --project src/BizScraper.Api
```

## Test

```powershell
dotnet test BizScraper.slnx
```

## Liquibase

Build the Liquibase image from the repository root:

```powershell
docker build -f deploy/liquibase/liquibase_dockerfile -t cosos-liquibase .
```

The image now bakes in the default changelog file, so the default command is just `update`.
You only need to provide the SQLite database path at runtime:

```powershell
docker run --rm `
	-v ${PWD}/data:/data `
	cosos-liquibase `
	--url="jdbc:sqlite:/data/bizscraper.db"
```

To run a different Liquibase command, append it instead of using the default:

```powershell
docker run --rm `
	-v ${PWD}/data:/data `
	cosos-liquibase `
	--url="jdbc:sqlite:/data/bizscraper.db" `
	validate
```

## Configuration

Configure the following settings through `appsettings.json` and user secrets:

- `Playwright:BrowserEndpoint`
- `Playwright:AccessToken`
- `ConnectionStrings:AuditTrail`
- `AzureAd:Instance`
- `AzureAd:TenantId`
- `AzureAd:ClientId`
- `AzureAd:Audience`

## Authentication

The API and Test-UI are protected by Microsoft Entra ID. The API accepts both delegated (user) and application (client credentials) tokens. See [quickstart.md](specs/025-entra-id-auth/quickstart.md) for Entra ID app registration setup.

## Endpoints

- `GET /api/v1/business-search?name=...`
- `GET /healthz`
- `GET /ready`
