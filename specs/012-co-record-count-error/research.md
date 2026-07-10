# Research: Semantic Error Handling for Colorado Exceeded Record Count

**Feature**: 012-co-record-count-error  
**Date**: 2026-03-27

## Research Task 1: Root Cause of Generic 500 Response

### Decision
The generic 500 response is caused by `ScrapingFlowEngine.ExecuteAsync` wrapping `CheckTextMatchException` in `InvalidOperationException` before the caller (`JsonDrivenSearchScraper.SearchCoreAsync`) can intercept it.

### Rationale
Stack trace analysis confirms the exception chain:
1. `CheckTextActionHandler` throws `CheckTextMatchException` (line 28)
2. `ScrapingFlowEngine.ExecuteAsync` generic catch wraps it: `throw new InvalidOperationException($"Flow '{name}' failed: {ex.Message}", ex)`
3. `SearchCoreAsync` has `catch (CheckTextMatchException ex)` — **unreachable** because step 2 already wrapped it
4. Polly retries `InvalidOperationException` (in `ShouldHandle` predicate)
5. After retry exhaustion, `GlobalExceptionHandler` maps `InvalidOperationException` to the `_` default → 500

The double appearance of `SearchCoreAsync` at the same line number in the stack trace confirms retries are occurring.

### Alternatives Considered
1. **Unwrap InnerException in SearchCoreAsync**: Could catch `InvalidOperationException` and check `InnerException is CheckTextMatchException`. Rejected — fragile, relies on wrapping implementation detail.
2. **Add CheckTextMatchException to GlobalExceptionHandler directly**: Would skip the `ExceededRecordCountException` conversion and lose the domain-specific semantics. Rejected — the existing catch in `SearchCoreAsync` already provides the right conversion.
3. **Modify Polly ShouldHandle to exclude CheckTextMatchException**: Would prevent retry but the exception would still be wrapped. Rejected — treats symptom, not cause.

## Research Task 2: Impact of Letting CheckTextMatchException Propagate

### Decision
Adding a specific catch for `CheckTextMatchException` in `ScrapingFlowEngine.ExecuteAsync` (re-throw without wrapping) is safe and complete.

### Rationale
After the fix, the exception flow becomes:
1. `CheckTextMatchException` propagates as-is from `ScrapingFlowEngine`
2. Polly does NOT retry — `ShouldHandle` only includes `PlaywrightException` and `InvalidOperationException`
3. `SearchCoreAsync` catches `CheckTextMatchException` with `exceeded-record-count` → `ExceededRecordCountException`
4. Polly does NOT retry `ExceededRecordCountException` (also not in `ShouldHandle`)
5. `SearchBusinessEntitiesHandler` catch block → `MapStatus` returns `"ExceededRecordCount"` → audit trail written
6. `GlobalExceptionHandler` → 422 with Problem Details

Every component in the chain is already correctly implemented. The only broken link is the exception wrapping in `ScrapingFlowEngine`.

### Verification of Existing Infrastructure

| Component | File | Status |
|-----------|------|--------|
| `CheckTextMatchException` class | `Actions/CheckTextActionHandler.cs` | Has `ErrorType` property |
| `ExceededRecordCountException` class | `Exceptions/ExceededRecordCountException.cs` | Exists |
| `SearchCoreAsync` catch clause | `JsonDrivenSearchScraper.cs:98-103` | Catches `CheckTextMatchException` where `ErrorType == "exceeded-record-count"` |
| Polly `ShouldHandle` | `JsonDrivenSearchScraper.cs:452-453` | Only `PlaywrightException` + `InvalidOperationException` |
| `MapStatus` | `SearchBusinessEntitiesHandler.cs:88` | `ExceededRecordCountException → "ExceededRecordCount"` |
| `GlobalExceptionHandler` mapping | `GlobalExceptionHandler.cs:21` | `ExceededRecordCountException → 422` |
| `co-business-search.json` | `Definitions/co-business-search.json:37-43` | `check-text` with `errorType: "exceeded-record-count"`, `onMatch: "throw"` |
| Integration test | `ErrorResponseContractTests.cs:12` | Already tests 422 for `ExceededRecordCountException` |

## Research Task 3: Broader Impact on Other Check-Text Error Types

### Decision
Per the clarification, all `onMatch=throw` check-text matches are business-rule violations and should bypass retry. The fix achieves this generically — `CheckTextMatchException` (the base type for all check-text throws) is excluded from wrapping.

### Rationale
Currently, `exceeded-record-count` is the only `errorType` defined in any flow JSON. The fix is generic (catches `CheckTextMatchException` regardless of `errorType`), so any future check-text error types will automatically bypass retry without additional code changes. Per YAGNI, no additional error-type-to-exception mappings are added until they are needed.

### Alternatives Considered
None needed — the single-catch approach satisfies both the specific requirement (exceeded-record-count) and the general requirement (all check-text throws).
