# Quickstart: Cloud Screenshot Storage & Per-Request Folder Organization

**Feature**: `003-cloud-screenshot-storage`
**Branch**: `003-cloud-screenshot-storage`

## Prerequisites

- .NET 10 SDK
- Azure Storage Account (or Azurite for local development)
- Azure SQL MI connection string (for audit trail)
- Existing diagnostics infrastructure from feature `002-scraping-diagnostics`

## New NuGet Package

Add `Azure.Storage.Blobs` to `Directory.Packages.props`:

```xml
<PackageVersion Include="Azure.Storage.Blobs" Version="12.24.0" />
```

And reference it in `CososWebScraping.Api.csproj`:

```xml
<PackageReference Include="Azure.Storage.Blobs" />
```

## Configuration

### appsettings.json

Add the `CloudStorage` section under `Playwright:Diagnostics`:

```json
{
  "Playwright": {
    "Diagnostics": {
      "CloudStorage": {
        "Enabled": false,
        "ConnectionString": "",
        "ContainerName": "diagnostics"
      }
    }
  }
}
```

### Local Development (User Secrets)

```bash
dotnet user-secrets set "Playwright:Diagnostics:CloudStorage:Enabled" "true"
dotnet user-secrets set "Playwright:Diagnostics:CloudStorage:ConnectionString" "UseDevelopmentStorage=true"
dotnet user-secrets set "Playwright:Diagnostics:CloudStorage:ContainerName" "diagnostics-dev"
```

Or for a real Azure Storage Account:

```bash
dotnet user-secrets set "Playwright:Diagnostics:CloudStorage:ConnectionString" "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
```

### Deployed Environments (Key Vault)

Store the connection string as a Key Vault secret. Reference it via the standard ASP.NET Core Key Vault configuration provider. Secret name convention: `Playwright--Diagnostics--CloudStorage--ConnectionString`.

## Database Migration

Run Liquibase to apply the new migration:

```bash
liquibase update --changelog-file=deploy/liquibase/masterfiles/master-1.0.2603.0.yaml
```

This adds the `DiagnosticsUrl` column to `dbo.SearchAuditEntries`.

## Local Development with Azurite

For local testing without a real Azure Storage Account:

```bash
# Install Azurite (if not already installed)
npm install -g azurite

# Start Azurite blob service
azurite-blob --location ./azurite-data --blobPort 10000
```

Connection string for Azurite: `UseDevelopmentStorage=true`

## Verification

1. **Local folder structure**: After a search request, check `diagnostics-dev/{correlationId}/` for screenshots and traces organized in per-request subfolders.

2. **Cloud upload**: With cloud storage enabled, use Azure Storage Explorer or `az storage blob list` to verify blobs appear under `{containerName}/{correlationId}/`.

3. **Audit trail**: Query the audit trail for the correlation ID and verify `DiagnosticsUrl` is populated:
   ```sql
   SELECT CorrelationId, DiagnosticsUrl FROM dbo.SearchAuditEntries ORDER BY CreatedAt DESC;
   ```

## Build & Test

```bash
dotnet build
dotnet test
```
