# Implementation Plan: Scraping Diagnostics — Traces & Screenshots

**Branch**: `002-scraping-diagnostics` | **Date**: 2026-03-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-scraping-diagnostics/spec.md`

## Summary

Add configurable Playwright trace capture and milestone screenshot support to the existing `PlaywrightBusinessEntityScraper`. When enabled, every scraping session records a self-contained trace archive (`.zip` with embedded screenshots) and saves PNG screenshots at key workflow milestones (page load, search submit, each paginated page, failure). Trace mode defaults to "always"; screenshots default to enabled. Diagnostic artifacts are written to a configurable local filesystem directory, named by correlation ID and UTC timestamp. Diagnostic failures are swallowed, logged, and never propagate to the caller.

## Technical Context

**Language/Version**: .NET 10 with C# 14 (`net10.0`, `latest` language version)
**Primary Dependencies**: Microsoft.Playwright 1.58.0, Polly 8.6.6, LiteBus 4.3.0, OpenTelemetry 1.15.x, Swashbuckle 7.3.1
**Storage**: Azure SQL (audit trail — not affected by this feature); local filesystem for diagnostic artifacts
**Testing**: xUnit 2.9.3 + NSubstitute 5.3.0 (unit), Testcontainers.MsSql 4.11.0 (integration), ASP.NET MVC Testing (integration)
**Target Platform**: Azure AKS (Linux containers), Azure Playwright Workspaces (remote browser)
**Project Type**: Web service (ASP.NET Minimal API)
**Performance Goals**: Diagnostics MUST NOT degrade search response time when disabled; modest overhead acceptable when enabled
**Constraints**: Read-only root filesystem in containers — diagnostics output path must be a writable volume mount
**Scale/Scope**: Single scraper service; diagnostics are per-request artifacts; no shared state

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for config parsing, tracing mode logic, file naming. Unit tests (NSubstitute-mocked Playwright lifecycle) for trace/screenshot capture behaviour. |
| II. SOLID Design | PASS | New `DiagnosticsOptions` config class (SRP). Diagnostics logic added to existing scraper via composition, not new abstractions. No speculative interfaces. |
| III. Quality Attributes | PASS | FR-008/FR-009 ensure reliability (swallow + log). Structured logs via `[LoggerMessage]`. Correlation ID in all filenames. |
| IV. Hybrid SemVer+CalVer | PASS | No new versioned artifacts — configuration keys only. |
| V. Hands-Free Deployment | PASS | New config section in `appsettings.json`; Helm values for writable volume mount can be added later. |
| KISS/YAGNI | PASS | No new projects, no new abstractions. Extends existing scraper and options class. |

## Project Structure

### Documentation (this feature)

```text
specs/002-scraping-diagnostics/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (not applicable — no new external interfaces)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/CososWebScraping.Api/
├── Common/
│   └── Configuration/
│       ├── PlaywrightOptions.cs          # MODIFIED — add Diagnostics sub-section
│       └── DiagnosticsOptions.cs         # NEW — TracingMode enum, OutputPath, ScreenshotsEnabled
├── Infrastructure/
│   └── Scraping/
│       └── PlaywrightBusinessEntityScraper.cs  # MODIFIED — add tracing/screenshot lifecycle
├── Features/
│   └── BusinessSearch/
│       └── Logging/
│           └── BusinessSearchLogMessages.cs    # MODIFIED — add diagnostics log messages

tests/CososWebScraping.UnitTests/
├── Common/
│   └── Configuration/
│       └── DiagnosticsOptionsTests.cs          # NEW — config defaults, tracing mode parsing, directory creation
└── Infrastructure/
    └── Scraping/
        ├── ScrapingDiagnosticsTracingTests.cs  # NEW — tracing lifecycle and conditional save tests
        └── ScrapingDiagnosticsScreenshotTests.cs # NEW — milestone and error screenshot tests
```

**Structure Decision**: No new projects. The feature extends the existing `PlaywrightBusinessEntityScraper` and `PlaywrightOptions` within the current vertical slice. A new `DiagnosticsOptions` class is added for clean configuration separation but lives in `Common/Configuration/` alongside the parent options class.

## Complexity Tracking

No constitution violations. No complexity justifications required.
