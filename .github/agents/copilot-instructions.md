# bizscraper Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-13

## Active Technologies
- C# 14 / .NET 10 (`net10.0`, `LangVersion=latest`) + Microsoft.Playwright 1.58.0, Polly 8.6.6, Flurl.Http 4.0.2, LiteBus 4.3.0, Microsoft.Data.SqlClient 7.0.0, OpenTelemetry, Swashbuckle — **new**: Azure.Storage.Blobs (latest stable) (003-cloud-screenshot-storage)
- Azure SQL Managed Instance (audit trail via raw ADO.NET), local filesystem (diagnostics), Azure Blob Storage (cloud diagnostics — new) (003-cloud-screenshot-storage)
- C# 14 / .NET 10 (net10.0, LangVersion latest) + Microsoft.Playwright 1.58.0, LiteBus 4.3.0 (CQRS), Azure.Storage.Blobs 12.24.0, Polly 8.6.6, Microsoft.Data.SqlClient 7.0.0, Swashbuckle 7.3.1 (004-entity-details-endpoint)
- Azure SQL Managed Instance (audit trail), Azure Blob Storage (diagnostics + certificates) (004-entity-details-endpoint)
- .NET 10, C# 14, `net10.0`, `LangVersion=latest` + LiteBus 4.3.0 (CQRS), Microsoft.Playwright 1.58.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0, Swashbuckle.AspNetCore 7.3.1 (005-multi-state-search)
- Azure SQL Managed Instance (via Microsoft.Data.SqlClient 7.0.0), Liquibase for DDL (005-multi-state-search)
- C# 14 / .NET 10 + Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0 (006-json-scraping-engine)
- SQL Server (audit trail via ADO.NET), Azure Blob Storage (diagnostics artifacts), local filesystem (JSON flow definitions bundled as content files) (006-json-scraping-engine)
- C# 14 / .NET 10 (LTS), `net10.0` + Playwright (Azure Playwright Workspaces), Flurl.Http, Polly, System.Text.Json (source-generated), LiteBus (007-wy-entity-details)
- Azure SQL Managed Instance (audit trail), Azure Blob Storage (diagnostics, documents) (007-wy-entity-details)
- C# 14 / .NET 10 (LTS), `net10.0` + Playwright (Azure Playwright Workspaces), Azure.Storage.Blobs, Polly, System.Text.Json (source-generated), LiteBus (008-wy-history-doc-storage)
- Azure SQL Managed Instance (audit trail), Azure Blob Storage (documents + diagnostics) (008-wy-history-doc-storage)
- .NET 10 (LTS) with C# 14 + Playwright (Azure Playwright Workspaces), System.Text.Json, Polly, Litebus, Azure.Storage.Blobs (009-wy-certificate-good-standing)
- Azure Blob Storage (certificate PDFs), Azure SQL MI (audit trail) (009-wy-certificate-good-standing)
- [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION] + [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION] (009-wy-certificate-good-standing)
- [if applicable, e.g., PostgreSQL, CoreData, files or N/A] (009-wy-certificate-good-standing)
- TypeScript 5 / Angular 17 (standalone components, signals API) + @wk/components-v3-angular17 (Jumpstart/Bamboo WK design system), Angular Router, Angular HttpClient + RxJS switchMap, Karma/Jasmine unit tests (010-test-ui)
- nginx:alpine (runtime container), node:22-alpine (build stage); no persistent storage — stateless SPA; API base URL in localStorage (010-test-ui)
- .NET 10 / C# 14 (`net10.0`, language version `latest`) + Azure.Storage.Blobs, Polly (resilience), LiteBus (CQRS), OpenTelemetry, System.Text.Json (source-generated) (011-pdf-proxy-streaming)
- Azure Blob Storage (container `diagnostics`) for PDF files; IBlobStorageClient / AzureBlobStorageClient (011-pdf-proxy-streaming)
- C# 14 / .NET 10, target framework `net10.0` + Polly (retry pipeline), Playwright (scraping), LiteBus (CQRS) (012-co-record-count-error)
- Azure SQL Managed Instance (audit trail — existing, no schema changes) (012-co-record-count-error)
- C# 14 / .NET 10 + Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Azure.Storage.Blobs 12.24.0, System.Text.Json (source-generated) (013-germany-business-search)
- Azure SQL Managed Instance (audit trail via ADO.NET), Azure Blob Storage (screenshots/diagnostics), local filesystem (JSON flow definitions bundled as content files) (013-germany-business-search)
- Azure SQL MI (audit trail), Azure Blob Storage (diagnostics), local filesystem (JSON flow definitions as content files) (014-recorder-friendly-schema)
- C# 14 / .NET 10 + Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0, Markdig (MIT, to be added), System.Text.Json (source-generated) (015-de-hardcopy-printout)
- Azure SQL Managed Instance (audit trail via ADO.NET), Azure Blob Storage (PDFs + diagnostics), local filesystem (JSON flow definitions) (015-de-hardcopy-printout)
- C# 14 / .NET 10 + Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0, Swashbuckle.AspNetCore (016-generic-script-execution)
- C# 14 / .NET 10 (LTS), Angular 17 (test UI) + System.Text.Json (source-gen), Litebus (CQRS), Playwright, Flurl.Http, Polly, OpenTelemetry (017-constitution-compliance-audit)
- Azure SQL Managed Instance (audit trail), Azure Blob Storage (screenshots/documents) (017-constitution-compliance-audit)
- .NET 10 (LTS) with C# 14, `net10.0` target framework, language version `latest` + ASP.NET Core Minimal API, LiteBus (CQRS), Playwright (browser automation), Flurl.Http (outbound HTTP), Polly (resilience), System.Text.Json (STJ source generation) (018-unified-entity-schema)
- Azure Blob Storage (documents), Azure SQL Managed Instance (audit trail), Liquibase (schema migrations) (018-unified-entity-schema)
- Azure DevOps YAML Pipelines; .NET 10 (C# 14); Node.js 22; Angular 17 + Azure DevOps, Docker, Helm 3, Liquibase 4.33, Testcontainers (Azure SQL Edge) (019-ci-pipeline-refinement)
- Azure SQL Managed Instance (validated via Testcontainers SQL Edge in CI) (019-ci-pipeline-refinement)
- .NET 10 / C# 14 + Playwright, Flurl.Http, Polly, LiteBus, System.Text.Json (020-wy-cert-good-standing-fix)
- Azure Blob Storage (certificate PDFs), SQLite (audit trail) (020-wy-cert-good-standing-fix)
- .NET 10 / C# 14 + Microsoft.Playwright 1.58, LiteBus (CQRS), OpenTelemetry (021-playwright-local-fallback)
- SQLite (audit trail), Azure Blob Storage (diagnostics) (021-playwright-local-fallback)
- C# 14 / .NET 10 (`net10.0`) for API, TypeScript with Angular for test-ui + ASP.NET minimal APIs, Playwright-backed execution engine, Angular HttpClient, RxJS (023-test-ui-spec-alignment)
- Existing audit trail storage and blob diagnostics (no new persistence model introduced by this feature) (023-test-ui-spec-alignment)
- .NET 10 with C# 14 (`net10.0`); Angular 17 (test UI) + Playwright, System.Text.Json, OpenTelemetry, Litebus, Flurl.Http (024-ia-search-testui-fix)
- Azure Blob Storage (diagnostics/screenshots), Azure SQL Managed Instance (audit) (024-ia-search-testui-fix)
- .NET 10 / C# 14 (API), Angular 17 / TypeScript 5.4 (UI) + Microsoft.Identity.Web (API), @azure/msal-browser + @azure/msal-angular (UI) (025-entra-id-auth)
- SQLite via Microsoft.Data.Sqlite (audit trail — add CallerIdentity column) (025-entra-id-auth)
- C# 14 / .NET 10 (LTS), `net10.0` + Playwright (Azure Playwright Workspaces), `[LoggerMessage]` source generation (Microsoft.Extensions.Logging) (026-action-logging)
- N/A (logging only — no storage changes) (026-action-logging)
- .NET 10 / C# 14 (API), Angular 17 / TypeScript 5.4 (UI) + System.Text.Json source generation (API), @wk/components-v3-angular17 Jumpstart PDS3 (UI) (027-no-records-found)
- N/A — no schema changes (027-no-records-found)
- TypeScript 5.x / Angular 17 (standalone components, signals) + `@angular/common/http` (HttpClient), `@azure/msal-angular` (MsalInterceptor), `@wk/components-v3-angular17` (Jumpstart PDS3) (028-doc-proxy-auth)
- N/A (no backend data changes) (028-doc-proxy-auth)
- .NET 10 with C# 14 + Playwright (browser context APIRequest), Azure.Storage.Blobs, System.Text.Json (029-wy-history-doc-fix)
- Azure Blob Storage (existing container), document proxy endpoint (`/api/v1/documents/{blobPath}`) (029-wy-history-doc-fix)
- C# 14 / .NET 10 (LTS), `net10.0` + Microsoft.Playwright (v1.58.0), System.Text.Json (source-generated) (030-per-definition-browser-mode)
- N/A (no database changes for this feature) (030-per-definition-browser-mode)
- .NET 10 (LTS) with C# 14, `net10.0` target + Playwright (browser automation), Flurl.Http (outbound HTTP), Polly (resilience), LiteBus (CQRS), Azure.Storage.Blobs, System.Text.Json (source-generated) (031-mo-entity-search)
- Azure Blob Storage (filing PDFs), Azure SQL MI (audit trail) (031-mo-entity-search)
- .NET 10 / C# 14 + Flurl.Http (HTTP client), Polly (resilience), System.Text.Json (serialization), Microsoft.Identity.Web (Entra ID auth) (033-windows-headed-proxy)
- SQLite (audit trail) — local to each instance; Azure Blob Storage (shared between instances) (033-windows-headed-proxy)
- C# 14 / .NET 10.0 (LTS) + Microsoft.Playwright 1.58.0, Microsoft.Identity.Web 3.14.1, Flurl.Http 4.0.2, Polly 8.6.6, Azure.Storage.Blobs 12.24.0, LiteBus 4.3.0, OpenTelemetry 1.15.0+ (033-windows-headed-proxy)
- Azure Blob Storage (screenshots, documents); SQLite (audit trail, per-instance) (033-windows-headed-proxy)
- .NET 10, C# 14 + Playwright (browser automation), Azure Blob Storage SDK, System.Text.Json source generation (034-co-entity-doc-download)
- Azure Blob Storage (document PDFs), Azure SQL MI (audit trail) (034-co-entity-doc-download)
- C# 14 / .NET 10 + Microsoft.Playwright (Chromium.ConnectAsync, Playwright.CreateAsync) (037-no-remote-browser-cache)
- N/A (no new persistence; existing audit trail unchanged) (037-no-remote-browser-cache)

- C# 14 / .NET 10 (LTS), target framework `net10.0`, language version `latest` + Microsoft.Playwright (browser automation via Azure Playwright workspaces), Litebus (CQRS), Polly (resilience), Flurl.Http (outbound HTTP), System.Text.Json (source-generated serialisation), OpenTelemetry (observability) (001-cosos-business-search-api)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

# Add commands for C# 14 / .NET 10 (LTS), target framework `net10.0`, language version `latest`

## Code Style

C# 14 / .NET 10 (LTS), target framework `net10.0`, language version `latest`: Follow standard conventions

## Recent Changes
- 038-engine-site-code-extraction: Added .NET 10, C# 14 + Playwright (browser automation), Azure Blob Storage SDK, System.Text.Json source generation
- 037-no-remote-browser-cache: Added C# 14 / .NET 10 + Microsoft.Playwright (Chromium.ConnectAsync, Playwright.CreateAsync)
- 034-co-entity-doc-download: Added .NET 10, C# 14 + Playwright (browser automation), Azure Blob Storage SDK, System.Text.Json source generation


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
