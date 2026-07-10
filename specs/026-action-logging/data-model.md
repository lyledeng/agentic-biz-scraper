# Data Model: Comprehensive Action Logging

## Overview

This feature modifies existing interfaces and models rather than introducing new persistent entities. The "data model" here describes the interface changes and the structured data flowing through the logging pipeline.

## Interface Changes

### IActionHandler (modified)

```csharp
public interface IActionHandler
{
    string ActionType { get; }
    Task ExecuteAsync(ActionContext context, FlowActionV2 action, CancellationToken cancellationToken);

    // NEW: Returns action-specific detail string for the ActionStarted log entry.
    // Returns null if no details are relevant.
    string? GetLogDetails(ActionContext context, FlowActionV2 action);

    // NEW: Returns post-execution detail string for the ActionCompleted log entry.
    // Default implementation returns null (most handlers have no post-execution state to report).
    string? GetCompletionDetails(ActionContext context, FlowActionV2 action) => null;
}
```

**Relationships**: Called by `ScrapingFlowEngine` before and after `ExecuteAsync`, respectively. The returned strings are passed as `{Details}` parameters to `EngineLogging` methods.

**Validation**: Return value is `string?` — null is valid and means "no details to log".

## Model Changes

### ActionContext (modified)

| Field | Type | Description |
|-------|------|-------------|
| `SensitiveVariableNames` | `IReadOnlySet<string>` | Set of variable names marked `sensitive: true` in the flow definition. Built once at flow start. |
| `LoopDepth` | `int` | Current loop nesting depth (0 = not in a loop). Set by `LoopActionHandler` before sub-action execution. |
| `LoopIteration` | `int` | Current iteration index within the innermost loop (0-based). Set by `LoopActionHandler` per iteration. |

**State**: Set by `ScrapingFlowEngine.ExecuteAsync` after `InitializeVariableDefaults`, before `ExecuteActionsAsync`. Immutable for the duration of the flow.

## Log Method Signatures

### Modified Methods (EngineLogging.cs)

| Method | Level | Message Template |
|--------|-------|-----------------|
| `ActionStarted` | Information | `"Action started: [{ActionIndex}] {ActionType} (id={ActionId}) - {Description} [{Details}] [CorrelationId={CorrelationId}]"` |
| `ActionCompleted` | Information | `"Action completed: [{ActionIndex}] {ActionType} (id={ActionId}) in {ElapsedMs}ms [{Details}] [CorrelationId={CorrelationId}]"` |
| `ActionSkipped` | Information | `"Action skipped (condition false): [{ActionIndex}] {ActionType} (id={ActionId}) condition={Condition} [CorrelationId={CorrelationId}]"` |
| `ActionFailed` | Error | `"Action failed: [{ActionIndex}] {ActionType} (id={ActionId}) - {ErrorMessage} [{Details}] [CorrelationId={CorrelationId}]"` |

### New Methods (EngineLogging.cs)

| Method | Level | Message Template |
|--------|-------|-----------------|
| `RetryAttempt` | Warning | `"Action retry: {ActionType} (id={ActionId}) attempt {Attempt}/{MaxAttempts} after {DelayMs}ms"` |

## Detail String Contracts Per Action Type

| Action Type | GetLogDetails Output | GetCompletionDetails Output |
|-------------|---------------------|----------------------------|
| navigate | `url={resolved_url}, waitUntil={state}` | null |
| fill | `target={description}, value={resolved_or_redacted}` | null |
| click | `target={description}, waitAfter={state}` | null |
| extract | `script={ref}, storeAs={variable}, postProcessor={name\|none}` | `resultLength={n}` |
| check-text | `pattern={pattern}, onMatch={behavior}` | `matched={true/false}` |
| download | `selector={description}, filenamePattern={pattern}` | `destination={path}` |
| loop | `maxIterations={n}, aggregateInto={var}` | null (per-iteration logging handled inside handler) |
| screenshot | `milestone={name}, force={flag}` | null |
| wait-for-load | `state={loadState}` | null |
| wait-for-condition | `condition={summary}, timeoutMs={ms}` | `outcome=met` |
| call-service | `service={name}, input={var}, output={var}` | null |

## Sensitive Data Flow

```
FlowDefinitionV2.Variables
  │ where Sensitive == true
  ▼
HashSet<string> sensitiveNames (built once at flow start)
  │ stored on
  ▼
ActionContext.SensitiveVariableNames
  │ used by handlers in
  ▼
VariableSubstitution.ResolveSafe(template, variables, sensitiveNames)
  │ returns "***" for sensitive, full value for non-sensitive
  ▼
Handler.GetLogDetails() detail string
  │ for fill: sensitive → "value=[N chars]", non-sensitive → "value=Acme Corp"
  ▼
EngineLogging.ActionStarted(..., details, ...)
```
