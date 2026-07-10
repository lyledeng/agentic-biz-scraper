# Implementation Plan: Windows VM Proxy for Chrome Headed Mode

**Branch**: `033-windows-headed-proxy` | **Date**: 2026-04-06 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/033-windows-headed-proxy/spec.md`

## Summary

When the AKS-hosted BizScraper API encounters a scraping definition with `"mode": "windows"` in its browser config, it transparently forwards the entire request (including bearer token and correlation ID) to a Windows VM running the same BizScraper API as an IIS app at `/mvpoc/bizscrapper-api`. The Windows VM executes headed Chrome for anti-bot bypass and handles all post-processing independently. The AKS API returns the Windows VM's response unmodified. Health checks report Windows VM connectivity status.

> **Note**: The IIS virtual directory path is `/mvpoc/bizscrapper-api` (double 'p' — matches the deployed IIS app name). The codebase default `PathBase` in Swagger is `/mvpoc/bizscraper-api` (single 'p'). The Windows VM's `appsettings.json` must set `"PathBase": "/mvpoc/bizscrapper-api"` to match the IIS configuration.

## Technical Context

**Language/Version**: C# 14 / .NET 10.0 (LTS)
**Primary Dependencies**: Microsoft.Playwright 1.58.0, Microsoft.Identity.Web 3.14.1, Flurl.Http 4.0.2, Polly 8.6.6, Azure.Storage.Blobs 12.24.0, LiteBus 4.3.0, OpenTelemetry 1.15.0+
**Storage**: Azure Blob Storage (screenshots, documents); SQLite (audit trail, per-instance)
**Testing**: xUnit 2.9.3 + NSubstitute (unit), integration tests, k6 load tests
**Target Platform**: AKS (Linux containers) → proxy → Windows VM (IIS at `/mvpoc/bizscrapper-api`)
**Project Type**: Web service (ASP.NET Minimal API)
**Performance Goals**: Proxied requests complete within 180s (headed Chrome timeout); proxy overhead < 1s
**Constraints**: 180s proxy timeout (headed scraping non-idempotent — no retry); HTTPS only to Windows VM; forward original bearer token
**Scale/Scope**: 1–2 concurrent headed executions on Windows VM; existing local/remote pools unaffected

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | **PASS** | Unit tests for proxy service, mode resolution, health check extension; integration tests for proxy flow |
| II. SOLID Design | **PASS** | New `IWindowsProxyService` follows ISP/DIP; `GenericScriptScraper` extended via composition; single-responsibility proxy class |
| III. Quality Attributes | **PASS** | Resilience: 503/504 error propagation, no retry on non-idempotent ops; Security: token forwarding, HTTPS-only; Observability: structured logging per proxied request, health check extension |
| IV. Versioning | **PASS** | No API contract change — existing `/api/v2/execute-script` request/response shape unchanged |
| V. Hands-Free Deployment | **PASS** | Configuration via `appsettings.json` / environment variables; no manual steps beyond config |
| VI. Documentation | **PASS** | Spec, plan, data-model, contracts, quickstart produced; inline doc-comments on public types |
| Quality Gates | **PASS** | Test gate (unit + integration), static analysis, security (no new secrets in source), build gate |

## Project Structure

### Documentation (this feature)

```text
specs/033-windows-headed-proxy/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Common/Configuration/
│   ├── PlaywrightOptions.cs              # Existing — no changes
│   └── WindowsProxyOptions.cs            # NEW — proxy endpoint, timeout config
├── Features/
│   ├── ExecuteScript/
│   │   ├── Endpoints/ExecuteScriptEndpoints.cs  # Existing — no changes
│   │   └── Handlers/ExecuteScriptHandler.cs     # Existing — delegates to scraper
│   └── HealthCheck/
│       ├── HealthCheckEndpoints.cs        # Existing — extended with windows proxy status
│       └── HealthModels.cs                # Existing — extended with WindowsProxy section
├── Infrastructure/
│   └── Scraping/
│       ├── Engine/
│       │   ├── GenericScriptScraper.cs    # Existing — add "windows" mode branch
│       │   └── Models/FlowDefinitionV2.cs # Existing — BrowserConfig.Mode already string-based
│       └── Proxy/
│           ├── IWindowsProxyService.cs    # NEW — interface
│           └── WindowsProxyService.cs     # NEW — HttpClient-based forwarding

tests/BizScraper.UnitTests/
├── Infrastructure/Scraping/Proxy/
│   └── WindowsProxyServiceTests.cs        # NEW
├── Infrastructure/Scraping/Engine/
│   └── GenericScriptScraperModeResolutionTests.cs  # NEW or extend existing
└── Features/HealthCheck/
    └── HealthCheckWindowsProxyTests.cs    # NEW
```

**Structure Decision**: Existing single-project structure. New proxy service lives under `Infrastructure/Scraping/Proxy/` following existing separation of concerns. Configuration options follow the established `Common/Configuration/` pattern.

## Complexity Tracking

No constitution violations — no entries required.
