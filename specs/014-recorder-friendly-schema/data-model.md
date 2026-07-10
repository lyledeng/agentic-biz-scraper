# Data Model: Recorder-Friendly JSON Schema (V2)

**Feature**: 014-recorder-friendly-schema | **Date**: 2026-03-27

## Overview

This document defines the v2 internal model records that replace/extend the existing v1 `FlowDefinition` types. After migration, these are the only runtime types. The v1 model is retained temporarily for the `V1Normalizer` input during the transition period, then removed.

## Entity Relationship

```
FlowDefinitionV2
├── FlowMetadata
├── FlowEnvironment
│   ├── ViewportConfig
│   └── RetryConfig
├── VariableV2[]
├── FlowActionV2[]
│   ├── ActionTarget?
│   │   └── SelectorEntry[]
│   ├── ActionMetadata?
│   ├── ScreenshotConfig (existing)
│   ├── ConditionV2?
│   │   ├── ActionTarget? (for element conditions)
│   │   ├── ConditionV2? (InnerCondition for "not")
│   │   └── ConditionV2[]? (Conditions for "and"/"or")
│   └── FlowActionV2[]? (loop sub-actions)
├── ScriptDefinition{} (dictionary)
└── OutputDeclarationV2
    └── JsonElement? (schema)
```

## Record Definitions

### FlowDefinitionV2

The root record for a v2 flow definition. Serves as the single internal model for all definitions (both originally v1 and v2).

```csharp
public sealed record FlowDefinitionV2(
    string SchemaVersion,              // "1.0.0"
    FlowMetadata Metadata,
    FlowEnvironment? Environment,
    IReadOnlyList<VariableV2> Variables,
    IReadOnlyList<FlowActionV2> Actions,
    IReadOnlyDictionary<string, ScriptDefinition>? Scripts,
    OutputDeclarationV2 Output);
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| SchemaVersion | string | Yes | Semver string, e.g. `"1.0.0"` |
| Metadata | FlowMetadata | Yes | Descriptive information |
| Environment | FlowEnvironment? | No | Browser/execution config |
| Variables | IReadOnlyList\<VariableV2\> | Yes | Input/output variable declarations |
| Actions | IReadOnlyList\<FlowActionV2\> | Yes | Ordered list of actions |
| Scripts | IReadOnlyDictionary\<string, ScriptDefinition\>? | No | Named reusable scripts |
| Output | OutputDeclarationV2 | Yes | Output variable declaration |

---

### FlowMetadata

Descriptive information about a flow definition.

```csharp
public sealed record FlowMetadata
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string State { get; init; }
    public required string Endpoint { get; init; }
    public string? CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
    public string? Author { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | string | Yes | Unique definition identifier |
| Name | string | Yes | Human-readable name |
| Description | string? | No | Detailed description |
| State | string | Yes | State code (CO, WY, DE) |
| Endpoint | string | Yes | API endpoint key (business-search, entity-details) |
| CreatedAt | string? | No | ISO-8601 creation timestamp |
| UpdatedAt | string? | No | ISO-8601 last update timestamp |
| Author | string? | No | Definition author |
| Tags | IReadOnlyList\<string\>? | No | Searchable tags |

---

### FlowEnvironment

Browser and execution configuration.

```csharp
public sealed record FlowEnvironment
{
    public ViewportConfig? Viewport { get; init; }
    public string? UserAgent { get; init; }
    public string? Locale { get; init; }
    public int? Timeout { get; init; }
    public int? SelectorTimeoutMs { get; init; }
    public RetryConfig? Retries { get; init; }
}
```

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| Viewport | ViewportConfig? | No | Browser default | Width/height for browser viewport |
| UserAgent | string? | No | null | User agent override (informational) |
| Locale | string? | No | null | Browser locale |
| Timeout | int? | No | 30000 | Global execution timeout (ms) |
| SelectorTimeoutMs | int? | No | 5000 | Per-selector probe timeout (ms) |
| Retries | RetryConfig? | No | null | Per-action retry policy |

---

### ViewportConfig

```csharp
public sealed record ViewportConfig(int Width, int Height);
```

---

### RetryConfig

```csharp
public sealed record RetryConfig(int MaxAttempts, int DelayMs, double BackoffMultiplier);
```

| Field | Type | Description |
|-------|------|-------------|
| MaxAttempts | int | Maximum retry attempts per action |
| DelayMs | int | Initial delay between retries (ms) |
| BackoffMultiplier | double | Multiplier applied to delay after each retry |

---

### VariableV2

An enriched variable declaration.

```csharp
public sealed record VariableV2
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public bool? Required { get; init; }
    public JsonElement? DefaultValue { get; init; }
    public bool? Sensitive { get; init; }
    public string? Description { get; init; }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Name | string | Yes | Variable name |
| Source | string | Yes | `"runtime"`, `"config"`, or `"extracted"` |
| Required | bool? | No | Whether runtime value is required |
| DefaultValue | JsonElement? | No | Fallback value (any JSON type) |
| Sensitive | bool? | No | When true, value is redacted in logs |
| Description | string? | No | Human-readable description |

**Note**: `DefaultValue` uses `JsonElement` to support any JSON primitive (string, number, boolean, null) without boxing. During variable initialization, the engine converts to the appropriate CLR type.

---

### FlowActionV2

The v2 action record, replacing the flat `FlowAction` record. Property-based with all action-type-specific fields as optional (same pattern as v1).

```csharp
public sealed record FlowActionV2
{
    // V2 identity
    public string? Id { get; init; }
    public int? Order { get; init; }
    public required string Type { get; init; }
    public string? Description { get; init; }
    public string? Group { get; init; }
    public ScreenshotConfig? Screenshot { get; init; }
    public ConditionV2? Condition { get; init; }
    public ActionMetadata? Metadata { get; init; }

    // navigate
    public string? Url { get; init; }
    public string? WaitUntil { get; init; }

    // fill / click — v2 target OR v1 flat selector
    public ActionTarget? Target { get; init; }
    public string? Selector { get; init; }
    public string? Value { get; init; }

    // click
    public string? WaitAfter { get; init; }

    // wait-for-load
    public string? State { get; init; }

    // wait-for-condition / extract
    public string? Javascript { get; init; }
    public int? TimeoutMs { get; init; }

    // extract
    public string? StoreAs { get; init; }
    public string? PostProcessor { get; init; }
    public string? ScriptRef { get; init; }

    // check-text
    public string? Pattern { get; init; }
    public bool? IsRegex { get; init; }
    public string? OnMatch { get; init; }
    public string? ErrorType { get; init; }
    public string? ErrorMessage { get; init; }

    // screenshot
    public string? Milestone { get; init; }
    public bool? Force { get; init; }

    // download
    public string? LinkSelector { get; init; }
    public string? ValidateExtension { get; init; }
    public string? FilenamePattern { get; init; }

    // loop
    public string? LoopType { get; init; }
    [JsonConverter(typeof(MaxIterationsConverter))]
    public int? MaxIterations { get; init; }
    public ConditionV2? TerminateWhen { get; init; }
    public IReadOnlyList<FlowActionV2>? Actions { get; init; }
    public string? AggregateInto { get; init; }

    // call-service
    public string? ServiceName { get; init; }
    public string? InputVariable { get; init; }
    public string? OutputVariable { get; init; }
}
```

**Key differences from v1 `FlowAction`**:
- `Id` and `Order` fields for identification and explicit ordering
- `Target` (ActionTarget) alongside legacy `Selector` for multi-selector support
- `ScriptRef` for named script references
- `ErrorMessage` for self-descriptive check-text errors
- `Metadata` (ActionMetadata) for recorder-captured context
- `ConditionV2` replacing `Condition` (supports compound `and`/`or` and multi-selector targets)

---

### ActionTarget

A resilient element targeting specification.

```csharp
public sealed record ActionTarget(
    IReadOnlyList<SelectorEntry> Selectors,
    string? Description);
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Selectors | IReadOnlyList\<SelectorEntry\> | Yes | Ranked selector entries |
| Description | string? | No | Human-readable target description |

---

### SelectorEntry

A single selector strategy with confidence score.

```csharp
public sealed record SelectorEntry(string Strategy, string Value, double Confidence);
```

| Field | Type | Description |
|-------|------|-------------|
| Strategy | string | `"css"`, `"xpath"`, `"role"`, `"name"` |
| Value | string | The selector expression |
| Confidence | double | 0.0–1.0 ranking for resolution order |

---

### ActionMetadata

Recorder-captured context (passthrough only, not executed on).

```csharp
public sealed record ActionMetadata
{
    public string? Timestamp { get; init; }
    public string? PageUrl { get; init; }
    public string? PageTitle { get; init; }
}
```

---

### ConditionV2

Extended condition record supporting compound `and`/`or` and multi-selector targets.

```csharp
public sealed record ConditionV2
{
    public required string Type { get; init; }
    
    // Leaf conditions
    public string? Selector { get; init; }
    public ActionTarget? Target { get; init; }
    public string? Text { get; init; }
    public string? VariableName { get; init; }

    // "not" — single inner condition
    [JsonPropertyName("condition")]
    public ConditionV2? InnerCondition { get; init; }

    // "and" / "or" — array of inner conditions
    public IReadOnlyList<ConditionV2>? Conditions { get; init; }
}
```

| Type | Uses Fields | Semantics |
|------|-------------|-----------|
| `element-exists` | Selector or Target | True if element found on page |
| `element-visible` | Selector or Target | True if element exists and is visible |
| `text-contains` | Text | True if body text contains substring |
| `variable-truthy` | VariableName | True if variable is truthy |
| `variable-falsy` | VariableName | True if variable is falsy |
| `not` | InnerCondition | Negation of inner condition |
| `and` | Conditions | All inner conditions must be true |
| `or` | Conditions | Any inner condition must be true |

---

### ScriptDefinition

A named reusable JavaScript function.

```csharp
public sealed record ScriptDefinition(string Language, string Body, string? Description);
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Language | string | Yes | Currently only `"javascript"` |
| Body | string | Yes | The function body |
| Description | string? | No | Human-readable description of the script |

---

### OutputDeclarationV2

```csharp
public sealed record OutputDeclarationV2
{
    public required string VariableName { get; init; }
    public required string Type { get; init; }
    public JsonElement? Schema { get; init; }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| VariableName | string | Yes | Variable containing the output |
| Type | string | Yes | Type descriptor for documentation |
| Schema | JsonElement? | No | Embedded JSON Schema for advisory validation |

---

### SchemaVersionProbe

Lightweight record for version detection in the `FlowDefinitionLoader`.

```csharp
internal sealed record SchemaVersionProbe(JsonElement SchemaVersion);
```

---

### SchemaViolation

Result record from output schema validation.

```csharp
internal sealed record SchemaViolation(string Path, string Expected, string Actual);
```

---

## New Service Classes

### TargetResolver

```
Internal sealed class.
Injected into: ClickActionHandler, FillActionHandler, ConditionEvaluator, DownloadActionHandler.
Dependencies: ILogger<TargetResolver>.
Method: Task<ILocator> ResolveAsync(ActionTarget target, IPage page, Dictionary<string, object?> variables, int selectorTimeoutMs, CancellationToken ct)
Behavior: Sort selectors by descending confidence. For each selector, resolve variables in value, create locator by strategy, probe with timeout. Return first match. Throw TargetResolutionException if all fail.
```

### ScriptResolver

```
Internal static class (stateless utility).
Method: string Resolve(FlowActionV2 action, IReadOnlyDictionary<string, ScriptDefinition>? scripts)
Behavior: If ScriptRef set, look up in scripts dict, return Body. If Javascript set, return it. Otherwise throw.
```

### V1Normalizer

```
Internal static class (pure function).
Method: FlowDefinitionV2 Normalize(FlowDefinition v1)
Behavior: Map v1 flat fields to v2 model. Generate metadata from name/state/endpoint. Map flat selectors to single-entry ActionTarget. Map v1 Condition to ConditionV2. Preserve all action properties.
```

### OutputSchemaValidator

```
Internal sealed class.
Dependencies: ILogger<OutputSchemaValidator>.
Method: void Validate(object? output, JsonElement? schema, string definitionName)
Behavior: If schema is null, return. Serialize output to JsonElement. Walk schema tree and output tree in parallel. Check type assertions, required fields, array item types. Log warnings for violations.
```

---

## V2 JSON Serialization Context

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    ReadCommentHandling = JsonCommentHandling.Skip)]
[JsonSerializable(typeof(FlowDefinitionV2))]
[JsonSerializable(typeof(FlowActionV2))]
[JsonSerializable(typeof(VariableV2))]
[JsonSerializable(typeof(OutputDeclarationV2))]
[JsonSerializable(typeof(ScreenshotConfig))]
[JsonSerializable(typeof(ConditionV2))]
[JsonSerializable(typeof(FlowMetadata))]
[JsonSerializable(typeof(FlowEnvironment))]
[JsonSerializable(typeof(ViewportConfig))]
[JsonSerializable(typeof(RetryConfig))]
[JsonSerializable(typeof(ActionTarget))]
[JsonSerializable(typeof(SelectorEntry))]
[JsonSerializable(typeof(ScriptDefinition))]
[JsonSerializable(typeof(ActionMetadata))]
[JsonSerializable(typeof(SchemaVersionProbe))]
internal sealed partial class FlowDefinitionV2JsonContext : JsonSerializerContext;
```

---

## V1 → V2 Normalization Mapping

| V1 Field | V2 Field | Mapping |
|----------|----------|---------|
| `SchemaVersion` (int 1) | `SchemaVersion` ("1.0.0") | Constant |
| `Name` | `Metadata.Name` | Direct |
| `State` | `Metadata.State` | Direct |
| `Endpoint` | `Metadata.Endpoint` | Direct |
| — | `Metadata.Id` | Generated GUID |
| `Variables[].Name/Source/Required` | `Variables[].Name/Source/Required` | Direct |
| — | `Variables[].DefaultValue/Sensitive/Description` | null/false/null |
| `Actions[].Selector` | `Actions[].Target` | Single-entry `ActionTarget` with strategy="css", confidence=1.0 |
| `Actions[].Javascript` | `Actions[].Javascript` | Direct (not moved to scripts) |
| `Actions[].Condition` | `Actions[].Condition` | `Condition` → `ConditionV2` (recursive) |
| — | `Actions[].Id` | Generated `"action-NNN"` |
| — | `Actions[].Order` | Sequential index |
| `Output.VariableName/Type` | `Output.VariableName/Type` | Direct |
| — | `Output.Schema` | null |
| — | `Environment` | null |
| — | `Scripts` | null |
