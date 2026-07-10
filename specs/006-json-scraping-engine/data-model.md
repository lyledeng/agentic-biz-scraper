# Data Model: JSON-Driven Scraping Engine

**Feature**: 006-json-scraping-engine  
**Date**: 2026-03-25

## Entities

### FlowDefinition

The root object of a JSON flow definition file. Represents one complete scraping workflow for a single endpoint.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| schemaVersion | integer | yes | Schema version for forward compatibility. Engine rejects unsupported versions at startup. |
| name | string | yes | Human-readable name (e.g., "Colorado Business Search") |
| state | string | yes | State code (e.g., "CO", "WY") |
| endpoint | string | yes | Endpoint identifier (e.g., "business-search", "entity-details") |
| variables | Variable[] | yes | Declared variables available during execution |
| actions | Action[] | yes | Ordered list of actions to execute |
| output | OutputDeclaration | yes | Declares the variable name and type of the flow's final result |

**Identity**: Unique by (state, endpoint) pair. No two definitions may share the same combination.

**Validation**: At startup, the engine validates all definitions. Invalid definitions cause startup failure.

---

### Variable

A named value available during flow execution. Can be injected at runtime, read from configuration, or captured from extraction output.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| name | string | yes | Variable name, referenced via `${name}` in action parameters |
| source | enum | yes | "runtime" (injected by caller), "config" (from app configuration), "extracted" (set by an extract action) |
| required | boolean | no | If true, engine fails if variable is not provided at execution time. Default: true for runtime variables. |

**Identity**: Unique by name within a FlowDefinition.

---

### Action

A single step in a flow. Discriminated by the `type` field.

#### Common Fields (all action types)

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| type | enum | yes | Action type discriminator |
| description | string | no | Human-readable label for logging |
| screenshot | ScreenshotConfig | no | If present, capture screenshot after this action |
| condition | Condition | no | If present, action executes only when condition is met |

#### Action Type: navigate

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| url | string | yes | URL to navigate to. Supports `${variable}` substitution. |
| waitUntil | enum | no | Load state to wait for: "networkidle", "domcontentloaded", "load". Default: "networkidle" |

#### Action Type: fill

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| selector | string | yes | CSS selector for the input element |
| value | string | yes | Value to enter. Supports `${variable}` substitution. |

#### Action Type: click

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| selector | string | yes | CSS selector for the element to click |
| waitAfter | enum | no | Load state to wait for after click. Default: "networkidle" |

#### Action Type: wait-for-load

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| state | enum | yes | "networkidle", "domcontentloaded", "load" |

#### Action Type: wait-for-condition

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| javascript | string | yes | JavaScript function body that returns truthy when condition is met |
| timeoutMs | integer | no | Timeout in milliseconds. Default: 5000 |

#### Action Type: extract

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| javascript | string | yes | JavaScript expression evaluated in page context. Must return a JSON-serializable value. |
| storeAs | string | yes | Variable name to store the extraction result |
| postProcessor | string | no | Name of a registered C# post-processor to transform raw output |

#### Action Type: check-text

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| pattern | string | yes | Text or regex pattern to search for in page body |
| isRegex | boolean | no | Whether pattern is a regex. Default: false |
| onMatch | enum | yes | "throw" (raise typed error), "skip" (skip remaining actions), "continue" (do nothing) |
| errorType | string | no | Error type identifier when onMatch is "throw" (e.g., "exceeded-record-count") |

#### Action Type: screenshot

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| milestone | string | yes | Milestone name for the screenshot file |
| force | boolean | no | Force capture even if screenshots are disabled. Default: false |

#### Action Type: download

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| linkSelector | string | yes | CSS selector for the download link |
| validateExtension | string | no | Required file extension (e.g., ".pdf"). Download rejected if mismatch. |
| filenamePattern | string | yes | Output filename pattern. Supports `${variable}` substitution. |
| storeAs | string | no | Variable name to store the local file path |

#### Action Type: loop

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| loopType | enum | yes | "pagination" (page through results) or "retry" (attempt until success) |
| maxIterations | integer or string | yes | Maximum number of iterations. May be a literal integer or a `${variable}` reference resolved at runtime. |
| terminateWhen | Condition | yes | Condition that ends the loop (e.g., next link not visible) |
| actions | Action[] | yes | Nested list of actions to execute per iteration |
| aggregateInto | string | conditional | Variable name to accumulate extraction results across iterations. Required when `loopType` is "pagination". Each iteration's extract result (identified by `storeAs` in a nested extract action) is appended to this aggregate array. |

**Engine-injected loop variables**: The engine automatically injects the following variables into the variable bag during loop execution. These do not need to be declared in the `variables` array:
- `currentPage` — 1-based index of the current iteration (starts at 1 for the first loop body execution, i.e., the second page).
- `nextPage` — `currentPage + 1`, for convenience in selector interpolation.
- `iterationCount` — 0-based count of completed iterations.

#### Action Type: call-service

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| serviceName | string | yes | Name of the registered external service (e.g., "captcha-solver") |
| inputVariable | string | yes | Variable name containing input data for the service |
| outputVariable | string | yes | Variable name to store the service response |

---

### Condition

A predicate evaluated at runtime to control conditional execution or loop termination.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| type | enum | yes | "element-exists", "element-visible", "text-contains", "variable-truthy", "variable-falsy", "not" |
| selector | string | conditional | CSS selector (for element-exists, element-visible) |
| text | string | conditional | Text to search for (for text-contains) |
| variableName | string | conditional | Variable to evaluate (for variable-truthy, variable-falsy) |
| condition | Condition | conditional | Inner condition to negate (for "not" type) |

---

### ScreenshotConfig

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| milestone | string | yes | Milestone name |
| force | boolean | no | Force capture. Default: false |

---

### OutputDeclaration

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| variableName | string | yes | Name of the variable holding the final result |
| type | string | yes | Logical type name for documentation (e.g., "NormalizedSearchResult[]") |

**Engine-provided metadata**: When returning results, the engine wraps the output in an envelope that includes:
- `truncated` (boolean) — `true` if the flow terminated early during a pagination loop (e.g., mid-pagination error, session expiration). The adapter classes surface this flag in the API response so callers know results may be incomplete.

---

## Relationships

```
FlowDefinition 1──* Variable
FlowDefinition 1──* Action
FlowDefinition 1──1 OutputDeclaration
Action 0──1 Condition  (optional conditional execution)
Action 0──1 ScreenshotConfig  (optional diagnostics)
Action(loop) 1──* Action  (nested actions within loops)
Action(loop) 1──1 Condition  (termination condition)
Condition(not) 1──1 Condition  (negation wraps inner condition)
```

## State Transitions

### Flow Execution Lifecycle

```
Loading → Validated → Ready
                        ↓
                    Executing → [per-action: Pending → Running → Completed/Failed]
                        ↓
                  Completed / Failed
```

- **Loading**: JSON file read from disk at startup.
- **Validated**: Schema version checked, required fields verified, action types resolved.
- **Ready**: Definition cached in memory, available for execution.
- **Executing**: Engine walks the action list with an active `IPage` and variable bag.
- **Completed**: All actions executed successfully; output variable returned.
- **Failed**: An action failed; error screenshot captured; exception propagated.

### Action States

- **Pending**: Not yet reached in the sequence.
- **Running**: Currently being executed by its handler.
- **Completed**: Handler returned successfully.
- **Skipped**: Condition evaluated to false; action was bypassed.
- **Failed**: Handler threw an exception.
