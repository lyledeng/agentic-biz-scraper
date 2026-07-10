# Research: Show No Records Found in Both UI and API

**Feature Branch**: `027-no-records-found`
**Date**: 2026-04-03

---

## R1: How to add an optional field to ExecuteScriptResponse

**Decision**: Add `ResultCount` as `int?` with `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`.

**Rationale**: The `FlowDefinitionV2JsonContext` source-generated serializer already uses `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` at the context level. However, the endpoint in `ExecuteScriptEndpoints.cs` serializes the response using a local `CamelCaseOptions` instance (`Results.Json(response, CamelCaseOptions)`), not the source-generated context. The local options do **not** set `DefaultIgnoreCondition`. Therefore, an explicit `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]` attribute on the property is required to ensure the field is omitted when null.

**Alternatives considered**:
- Add `DefaultIgnoreCondition = WhenWritingNull` to the endpoint's `CamelCaseOptions` — rejected because it would change serialization behaviour for all other nullable fields on all response types, potentially breaking existing consumers.
- Use a separate response type for search vs. non-search — rejected per YAGNI; a nullable property with conditional serialization achieves the same result.

---

## R2: Where to compute resultCount in the handler

**Decision**: After `ApplyPostProcessingMapper` returns, check if the result is `UnifiedSearchResult[]` and set `ResultCount = array.Length`. Leave null for all other definition types.

**Rationale**: `ApplyPostProcessingMapper` already differentiates business-search slugs (returns `UnifiedSearchResult[]`) from entity-details slugs (returns `UnifiedEntityDetailResponse`). The type check `data is UnifiedSearchResult[] arr` is safe and aligns with the existing pattern. Computing the count after mapping (not before) ensures it reflects the actual number of records the consumer will see, matching FR-003/FR-004.

**Alternatives considered**:
- Compute count inside `SearchResultMapper.MapToUnified` and return a wrapper — rejected because it would change the mapper's return type and break the existing handler flow.
- Compute count based on `definitionSlug.EndsWith("-business-search")` — rejected because the type check is more robust and self-documenting.

---

## R3: Angular UI state machine for empty states

**Decision**: Add a `hasSearched` signal (boolean, default `false`) to track whether the user has performed at least one search. Track `lastSearchTerm` and `lastSearchState` strings for the message template together with the response `truncated` flag.

**Rationale**: The current template has a single `@if` block for the empty state that cannot distinguish "not yet searched" from "searched with zero results." Adding `hasSearched` (set to `true` in `onSearch()`, never reset) enables three distinct empty-state blocks:
1. `!hasSearched()` → initial prompt ("Enter a business name above...")
2. `hasSearched() && results().length === 0 && !warning()` → no-records-found notification
3. Truncated with zero results → already handled by the existing `warning()` signal path

The `truncated` + zero mapped results edge case (FR-002d) maps to the truncation warning. When the API returns `truncated: true` but `data: []`, the handler already sets `Truncated = true` and `Data = []`. The UI needs to detect this: if `response.truncated && mapped.length === 0`, set `warning()` with the truncation message instead of leaving it in the no-records state.

**Alternatives considered**:
- Use a `searchState` enum signal (`idle | searching | results | empty | error`) — rejected per YAGNI; the existing signal-per-concern pattern (loading, results, error, warning) plus one boolean is simpler.
- Track search count instead of boolean — rejected; only "has searched at least once" matters.

---

## R4: Notification component for no-records-found

**Decision**: Use `<notification-pds3 type="information">` from `@wk/components-v3-angular17` (already imported via `JumpstartComponentsModule`).

**Rationale**: The spec requires an "informational notification (blue/neutral tone)" that is visually distinct from both the dashed-border initial prompt and the warning/error banners. `notification-pds3` with `type="information"` provides this out of the box. The `ErrorBannerComponent` already used in the template wraps `notification-pds3` for error/warning types; the no-records notification should be rendered directly in the template to keep it semantically separate.

**Alternatives considered**:
- Reuse `ErrorBannerComponent` with a new `notificationType="information"` — could work but semantically misleading; "error banner" for a "no results" message.
- Plain styled `<div>` — rejected; Jumpstart PDS3 components are the project standard.

---

## R5: Serialization verification — endpoint uses CamelCaseOptions

**Decision**: The `ResultCount` C# property will serialize as `resultCount` because the endpoint uses `JsonNamingPolicy.CamelCase`. The explicit `[JsonPropertyName("resultCount")]` attribute should be added for consistency with the existing properties which all declare `[JsonPropertyName]`.

**Rationale**: All four existing properties on `ExecuteScriptResponse` use explicit `[JsonPropertyName]` attributes. Adding the same to `ResultCount` maintains consistency and makes the serialized name self-documenting regardless of which serializer options are used.

---

## R6: Impact on existing tests

**Decision**: Existing `ExecuteScriptHandlerTests` already test the response envelope shape. The `HandleAsync_SuccessfulExecution_ReturnsResponseEnvelope` test returns a mock `FlowExecutionResult(new { name = "Acme" }, false)` — this raw object won't be mapped to `UnifiedSearchResult[]` by the real `SearchResultMapper` because it's not a JSON array. Therefore `ResultCount` will be null, which is correct for the test scenario (non-array output). No existing tests need modification — only new tests need to be added.

**Alternatives considered**: None — this is a verification finding, not a decision point.
