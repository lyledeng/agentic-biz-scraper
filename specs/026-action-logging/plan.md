# Implementation Plan: Comprehensive Action Logging for Scraping Engine

**Branch**: `026-action-logging` | **Date**: 2026-04-02 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/026-action-logging/spec.md`

## Summary

Enhance the JSON scraping engine to provide comprehensive action-level structured logging. The four existing lifecycle log methods (`ActionStarted`, `ActionCompleted`, `ActionSkipped`, `ActionFailed`) are promoted from `Debug` to `Information` level and enriched with action-specific contextual details (URLs, selectors, patterns, elapsed time, etc.). Each action handler returns a detail string to the engine for inclusion in the single lifecycle log line. Sensitive variables (those with `sensitive: true` in the flow definition) are redacted using the existing `VariableSubstitution.ResolveSafe` infrastructure. A `Stopwatch` per action captures elapsed milliseconds for `ActionCompleted` entries. Retry attempts are logged with attempt number and delay. No new projects, endpoints, or database changes are required.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (LTS), `net10.0`
**Primary Dependencies**: Playwright (Azure Playwright Workspaces), `[LoggerMessage]` source generation (Microsoft.Extensions.Logging)
**Storage**: N/A (logging only — no storage changes)
**Testing**: xUnit + NSubstitute (unit), existing E2E flow tests for log verification
**Target Platform**: Azure AKS, Kestrel on port 8443 (HTTPS only)
**Project Type**: Web service (ASP.NET Core Minimal API)
**Performance Goals**: Zero measurable overhead from logging changes (source-generated methods, no runtime string formatting)
**Constraints**: No plaintext sensitive values in logs; all new log methods must use `[LoggerMessage]` source generation
**Scale/Scope**: 11 action handlers modified, ~3 core files modified, 5 modified/new source-generated log methods + per-handler iteration logging

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Test-First | **PASS** | Unit tests for each handler's detail generation; unit tests for sensitive redaction; E2E log output verification |
| II. SOLID Design | **PASS** | Handlers remain single-responsibility; detail generation is an internal concern of each handler; no new abstractions needed |
| III. Architectural Quality | **PASS** | Structured logging with correlation ID per FR-003; `[LoggerMessage]` source-generated per FR-007; sensitive redaction per FR-004 |
| IV. Hybrid SemVer+CalVer | **PASS** | No version-impacting changes (internal logging enhancement, no API surface changes) |
| V. Hands-Free Deployment | **PASS** | No migration, no config changes; logging level promotion requires no deployment steps |
| VI. Documentation | **PASS** | All new `[LoggerMessage]` methods self-document via message templates; plan and spec artifacts cover the feature |

**Gate Result**: PASS — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/026-action-logging/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/Infrastructure/Scraping/Engine/
├── EngineLogging.cs                        # MODIFY: promote 4 lifecycle methods Debug→Information,
│                                           #   add detail/elapsed params, add RetryAttempt method
├── ScrapingFlowEngine.cs                   # MODIFY: add Stopwatch per action, build sensitiveNames
│                                           #   set from definition, pass details to log methods
├── VariableSubstitution.cs                 # NO CHANGE (ResolveSafe already exists)
├── ActionContext.cs                        # MODIFY: add SensitiveVariableNames property
├── Models/
│   └── FlowDefinitionV2.cs                # NO CHANGE (VariableV2.Sensitive already exists)
└── Actions/
    ├── IActionHandler.cs                   # MODIFY: add GetLogDetails method to interface
    ├── NavigateActionHandler.cs            # MODIFY: implement GetLogDetails
    ├── FillActionHandler.cs                # MODIFY: implement GetLogDetails + inject ILogger
    ├── ClickActionHandler.cs               # MODIFY: implement GetLogDetails
    ├── ExtractActionHandler.cs             # MODIFY: implement GetLogDetails
    ├── CheckTextActionHandler.cs           # MODIFY: implement GetLogDetails
    ├── DownloadActionHandler.cs            # MODIFY: implement GetLogDetails (already has ILogger)
    ├── LoopActionHandler.cs                # MODIFY: implement GetLogDetails (already has ILogger)
    ├── ScreenshotActionHandler.cs          # MODIFY: implement GetLogDetails
    ├── WaitForLoadActionHandler.cs         # MODIFY: implement GetLogDetails
    ├── WaitForConditionActionHandler.cs    # MODIFY: implement GetLogDetails
    └── CallServiceActionHandler.cs         # MODIFY: implement GetLogDetails

tests/BizScraper.UnitTests/Infrastructure/Scraping/Engine/
├── Actions/                                # NEW: per-handler GetLogDetails tests
└── EngineLoggingTests.cs                   # NEW: verify log level and structured params
```

**Structure Decision**: All changes within the existing `Infrastructure/Scraping/Engine/` vertical slice. No new projects. The engine already owns the logging surface; handlers are extended with a detail-generation method.

## Complexity Tracking

No constitution violations — table not applicable.
