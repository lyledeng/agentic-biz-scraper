# Implementation Plan: Semantic Error Handling for Colorado Exceeded Record Count

**Branch**: `012-co-record-count-error` | **Date**: 2026-03-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/012-co-record-count-error/spec.md`

## Summary

The Colorado SOS "Exceeded Record Count" error currently surfaces as a generic 500 because `ScrapingFlowEngine` wraps all exceptions in `InvalidOperationException` before the caller can intercept them, and the Polly retry pipeline retries `InvalidOperationException`. The fix is a single change: let `CheckTextMatchException` propagate through `ScrapingFlowEngine` without wrapping. This makes the existing (but currently unreachable) `ExceededRecordCountException` catch in `JsonDrivenSearchScraper`, the 422 mapping in `GlobalExceptionHandler`, and the `ExceededRecordCount` audit status all functional.

## Technical Context

**Language/Version**: C# 14 / .NET 10, target framework `net10.0`  
**Primary Dependencies**: Polly (retry pipeline), Playwright (scraping), LiteBus (CQRS)  
**Storage**: Azure SQL Managed Instance (audit trail — existing, no schema changes)  
**Testing**: xUnit + NSubstitute (unit), WebApplicationFactory (integration), Testcontainers (database integration)  
**Target Platform**: Azure AKS (Linux containers)  
**Project Type**: Web service (ASP.NET Core minimal API)  
**Performance Goals**: Error response within 10 seconds (no retry delay accumulation)  
**Constraints**: `TreatWarningsAsErrors`, `AnalysisLevel: latest-all`, `.editorconfig` style rules as errors  
**Scale/Scope**: Single file change in production code + test additions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for engine exception propagation + integration test for 422 response will be written before the production fix |
| II. SOLID & YAGNI | PASS | Minimal change (one catch clause in one file); no over-engineering; no speculative abstractions |
| III. Quality Attributes — Resilience | PASS | Transient retry behavior is preserved; only deterministic business-rule errors bypass retry |
| III. Quality Attributes — Auditability | PASS | Audit trail already records `MapStatus(ExceededRecordCountException) → "ExceededRecordCount"` — this path becomes reachable |
| III. Quality Attributes — Supportability | PASS | Existing `[LoggerMessage]` logging in `SearchBusinessEntitiesHandler` covers the failure path |
| IV. Versioning | N/A | No new artifacts require versioning |
| V. Hands-Free Deployment | PASS | Code-only change; no infrastructure or deployment modifications |
| Design Standards — Error Handling | PASS | Typed exception (`ExceededRecordCountException`) with structured RFC 9457 response replaces generic catch-all |

**Gate result**: PASS — all principles satisfied.

## Root Cause Analysis

The exception propagation chain currently works as follows:

1. `CheckTextActionHandler` throws `CheckTextMatchException` (with `errorType: "exceeded-record-count"`)
2. `ScrapingFlowEngine.ExecuteAsync` has a generic `catch (Exception ex)` that wraps **all** exceptions in `InvalidOperationException`
3. `JsonDrivenSearchScraper.SearchCoreAsync` has `catch (CheckTextMatchException ex)` — but this is **unreachable** because step 2 already wrapped it
4. The `InvalidOperationException` matches Polly's `ShouldHandle` predicate → **retries occur**
5. After retries exhaust, `InvalidOperationException` reaches `GlobalExceptionHandler` → matches the `_` default → **500**

The fix requires exactly **one production code change**: add a catch for `CheckTextMatchException` in `ScrapingFlowEngine.ExecuteAsync` that re-throws without wrapping. After this change:

1. `CheckTextMatchException` propagates as-is through `ScrapingFlowEngine`
2. Polly does NOT retry it (not in `ShouldHandle`: only `PlaywrightException` and `InvalidOperationException` are)
3. `SearchCoreAsync` catches it → throws `ExceededRecordCountException`
4. Polly does NOT retry `ExceededRecordCountException` (also not in `ShouldHandle`)
5. `SearchBusinessEntitiesHandler` catch block fires → audit trail writes status `"ExceededRecordCount"`
6. `GlobalExceptionHandler` matches `ExceededRecordCountException` → **422 with RFC 9457 Problem Details**

## Project Structure

### Documentation (this feature)

```text
specs/012-co-record-count-error/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (minimal — no new entities)
├── quickstart.md        # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (files to modify)

```text
src/BizScraper.Api/
└── Infrastructure/
    └── Scraping/
        └── Engine/
            └── ScrapingFlowEngine.cs          # THE fix: add CheckTextMatchException catch

tests/BizScraper.UnitTests/
└── Infrastructure/
    └── Scraping/
        └── Engine/
            └── ScrapingFlowEngineTests.cs     # NEW: test exception propagation

tests/BizScraper.UnitTests/
└── Middleware/
    └── GlobalExceptionHandlerTests.cs         # EXTEND: add 422 mapping test

tests/BizScraper.IntegrationTests/
└── Features/
    └── BusinessSearch/
        └── ErrorResponseContractTests.cs      # VERIFY: existing 422 contract test
```

**Structure Decision**: No new directories needed. The fix modifies one existing file and adds one new test file. Existing test files get extended.

## Implementation Design

### Change 1: `ScrapingFlowEngine.ExecuteAsync` — Let `CheckTextMatchException` propagate

**File**: `src/BizScraper.Api/Infrastructure/Scraping/Engine/ScrapingFlowEngine.cs`

In the `ExecuteAsync` method, add a specific catch for `CheckTextMatchException` before the generic catch. This allows the exception to propagate without being wrapped in `InvalidOperationException`, while still taking the error screenshot.

```csharp
// BEFORE (current):
catch (OperationCanceledException)
{
    throw;
}
catch (Exception ex)
{
    // ... screenshot ...
    throw new InvalidOperationException($"Flow '{definition.Name}' failed: {ex.Message}", ex);
}

// AFTER (fixed):
catch (OperationCanceledException)
{
    throw;
}
catch (CheckTextMatchException)
{
    try
    {
        await context.CaptureScreenshotAsync("error", force: true, cancellationToken);
    }
    catch
    {
        // Best-effort screenshot on error
    }

    throw; // Propagate as-is — business-rule violation, not retryable
}
catch (Exception ex)
{
    // ... screenshot ...
    throw new InvalidOperationException($"Flow '{definition.Name}' failed: {ex.Message}", ex);
}
```

### Verification: No Other Changes Needed

| Component | Already Correct | Why |
|-----------|----------------|-----|
| `JsonDrivenSearchScraper.SearchCoreAsync` | ✓ | Catches `CheckTextMatchException` with `exceeded-record-count` → throws `ExceededRecordCountException` |
| `JsonDrivenSearchScraper.BuildPipeline` | ✓ | Polly `ShouldHandle` only includes `PlaywrightException` and `InvalidOperationException` — neither `CheckTextMatchException` nor `ExceededRecordCountException` match |
| `SearchBusinessEntitiesHandler.MapStatus` | ✓ | Maps `ExceededRecordCountException → "ExceededRecordCount"` |
| `SearchBusinessEntitiesHandler.HandleAsync` | ✓ | Catch block writes audit trail before re-throwing |
| `GlobalExceptionHandler` | ✓ | Maps `ExceededRecordCountException → 422` with correct Problem Details |
| `ExceededRecordCountException` | ✓ | Already exists in `Features/BusinessSearch/Exceptions/` |
| `co-business-search.json` | ✓ | Already defines `check-text` action with `errorType: "exceeded-record-count"` and `onMatch: "throw"` |

## Complexity Tracking

No constitution violations. The fix is the minimum possible change (one catch clause) that makes the existing infrastructure work correctly.
