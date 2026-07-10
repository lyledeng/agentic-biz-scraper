# Research: Comprehensive Action Logging

## R1: How to Surface Per-Handler Details Without Changing ExecuteAsync Return Type

**Decision**: Add a `GetLogDetails(ActionContext, FlowActionV2)` method to `IActionHandler` that returns a `string?` summary of action-specific details for inclusion in the enriched `ActionStarted` log entry.

**Rationale**:
- The engine already calls `handler.ExecuteAsync()` which returns `Task` (void). Changing the return type to carry details would be a breaking interface change affecting all 11 handlers.
- A separate synchronous `GetLogDetails` method keeps the execution path unchanged and produces the enrichment string *before* execution (for `ActionStarted`) using the action's declarative properties (URL, selector, pattern, etc.) — no execution state needed.
- Post-execution details (e.g., check-text match result, extract stored value) that are only known *after* execution can be captured from `context.Variables` by the engine or via a second `GetCompletionDetails(ActionContext, FlowActionV2)` method for `ActionCompleted`.

**Alternatives Considered**:
- **Return `ActionResult` from `ExecuteAsync`**: Too invasive; changes all 11 handlers, adds allocation overhead, complicates the simple fire-and-forget contract.
- **Inject ILogger into every handler and let each handler log independently**: Violates the spec requirement of enriching the existing single lifecycle log line (no companion entries). Creates inconsistent formatting across handlers.
- **Use static helper methods per action type in the engine**: Keeps engine monolithic; logic for what to log about a `fill` action should live near the `fill` handler, not in the engine.

## R2: How to Build the Sensitive Variable Names Set

**Decision**: Build a `HashSet<string>` from the flow definition's `Variables` collection where `Sensitive == true` at flow start, store it on `ActionContext.SensitiveVariableNames`, and pass it to `VariableSubstitution.ResolveSafe()` when generating log detail strings.

**Rationale**:
- `VariableSubstitution.ResolveSafe(template, variables, sensitiveNames)` already exists (T050 from spec 014).
- `VariableV2.Sensitive` is a `bool?` — treat `null` and `false` as non-sensitive (matches existing convention from T052 unit tests).
- Building the set once at flow start avoids repeated enumeration.
- Storing on `ActionContext` makes it available to handlers' `GetLogDetails` without injecting the full definition.

**Alternatives Considered**:
- **Pass `FlowDefinitionV2` to each handler**: Exposes too much; handlers only need the sensitive names set for logging.
- **Rebuild the set per action**: Wasteful; the variable definitions don't change during execution.

## R3: How to Capture Elapsed Time per Action

**Decision**: Wrap the `handler.ExecuteAsync()` call in `ScrapingFlowEngine.ExecuteActionsAsync` with a `System.Diagnostics.Stopwatch`, passing `elapsed.ElapsedMilliseconds` to the enriched `ActionCompleted` log method.

**Rationale**:
- `Stopwatch` is the standard .NET high-resolution timer; `Stopwatch.StartNew()` + `elapsed.ElapsedMilliseconds` is idiomatic.
- The engine loop already owns the before/after lifecycle logging, making it the natural place to measure.
- No per-handler changes needed for timing.

**Alternatives Considered**:
- **`DateTime.UtcNow` difference**: Lower resolution, not recommended for performance measurement.
- **Per-handler timing**: Would require every handler to start/stop a timer, duplicating logic across 11 files.

## R4: Enriched Log Method Signatures

**Decision**: Modify the four existing `[LoggerMessage]` methods in `EngineLogging.cs`:

| Method | Level Change | New Parameters |
|--------|-------------|----------------|
| `ActionStarted` | Debug → Information | `+string correlationId`, `+string? details` |
| `ActionCompleted` | Debug → Information | `+string correlationId`, `+long elapsedMs`, `+string? details` |
| `ActionSkipped` | Debug → Information | `+string correlationId`, `+string? condition` |
| `ActionFailed` | Error (unchanged) | `+string correlationId`, `+string? details` |

Add one new method:

| Method | Level | Parameters |
|--------|-------|------------|
| `RetryAttempt` | Warning | `string actionType`, `string? actionId`, `int attempt`, `int maxAttempts`, `int delayMs` |

**Rationale**:
- Enriching existing methods preserves backward compatibility for log consumers filtering on message patterns.
- Adding `correlationId` to action-level logs satisfies FR-003 without requiring a logging scope (which would add overhead).
- The retry method replaces the current `LogDebug` interpolated string call in `ExecuteWithRetryAsync`, converting it to source-generated form per FR-007.

**Alternatives Considered**:
- **Logging scopes for correlation ID**: Adds allocations and nesting complexity; the engine already has the correlation ID on the context.
- **Separate new methods instead of modifying existing**: Would require removing the old methods to avoid duplication, achieving the same result with more churn.

## R5: Detail String Format Per Action Type

**Decision**: Each handler's `GetLogDetails` returns a concise, structured string. The engine appends it to the log message template via the `{Details}` parameter.

| Action Type | Detail Format | Example |
|-------------|---------------|---------|
| `navigate` | `url={url}, waitUntil={state}` | `url=https://sos.state.co.us/..., waitUntil=NetworkIdle` |
| `fill` | `target={desc}, value={value\|***}` | `target=Business Name, value=Acme Corp` or `target=Captcha, value=***` |
| `click` | `target={desc}, waitAfter={state}` | `target=Search Button, waitAfter=NetworkIdle` |
| `extract` | `script={ref}, storeAs={var}, postProcessor={pp}` | `script=parseResults, storeAs=searchResults, postProcessor=none` |
| `check-text` | `pattern={pattern}, onMatch={behavior}` | `pattern=No records found, onMatch=throw` |
| `download` | `selector={desc}, filenamePattern={pattern}` | `selector=PDF Link, filenamePattern=certificate-*.pdf` |
| `loop` | `maxIterations={n}, aggregateInto={var}` | `maxIterations=10, aggregateInto=allResults` |
| `screenshot` | `milestone={name}, force={flag}` | `milestone=search-results, force=false` |
| `wait-for-load` | `state={loadState}` | `state=NetworkIdle` |
| `wait-for-condition` | `condition={summary}, timeoutMs={ms}` | `condition=document.querySelector('.results')..., timeoutMs=30000` |
| `call-service` | `service={name}, input={var}, output={var}` | `service=captcha-solver, input=captchaImage, output=captchaSolution` |

For `ActionCompleted`, a second method `GetCompletionDetails` captures post-execution state:
- `check-text`: `matched={true/false}`
- `extract`: `resultLength={n}` (character count of stored value)
- `wait-for-condition`: `outcome=met` (or timeout is an exception)
- Others: return `null` (no additional post-execution detail)

**Rationale**: Key-value format is grep-friendly and consistent with existing log patterns in the codebase. Values resolved from sensitive variables use `ResolveSafe` to produce `***`.

## R6: Sensitive Value Handling in Fill Detail Strings

**Decision**: Use `VariableSubstitution.ResolveSafe(action.Value, context.Variables, context.SensitiveVariableNames)` for the value portion of fill action details. For non-sensitive values, log the full resolved string. For sensitive values, log the character count: `value=[12 chars]`.

**Rationale**:
- The spec (Clarification 2) requires full values for `sensitive: false` and character count for `sensitive: true`.
- `ResolveSafe` returns `***` for sensitive variables — the handler can detect this and substitute the character count instead.
- Alternatively, the handler can check `context.SensitiveVariableNames` directly against the variable name referenced in `action.Value` to decide format.

**Alternatives Considered**:
- **Always redact fill values**: Contradicts the spec clarification — non-sensitive search terms should be visible for debugging.
- **Log full values for all**: Security risk; sensitive data would appear in logs.
