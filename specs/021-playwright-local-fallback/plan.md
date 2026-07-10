# Implementation Plan: Playwright Local Fallback

**Branch**: `021-playwright-local-fallback` | **Date**: 2026-04-01 | **Spec**: [spec.md](specs/021-playwright-local-fallback/spec.md)
**Input**: Feature specification from `/specs/021-playwright-local-fallback/spec.md`

## Summary

Enable the BizScraper API to operate in two browser modes — **Remote** (Azure Playwright workspace via `wss://` endpoint) and **Local** (headless Chromium via `LaunchAsync`) — selected by configuration. The approach consolidates all browser lifecycle management into `GenericScriptScraper`, removes the legacy `PlaywrightBrowserManager`, refactors the `/ready` health check, increases default concurrency from 1 to 3, and adds mode observability to startup logs and health check responses.

## Technical Context

**Language/Version**: .NET 10 / C# 14  
**Primary Dependencies**: Microsoft.Playwright 1.58, LiteBus (CQRS), OpenTelemetry  
**Storage**: SQLite (audit trail), Azure Blob Storage (diagnostics)  
**Testing**: xUnit, NSubstitute, Playwright test fixtures  
**Target Platform**: Linux container (Docker, `mcr.microsoft.com/playwright/dotnet:v1.58.0-noble`)  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Performance Goals**: 3 concurrent script executions (up from 1)  
**Constraints**: Readiness probe must respond within 5 seconds; browser recycling at 25 uses  
**Scale/Scope**: Single pod (1–3 replicas via HPA), ~20 scraping definitions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | ✅ PASS | Unit tests for `PlaywrightBrowserManager` diagnostics already exist; removal requires migrating static helpers + writing new health check tests |
| II. SOLID Design | ✅ PASS | Consolidation into `GenericScriptScraper` follows SRP (single browser manager); removing `PlaywrightBrowserManager` eliminates DRY violation |
| III. Quality Attributes | ✅ PASS | `/ready` refactored for both modes; mode logged at startup (Observability); `IsConnected` check provides resilience |
| IV. Versioning | ✅ PASS | No public API version change — internal refactoring only |
| V. Hands-Free Deployment | ✅ PASS | Mode selected via `Playwright__BrowserEndpoint` env var; no manual steps |
| VI. Documentation | ✅ PASS | `PlaywrightOptions` doc-comments updated; health check response documented |
| Design Standards – Configuration | ✅ PASS | 12-factor: empty `BrowserEndpoint` = Local mode; populated = Remote mode; fail-fast on invalid config |
| Design Standards – Error Handling | ✅ PASS | Startup validation error for Remote mode with missing endpoint |
| Design Standards – Global Spec Snapshot | ⚠️ DEFERRED | `specs/current-specs/` to be updated in follow-up PR post-merge |

## Project Structure

### Documentation (this feature)

```text
specs/021-playwright-local-fallback/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (health check response contract)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Common/Configuration/
│   └── PlaywrightOptions.cs              # ADD: IsRemoteMode computed property
├── Features/HealthCheck/
│   └── HealthCheckEndpoints.cs           # MODIFY: inject GenericScriptScraper, add mode to response
├── Infrastructure/Scraping/
│   ├── PlaywrightBrowserManager.cs       # DELETE: entire file
│   ├── PlaywrightEntityDetailScraper.cs  # DELETE: orphaned v1 scraper (already superseded by JSON engine)
│   └── Engine/
│       └── GenericScriptScraper.cs       # MODIFY: add CanConnectAsync, expose IsRemoteMode, startup logging
├── Program.cs                            # MODIFY: remove PlaywrightBrowserManager registration, add startup log

deploy/helm/bizscraper/
├── templates/deployment.yaml             # MODIFY: add Playwright__BrowserEndpoint env var
└── values.yaml                           # MODIFY: add playwright.browserEndpoint with empty default

tests/BizScraper.UnitTests/
├── Infrastructure/Scraping/
│   ├── ScrapingDiagnosticsTracingTests.cs     # MODIFY: delete orchestration tests, migrate static helper calls to DiagnosticsPathHelper
│   ├── ScrapingDiagnosticsScreenshotTests.cs  # MODIFY: delete orchestration tests, migrate static helper calls to DiagnosticsPathHelper
│   ├── GenericScriptScraperTests.cs           # NEW: tests for CanConnectAsync, IsRemoteMode
│   └── PlaywrightOptionsTests.cs              # NEW: tests for IsRemoteMode, startup validation
└── Common/Configuration/
    └── DiagnosticsOptionsTests.cs             # MODIFY: migrate static helper calls to DiagnosticsPathHelper
```

**Structure Decision**: Existing vertical-slice architecture. No new projects needed — changes span configuration, health check, scraping infrastructure, Helm deployment, and tests within the existing `BizScraper.Api` and `BizScraper.UnitTests` projects.

## Complexity Tracking

> No constitution violations. All changes follow KISS/YAGNI — consolidation reduces code, does not add abstractions.
