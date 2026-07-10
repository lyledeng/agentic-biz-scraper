# Implementation Plan: Disable Remote Browser Caching

**Branch**: `037-no-remote-browser-cache` | **Date**: 2026-04-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/037-no-remote-browser-cache/spec.md`

## Summary

Eliminate Azure Playwright billing from idle cached sessions by changing remote browser lifecycle from a pooled model (shared `IBrowser` across requests) to a per-request model (connect → execute → close). Local browser pooling remains unchanged. The readiness probe is updated to avoid creating remote sessions.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: Microsoft.Playwright (Chromium.ConnectAsync, Playwright.CreateAsync)  
**Storage**: N/A (no new persistence; existing audit trail unchanged)  
**Testing**: xUnit + NSubstitute (unit tests for lifecycle behavior)  
**Target Platform**: Linux container on AKS  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Performance Goals**: Remote `ConnectAsync` completes within 30-second budget; local pool behavior unchanged  
**Constraints**: Zero idle Azure Playwright sessions between requests  
**Scale/Scope**: 3 concurrent remote executions (existing semaphore)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests required for new per-request lifecycle |
| II. SOLID Design | PASS | SRP: remote lifecycle extracted from shared pool logic. OCP: local pool untouched. |
| III. Quality Attributes | PASS | Observability: connect/disconnect logging (FR-007). Reliability: one-retry on disconnect (FR-004). |
| IV. Versioning | N/A | No versioned artifacts (internal behavioral change) |
| V. Hands-Free Deploy | PASS | No manual steps; same CI/CD pipeline |
| VI. Documentation | PASS | XML doc-comments on changed public methods |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/037-no-remote-browser-cache/
├── spec.md              # Feature specification (complete)
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (behavioral model)
├── quickstart.md        # Phase 1 output (verification guide)
├── checklists/
│   └── requirements.md  # Spec quality checklist (complete)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Infrastructure/Scraping/Engine/
│   ├── GenericScriptScraper.cs          # PRIMARY: per-request remote lifecycle
│   ├── BrowserPool.cs                   # UNCHANGED for local; remote pool becomes unused
│   └── Models/BrowserConfig.cs          # UNCHANGED
├── Features/HealthCheck/
│   └── HealthCheckEndpoints.cs          # UNCHANGED (already fixed)
├── Common/Configuration/
│   └── PlaywrightOptions.cs             # UNCHANGED (no new config needed)
└── appsettings.json                     # UNCHANGED

tests/BizScraper.UnitTests/
└── Infrastructure/Scraping/Engine/
    └── GenericScriptScraperRemoteLifecycleTests.cs  # NEW: per-request lifecycle tests
```

**Structure Decision**: No new files except a test class. All changes are in `GenericScriptScraper.cs` — modifying `ExecuteCoreAsync` for remote mode to create/dispose Playwright+browser per-request instead of using the shared `_remotePool`.

## Complexity Tracking

No violations — no entries needed.
