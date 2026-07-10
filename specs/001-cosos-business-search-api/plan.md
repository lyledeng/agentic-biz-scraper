# Implementation Plan: CO SOS Business Entity Search API

**Branch**: `001-cosos-business-search-api` | **Date**: 2026-03-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-cosos-business-search-api/spec.md`

## Summary

Build a REST/HTTP API that accepts a business name search term, uses Playwright browser automation (via Azure Playwright workspaces) to navigate the Colorado Secretary of State Business Database Search site, scrapes the results table across up to 10 pages, and returns a JSON array of BusinessEntityResult records. Sequential request processing; fail-fast on errors; RFC 9457 Problem Details for all error responses.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS), target framework `net10.0`, language version `latest`  
**Primary Dependencies**: Microsoft.Playwright (browser automation via Azure Playwright workspaces), Litebus (CQRS), Polly (resilience), Flurl.Http (outbound HTTP), System.Text.Json (source-generated serialisation), OpenTelemetry (observability)  
**Storage**: Azure SQL Managed Instance (audit trail); Liquibase for schema management  
**Testing**: xUnit + NSubstitute (unit/integration), Testcontainers with Azure SQL Edge (integration), Playwright (E2E), k6 (performance)  
**Target Platform**: Azure AKS (Kubernetes), Debian/Ubuntu-based container (Playwright browser binaries requirement)  
**Project Type**: web-service (ASP.NET Core Minimal API)  
**Performance Goals**: <30s for single-page results (SC-001); sequential processing — one request at a time (FR-012)  
**Constraints**: 10-page pagination cap (FR-014); `503 Retry-After` when busy; overall request timeout at middleware level  
**Scale/Scope**: Single-pod sequential service; HPA for availability; internal trusted callers (no auth for v1)

**Azure Playwright Workspace (Dev)**:
- API Base URL: `https://eastus.api.playwright.microsoft.com/playwrightworkspaces/4657851e-eb0c-4f66-b834-b94117667ee9`
- Browser Endpoint: `wss://eastus.api.playwright.microsoft.com/playwrightworkspaces/4657851e-eb0c-4f66-b834-b94117667ee9/browsers`
- Access Token: Azure Key Vault `zuscu-d003-kvt-d1-ils01`, secret `azure-playwright-workspace-token`

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Requirement | Status | Notes |
|---|-----------|-------------|--------|-------|
| I | Test-First (NON-NEGOTIABLE) | TDD Red-Green-Refactor; full test pyramid | **PASS** | xUnit + NSubstitute (unit), Testcontainers (integration), Playwright (E2E), k6 (perf). Tasks will enforce test-before-implementation ordering. |
| II | SOLID Design & Engineering | SRP, OCP, DI, DRY, YAGNI, KISS | **PASS** | Vertical slice architecture with CQRS via Litebus. Scraper behind `IBusinessEntityScraper` abstraction. Sequential processing is simplest model (KISS/YAGNI). |
| III | Architectural Quality Attributes | Reliability, resilience, security, observability, auditability | **PASS** | Polly for resilience; OpenTelemetry for observability; RFC 9457 error responses; audit trail in Azure SQL MI; input validation at API boundary. |
| IV | Hybrid SemVer+CalVer Versioning | MAJOR.MINOR.YYMM.PATCH | **PASS** | Initial version `1.0.2603.0`. Liquibase changelogs follow same scheme. |
| V | Hands-Free Deployment & Rollback | Fully automated CI/CD; IaC; secrets from vault | **PASS** | Azure DevOps CI/CD pipelines; Helm charts; Azure Key Vault for secrets (Playwright token, DB connection). |

| # | Design Standard | Status | Notes |
|---|----------------|--------|-------|
| DS-1 | Error Handling — typed results, structured context | **PASS** | `IExceptionHandler` → RFC 9457 Problem Details. Custom error types per failure mode. |
| DS-2 | Configuration — 12-factor, fail-fast | **PASS** | `appsettings.json` + Key Vault + User Secrets. Fail-fast on missing Playwright config. |
| DS-3 | Dependencies — pinned, lock files | **PASS** | Central Package Management via `Directory.Packages.props`. |
| DS-4 | API Design — versioned, documented, contract-validated | **PASS** | OpenAPI contract defined in Phase 1. |

| # | Quality Gate | Status | Notes |
|---|-------------|--------|-------|
| QG-1 | Test Gate | **PASS** | Full pyramid required before merge. |
| QG-2 | Static Analysis Gate | **PASS** | `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `.editorconfig` severities. |
| QG-3 | Security Gate | **PASS** | SonarQube SAST + Black Duck SBOM. OWASP Top 10 mitigated. |
| QG-4 | Review Gate | **PASS** | PR-based workflow with peer review. |
| QG-5 | Build Gate | **PASS** | Multi-stage Docker build producing versioned image. |

**GATE RESULT: ALL PASS** — Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/001-cosos-business-search-api/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (OpenAPI)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
CososWebScraping.slnx
Directory.Build.props
Directory.Packages.props
.editorconfig

src/
├── cososwebscraping-dockerfile
│
├── CososWebScraping.Api/
│   ├── CososWebScraping.Api.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   │
│   ├── Middleware/
│   │   ├── GlobalExceptionHandler.cs
│   │   ├── CorrelationIdMiddleware.cs
│   │   ├── SecurityHeadersMiddleware.cs
│   │   └── RequestTimeoutMiddleware.cs
│   │
│   ├── Features/
│   │   ├── BusinessSearch/
│   │   │   ├── Queries/
│   │   │   │   └── SearchBusinessEntitiesQuery.cs
│   │   │   ├── Handlers/
│   │   │   │   └── SearchBusinessEntitiesHandler.cs
│   │   │   ├── Models/
│   │   │   │   ├── BusinessEntityResult.cs
│   │   │   │   ├── SearchResponse.cs
│   │   │   │   └── SearchRequest.cs
│   │   │   ├── Endpoints/
│   │   │   │   └── BusinessSearchEndpoints.cs
│   │   │   ├── Exceptions/
│   │   │   │   ├── ExceededRecordCountException.cs
│   │   │   │   ├── UpstreamException.cs
│   │   │   │   ├── ScrapingException.cs
│   │   │   │   └── ServiceBusyException.cs
│   │   │   ├── Metrics/
│   │   │   │   └── BusinessSearchMetrics.cs
│   │   │   └── Logging/
│   │   │       └── BusinessSearchLogMessages.cs
│   │   └── HealthCheck/
│   │       └── HealthCheckEndpoints.cs
│   │
│   ├── Common/
│   │   ├── Interfaces/
│   │   │   └── IBusinessEntityScraper.cs
│   │   ├── Configuration/
│   │   │   └── PlaywrightOptions.cs
│   │   └── Serialization/
│   │       └── AppJsonSerializerContext.cs
│   │
│   ├── Infrastructure/
│   │   ├── Scraping/
│   │   │   └── PlaywrightBusinessEntityScraper.cs
│   │   └── Persistence/
│   │       └── AuditTrailRepository.cs
│   │
│   └── Domain/
│       └── Entities/
│           └── SearchAuditEntry.cs

tests/
├── CososWebScraping.UnitTests/
│   ├── CososWebScraping.UnitTests.csproj
│   ├── Features/
│   │   └── BusinessSearch/
│   └── Infrastructure/
│
├── CososWebScraping.IntegrationTests/
│   ├── CososWebScraping.IntegrationTests.csproj
│   ├── Features/
│   │   └── BusinessSearch/
│   └── Infrastructure/
│
├── CososWebScraping.EndToEndTests/
│   ├── CososWebScraping.EndToEndTests.csproj
│   ├── Fixtures/
│   └── Scenarios/
│       └── BusinessSearch/
│
└── k6/

deploy/
├── pipelines/
│   ├── ci.yaml
│   ├── cd.yaml
│   └── sbom.yaml
│
├── helm/
│   └── cososwebscraping/
│       ├── Chart.yaml
│       ├── values.yaml
│       └── templates/
│
└── liquibase/
    ├── masterfiles/
    │   └── master-1.0.2603.0.yaml
    ├── changelog/
    │   ├── dcl/
    │   ├── ddl/
    │   │   └── 1.0.2603.0-create-search-audit-table.sql
    │   └── dml/
    └── liquibase_dockerfile

docs/
├── architecture_decision_records/
├── runbooks/
│   ├── resource-sizing-playbook.md
│   └── local-otel-collector-setup.md
└── playbooks/
```

**Structure Decision**: Vertical slice architecture based on the user's provided folder structure pattern (HtmlPdfConvertor). Single API project with `Features/BusinessSearch/` slice. Project renamed from `HtmlPdfConvertor` to `CososWebScraping` to match this repository. Playwright used for scraping (not PDF conversion). Audit trail persisted to Azure SQL MI.

## Deferred Items

- **Rate Limiting**: ASP.NET Core rate limiting middleware is specified in the platform architecture but is explicitly deferred to v2 per spec assumption: "Rate-limiting or throttling of requests to the CO SOS site is not required for v1 but may be added later." Sequential processing via `SemaphoreSlim(1,1)` provides implicit single-request gating for v1.

## Complexity Tracking

> No constitution violations to justify. All gates pass.
