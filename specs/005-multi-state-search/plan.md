# Implementation Plan: Multi-State Business Entity Search

**Branch**: `005-multi-state-search` | **Date**: 2026-03-25 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-multi-state-search/spec.md`

## Summary

Enhance the API to support business entity search across both Colorado and Wyoming by adding a mandatory `state` query parameter to the existing `/api/v1/business-search` endpoint, implementing a Wyoming Secretary of State scraper with FAB AI CAPTCHA solving, defining a normalized response schema with common + state-specific fields, restructuring the scraper layer around a semantic `IStateSearchScraper` interface, and adding per-state concurrency enforcement via keyed semaphores.

## Technical Context

**Language/Version**: .NET 10, C# 14, `net10.0`, `LangVersion=latest`
**Primary Dependencies**: LiteBus 4.3.0 (CQRS), Microsoft.Playwright 1.58.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0, Swashbuckle.AspNetCore 7.3.1
**Storage**: Azure SQL Managed Instance (via Microsoft.Data.SqlClient 7.0.0), Liquibase for DDL
**Testing**: xUnit 2.9.3 + NSubstitute 5.3.0, Testcontainers.MsSql 4.11.0, Microsoft.AspNetCore.Mvc.Testing 10.0.5
**Target Platform**: Azure AKS, Alpine-based containers, HTTPS-only (port 8443)
**Project Type**: Web service (ASP.NET Core Minimal API)
**Performance Goals**: Search results within 30 seconds for typical queries; CO and WY can run simultaneously
**Constraints**: TreatWarningsAsErrors, CodeAnalysisTreatWarningsAsErrors, AnalysisLevel latest-all, EnforceCodeStyleInBuild; per-state concurrency limit of 1
**Scale/Scope**: 2 states (CO, WY), ~17 FRs, 5 user stories

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Status | Notes |
|---|-----------|--------|-------|
| I | Test-First (NON-NEGOTIABLE) | PASS | TDD Red-Green-Refactor for all new code. Unit tests for normalized models, handler validation, CAPTCHA solver client. Integration tests for WY scraper + audit. E2E tests with known WY entities. |
| II | SOLID Design | PASS | `IStateSearchScraper` interface (ISP, DIP); per-state scraper classes (SRP); handler uses strategy pattern via state→scraper mapping (OCP). No inheritance hierarchies — composition via DI. |
| III | Quality Attributes | PASS | Reliability: Polly retry on FAB calls. Resilience: per-state semaphores. Security: state parameter validation, SSRF protection on WY URLs. Supportability: structured logging + metrics per state. Auditability: state column in audit trail. Observability: screenshots, tracing. |
| IV | Versioning (SemVer+CalVer) | PASS | Breaking change (old endpoint removed, response schema changed) → MAJOR bump to `2.0.2603.0`. Liquibase changelog `2.0.2603.0-add-state-column-to-search-audit.sql`. |
| V | Hands-Free Deployment | PASS | Liquibase DDL with rollback; all config via appsettings/Key Vault; FAB agent token in Key Vault. |

**Gate Result**: PASS — no violations.

## Project Structure

### Documentation (this feature)

```text
specs/005-multi-state-search/
├── spec.md
├── plan.md                          # This file
├── research.md                      # Phase 0 output
├── data-model.md                    # Phase 1 output
├── quickstart.md                    # Phase 1 output
├── fab-captcha-agent-config.md      # FAB dashboard config guide
├── checklists/
│   └── requirements.md
└── contracts/
    └── business-search-api.md       # Phase 1 output
```

### Source Code (repository root)

```text
src/CososWebScraping.Api/
├── Common/
│   ├── Configuration/
│   │   ├── PlaywrightOptions.cs          # MODIFY: add WY search URL
│   │   ├── FabAgentOptions.cs            # NEW: FAB CAPTCHA solver config
│   │   └── ...
│   ├── Interfaces/
│   │   ├── IStateSearchScraper.cs        # NEW: semantic scraper interface
│   │   ├── ICaptchaSolver.cs             # NEW: CAPTCHA solver abstraction
│   │   ├── IBusinessEntityScraper.cs     # REMOVE: replaced by IStateSearchScraper
│   │   └── ...
│   └── Serialization/
│       └── AppJsonSerializerContext.cs    # MODIFY: add new response types
├── Domain/
│   └── Entities/
│       └── SearchAuditEntry.cs           # MODIFY: add State property
├── Features/
│   └── BusinessSearch/
│       ├── Endpoints/
│       │   └── BusinessSearchEndpoints.cs    # MODIFY: add state param, new response types
│       ├── Exceptions/
│       │   ├── CaptchaResolutionException.cs # NEW
│       │   └── ...
│       ├── Handlers/
│       │   └── SearchBusinessEntitiesHandler.cs  # MODIFY: state routing, per-state semaphore
│       ├── Logging/
│       │   └── BusinessSearchLogMessages.cs  # MODIFY: add state to log messages
│       ├── Metrics/
│       │   └── BusinessSearchMetrics.cs      # MODIFY: add state dimension
│       ├── Models/
│       │   ├── NormalizedSearchResult.cs     # NEW: normalized result with common + state fields
│       │   ├── NormalizedSearchResponse.cs   # NEW: normalized response envelope
│       │   ├── BusinessEntityResult.cs       # KEEP: used internally by CO scraper
│       │   └── ...
│       └── Queries/
│           └── SearchBusinessEntitiesQuery.cs  # MODIFY: add State property
├── Infrastructure/
│   ├── Scraping/
│   │   ├── PlaywrightBusinessEntityScraper.cs  # MODIFY: implement IStateSearchScraper
│   │   ├── PlaywrightWyomingSearchScraper.cs   # NEW: WY scraper
│   │   └── ...
│   ├── CaptchaSolving/
│   │   └── FabCaptchaSolver.cs                 # NEW: FAB AI agent HTTP client
│   └── Persistence/
│       └── AuditTrailRepository.cs             # MODIFY: add State column

deploy/liquibase/
├── changelog/ddl/
│   └── 2.0.2603.0-add-state-column-to-search-audit.sql  # NEW
└── masterfiles/
    └── master-1.0.2603.0.yaml                            # MODIFY: add new changelog

tests/
├── CososWebScraping.UnitTests/Features/BusinessSearch/
│   ├── StateParameterValidationTests.cs       # NEW
│   ├── NormalizedResponseMappingTests.cs      # NEW
│   ├── FabCaptchaSolverTests.cs               # NEW
│   ├── WyomingSearchScrapingTests.cs          # NEW
│   └── ...                                    # MODIFY existing tests for new response shape
├── CososWebScraping.IntegrationTests/Features/BusinessSearch/
│   ├── MultiStateEndpointTests.cs             # NEW
│   ├── AuditTrailStateColumnTests.cs          # NEW
│   └── ...
└── CososWebScraping.EndToEndTests/Scenarios/BusinessSearch/
    └── WyomingSearchTests.cs                  # NEW
```

**Structure Decision**: The feature follows the existing vertical slice architecture under `Features/BusinessSearch/`. The WY scraper goes in `Infrastructure/Scraping/` alongside the existing CO scraper. FAB CAPTCHA solving gets its own `Infrastructure/CaptchaSolving/` folder since it's a new cross-cutting infrastructure concern. No new projects are needed.
