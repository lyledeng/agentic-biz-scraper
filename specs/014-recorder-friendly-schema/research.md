# Research: Recorder-Friendly JSON Schema (V2)

**Feature**: 014-recorder-friendly-schema | **Date**: 2026-03-27

## Research Tasks & Findings

### R1: V2 Schema Version Detection Strategy

**Task**: Research how to detect v1 (integer `1`) vs v2 (string `"1.0.0"`) at deserialization time with source-generated `System.Text.Json`.

**Decision**: Use a two-pass deserialization strategy:
1. First pass: Deserialize into a lightweight `SchemaVersionProbe` record with a `JsonElement SchemaVersion` property to inspect the token type.
2. If token is `Number` → deserialize using existing `FlowDefinitionJsonContext.Default.FlowDefinition` (v1 path).
3. If token is `String` → deserialize using new `FlowDefinitionV2JsonContext.Default.FlowDefinitionV2` (v2 path).
4. V1 result is then passed through `V1Normalizer` to produce the v2 internal model.

**Rationale**: Source-generated JSON contexts cannot use polymorphic deserialization. A probe record with `JsonElement` avoids reflection while keeping the detection fast (no full parse until version is known).

**Alternatives considered**:
- Custom `JsonConverter` with `Utf8JsonReader` — Works but breaks source generation and AOT readiness.
- `JsonDocument.Parse` first — Allocates an extra DOM. The probe approach is lighter (single property read).

---

### R2: Multi-Selector Target Resolution Pattern

**Task**: Research the best pattern for resolving multiple selectors with confidence-ranked fallback and per-selector timeout.

**Decision**: Implement `TargetResolver` as a stateless internal service that:
1. Accepts an `ActionTarget` (selectors array sorted by descending confidence) and `IPage`.
2. For each selector, maps `strategy` to the appropriate Playwright locator method:
   - `"css"` → `page.Locator(value)`
   - `"xpath"` → `page.Locator("xpath=" + value)`
   - `"role"` → `page.GetByRole(AriaRole)` (parsed from value)
   - `"name"` → `page.Locator(value)` (CSS attribute selector pattern)
3. Each probe is wrapped in a `Task.WhenAny` with a `Task.Delay(selectorTimeoutMs)` to enforce the 5s (configurable) per-selector timeout.
4. Success = `locator.CountAsync() > 0` within timeout → return the `ILocator`.
5. Failure = all selectors exhausted → throw `TargetResolutionException` listing all attempted selectors.

**Rationale**: Per-selector timeout via `Task.WhenAny` is non-blocking and doesn't require Playwright's built-in timeout (which would wait the full timeout even when the element is simply absent). The 5s default matches user's clarification answer.

**Alternatives considered**:
- Playwright's `Locator.WaitForAsync` with timeout — Throws `TimeoutException`, requires try/catch per attempt. Less clean flow control.
- Parallel probe of all selectors — Wastes resources when the first high-confidence selector matches. Sequential by confidence is more efficient for the common case.

---

### R3: Script Resolution Strategy

**Task**: Research how `scriptRef` resolution should interact with the existing `ExtractActionHandler`.

**Decision**: Implement `ScriptResolver` as a stateless utility that:
1. Accepts a `FlowAction` and the definition's `scripts` dictionary.
2. If `action.ScriptRef` is set → look up the script body from the dictionary and return it.
3. If `action.Javascript` is set → return it directly (backward compatibility for v1-normalized definitions).
4. If neither is set → throw `InvalidOperationException`.
5. Validation at load time ensures `scriptRef` references exist and that `scriptRef` + `javascript` are mutually exclusive.

**Rationale**: Keeps the handler clean — it receives resolved JavaScript regardless of how it was specified. Load-time validation prevents runtime failures.

**Alternatives considered**:
- Embed resolution in `ExtractActionHandler` — Violates SRP. The handler should execute JavaScript, not resolve references.
- Precompile scripts into `FlowAction.Javascript` at load time — Simpler but loses the `scriptRef` metadata for logging/diagnostics.

---

### R4: Compound Condition Evaluation

**Task**: Research how to add `and`/`or` compound conditions to the existing `ConditionEvaluator`.

**Decision**: Extend the existing `Condition` record with a `Conditions` array property (for `and`/`or`). Extend the evaluator's switch expression:
- `"and"` → `Task.WhenAll` on inner conditions, return `all true`.
- `"or"` → Sequential evaluation with short-circuit on first `true`.
- Nesting supported naturally — inner conditions can be `and`, `or`, `not`, or any leaf type.

The `Condition` record changes:
```csharp
public sealed record Condition
{
    public required string Type { get; init; }
    public string? Selector { get; init; }
    public ActionTarget? Target { get; init; }  // NEW: multi-selector in conditions
    public string? Text { get; init; }
    public string? VariableName { get; init; }
    
    [JsonPropertyName("condition")]
    public Condition? InnerCondition { get; init; }  // Existing: for "not"
    
    public IReadOnlyList<Condition>? Conditions { get; init; }  // NEW: for "and"/"or"
}
```

**Rationale**: Minimal change to existing model. The `Conditions` array is only used for `and`/`or` types. The existing `InnerCondition` property continues to serve `not`.

**Alternatives considered**:
- Discriminated union via derived records — C# 14 sealed records don't support inheritance well with source-generated JSON. Flat record with optional properties matches the existing pattern.
- Expression tree pattern — Over-engineered for the boolean logic needed.

---

### R5: V1-to-V2 On-Disk Migration Strategy

**Task**: Research how to mechanically migrate existing v1 files to v2 format.

**Decision**: The `V1Normalizer` class that converts v1 `FlowDefinition` → v2 `FlowDefinitionV2` in-memory will serve dual purposes:
1. **Runtime**: Load-time normalization for backward compatibility during development.
2. **Migration**: A one-time invocation during implementation to produce v2 JSON files written to disk.

Mechanical mapping rules:
- `schemaVersion: 1` (int) → `schemaVersion: "1.0.0"` (string)
- `name/state/endpoint` → `metadata.name/state/endpoint` + generated `metadata.id` (GUID)
- `variables[]` → enriched with `defaultValue: null`, `sensitive: false`, `description: null`
- Flat `selector` on actions → `target: { selectors: [{ strategy: "css", value: selector, confidence: 1.0 }], description: null }`
- Inline `javascript` on extract actions → stays as inline `javascript` (not moved to `scripts` section in mechanical migration)
- `output` → `output` with `schema: null` (no auto-generated schema)
- Actions get `id: "action-NNN"` and `order: N` assigned sequentially
- All existing fields preserved: `condition`, `screenshot`, `loopType`, `maxIterations`, `terminateWhen`, `aggregateInto`, etc.

After migration is verified (all tests pass with v2 files), v1 runtime support is removed.

**Rationale**: Mechanical migration ensures functional equivalence. Manual enhancement (adding multi-selectors, moving scripts to `scripts` section) is a follow-up activity, not required for this feature.

**Alternatives considered**:
- Hand-craft all 5 v2 files — Time-consuming and error-prone. The sample v2 JSON in the spec already demonstrates the enhanced format for Colorado business search.
- Auto-extract inline JS into `scripts` — Possible but complex (need to detect identical scripts across actions, generate unique names). Better done manually per-definition.

---

### R6: Output Schema Validation with System.Text.Json

**Task**: Research how to validate JSON output against an embedded JSON Schema using only `System.Text.Json` (no third-party library).

**Decision**: Implement `OutputSchemaValidator` as a lightweight validator that:
1. Parses the output variable to `JsonElement` via `JsonSerializer.SerializeToElement()`.
2. Walks the `output.schema` `JsonElement` tree alongside the actual output `JsonElement`.
3. Validates:
   - `type` assertions: Check `.ValueKind` matches expected type (`string`→`String`, `number`→`Number`, `boolean`→`True/False`, `array`→`Array`, `object`→`Object`).
   - `properties` on objects: Recurse into each declared property.
   - `items` on arrays: Validate each array element against the `items` schema.
   - `required` on objects: Check that all listed property names exist.
4. Returns a list of `SchemaViolation` records (path, expected type, actual type).
5. Violations are logged as warnings via source-generated `[LoggerMessage]`.

Scope intentionally excludes: `oneOf`, `allOf`, `anyOf`, `$ref`, `pattern`, `minimum`, `maximum`, `enum`, `format`. These are advanced JSON Schema features not needed for the current definitions' output schemas.

**Rationale**: The output schemas in the spec are simple (array of objects with string properties). Full JSON Schema compliance is unnecessary and would require a third-party library. Basic type + required field checks catch the most common extraction errors.

**Alternatives considered**:
- `JsonSchema.Net` (3rd party, MIT) — Full compliance but adds a dependency. User explicitly chose no third-party library.
- Skip runtime validation entirely — Loses the advisory warning capability. The basic validator is low-effort and high-value.

---

### R7: Environment Configuration Application

**Task**: Research how `environment.viewport` and `environment.retries` interact with the existing browser pool and Polly pipeline.

**Decision**:
- **Viewport**: Applied via `page.SetViewportSizeAsync(width, height)` at the start of `ScrapingFlowEngine.ExecuteAsync`, before any actions run. The `environment` config is passed via `ActionContext` so the engine has access.
- **Retries**: NOT integrated into the existing Polly pipeline (which lives in `JsonDrivenSearchScraper`). Instead, implemented as a retry wrapper inside `ScrapingFlowEngine.ExecuteActionsAsync` around individual action handler calls. This keeps per-action retry behavior definition-driven rather than at the infrastructure level.
- **Timeout**: The global `environment.timeout` is applied as the overall execution timeout for the flow. This replaces the middleware-level timeout for individual flow executions.

**Rationale**: Viewport is a per-definition concern (different sites may need different viewport sizes). Retries at the action level (not flow level) match spec assumption A-9.

**Alternatives considered**:
- Apply retries via Polly dynamically — Would need to build a new pipeline per definition. Overkill for simple retry logic.
- Skip viewport if not specified — Correct. When `environment` is null, browser defaults apply (current behavior).

---

### R8: Condition Target Resolution

**Task**: Research how multi-selector targets work inside condition evaluation (FR-017).

**Decision**: The `ConditionEvaluator` currently takes a `string? Selector` from the condition. For v2, conditions also support `Target` (with multi-selector). The evaluator:
1. If `condition.Target` is set → use `TargetResolver` to resolve to a locator.
2. If `condition.Selector` is set → use existing direct locator approach (backward compat for normalized v1).
3. The `TargetResolver` is injected into `ConditionEvaluator` via constructor.

For `element-exists` and `element-visible` condition types, the resolved locator is used directly. The `TargetResolver` is shared between action handlers and the condition evaluator.

**Rationale**: Reuses the same resolution logic everywhere — DRY principle.

---

## Summary of Decisions

| Topic | Decision | Rationale |
|-------|----------|-----------|
| Version detection | Two-pass with `SchemaVersionProbe` | AOT-safe, avoids reflection |
| Target resolution | Sequential by confidence, 5s timeout per probe | Efficient for common case |
| Script resolution | `ScriptResolver` utility, load-time validation | SRP, fail-fast |
| Compound conditions | `and`/`or` with `Conditions` array on `Condition` record | Minimal model change |
| V1 migration | Mechanical via `V1Normalizer`, write to disk once verified | Functional equivalence guaranteed |
| Output validation | `System.Text.Json` tree walk, basic type + required checks | No 3rd-party dependency |
| Environment | Viewport via `page.SetViewportSizeAsync`, action-level retries | Per-definition control |
| Condition targets | `TargetResolver` shared with action handlers | DRY |
