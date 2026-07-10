# Feature Specification: Recorder-Friendly JSON Schema

**Feature Branch**: `014-recorder-friendly-schema`  
**Created**: 2025-07-15  
**Status**: Clarified  
**Input**: User description: "Improve the json definition to be recorder friendly"

## Overview

The current JSON flow definition schema (v1) uses flat selectors, inline JavaScript, and minimal metadata. This makes definitions difficult to generate from browser-based recording tools, hard to maintain, and lacking in self-documentation. This feature evolves the schema to v2 — a recorder-friendly format that supports multi-selector targeting with confidence scores, named reusable scripts, rich metadata, environment configuration, and structured output schemas. The engine must support both v1 and v2 definitions to ensure backward compatibility with all existing flow definitions.

## Clarifications

### Session 2026-03-27

- Q: How should the engine handle both v1 and v2 schemas internally — separate models, superset model, or normalize v1 into v2 at load time? → A: Normalize v1 → v2 at load time. Single internal model, single execution path.
- Q: How should the engine handle per-action `metadata` (timestamp, pageUrl, pageTitle) — populate at runtime, passthrough for diagnostics, or ignore? → A: Recorder-only / passthrough. Deserialized and available for logging/diagnostics but not actively populated or executed on.
- Q: What timeout should apply per selector when resolving a multi-selector target? → A: 5-second probe timeout per selector, configurable at the definition level via `environment.selectorTimeoutMs`.
- Q: Should the engine support an `errorMessage` field on check-text actions with variable interpolation? → A: Yes. The engine resolves variable placeholders in `errorMessage` and includes it in the exception for self-descriptive API error responses.
- Q: What scope for compound conditions — `and`/`or` in ConditionEvaluator only, or also compound conditions on actions? → A: Add `and` and `or` compound condition types to the ConditionEvaluator (e.g., `{ "type": "and", "conditions": [...] }`).
- Q: How to migrate existing v1 JSON files to v2 on disk? → A: Auto-generate v2 files from v1 using the in-memory normalization (mechanical mapping — single CSS selectors become single-entry target, inline JS stays inline), then optionally enhance later.
- Q: Which approach for output schema validation? → A: Use `System.Text.Json` with manual validation against the schema structure (no 3rd-party library). Basic checks: required fields and types.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Engine Executes V2 Definitions (Priority: P1)

As a scraping engine operator, I need the system to load and execute v2 schema flow definitions so that definitions produced by a browser recorder can run without manual conversion.

**Why this priority**: Without engine support for the new schema, none of the other improvements (multi-selectors, scripts section, metadata) can be used in production.

**Independent Test**: Create a v2 flow definition for an existing state (e.g., Colorado business search) and verify the engine produces identical results to the current v1 definition.

**Acceptance Scenarios**:

1. **Given** a v2 flow definition file with `schemaVersion: "1.0.0"`, **When** the engine loads definitions at startup, **Then** the definition is parsed, validated, and registered for execution.
2. **Given** a v2 flow definition with a `metadata` section, **When** the engine processes the definition, **Then** metadata fields (id, name, description, author, tags) are available for logging and diagnostics.
3. **Given** a v2 flow definition with an `environment` section specifying viewport dimensions, **When** the engine executes the flow, **Then** the browser viewport is configured to the specified dimensions.
4. **Given** a v2 flow definition with `environment.retries` configured, **When** an action fails transiently, **Then** the engine retries up to the configured `maxAttempts` with the specified delay and backoff.

---

### User Story 2 - Multi-Selector Resilient Targeting (Priority: P1)

As a definition author, I need actions to support multiple selectors with confidence scores so that if a site's DOM changes, the engine can fall back to alternative selectors without requiring a definition update.

**Why this priority**: Selector fragility is the primary maintenance pain point. Multi-selector targeting is the core "recorder friendly" capability that reduces definition breakage.

**Independent Test**: Create a definition with a `target` containing multiple selectors of varying confidence levels and verify the engine resolves the element correctly, falling back to lower-confidence selectors when higher ones fail.

**Acceptance Scenarios**:

1. **Given** an action with a `target` containing three selectors ranked by confidence, **When** the highest-confidence selector matches an element, **Then** that element is used and lower selectors are not attempted.
2. **Given** an action with a `target` where the highest-confidence selector no longer matches, **When** the engine attempts to resolve the target, **Then** it tries the next selector by confidence until one succeeds.
3. **Given** an action with a `target` where no selector matches, **When** execution reaches that action, **Then** the engine reports a clear failure indicating which selectors were attempted.
4. **Given** a condition (e.g., `terminateWhen`) with a `target` containing multiple selectors, **When** the condition is evaluated, **Then** the engine resolves the target using the same fallback strategy.

---

### User Story 3 - Named Reusable Scripts (Priority: P2)

As a definition author, I need to define JavaScript functions in a centralized `scripts` section and reference them by name from actions, so that common extraction logic is written once and reused across multiple actions.

**Why this priority**: Inline JavaScript in the current schema makes definitions unreadable and causes duplication (e.g., the same extraction script is copy-pasted in first-page and pagination actions). Centralizing scripts significantly reduces definition size and maintenance effort.

**Independent Test**: Create a v2 definition with a `scripts` section containing two named scripts, reference them from extract actions via `scriptRef`, and verify the engine executes the correct script for each action.

**Acceptance Scenarios**:

1. **Given** a `scripts` section with a named script `"extractResults"`, **When** an action references `"scriptRef": "extractResults"`, **Then** the engine executes the script body from the scripts section.
2. **Given** an action with `scriptRef` referencing a non-existent script name, **When** the definition is loaded, **Then** validation fails with a clear error identifying the missing script.
3. **Given** a v2 definition where the same script is referenced by two different actions, **When** both actions execute, **Then** each uses the same script body (no duplication needed in the definition).

---

### User Story 4 - Enriched Variable Definitions (Priority: P2)

As a definition author, I need variables to support default values, sensitivity markers, and descriptions so that definitions are self-documenting and the engine can provide smarter defaults and redact sensitive data from logs.

**Why this priority**: Default values reduce required runtime configuration, sensitivity markers improve security posture for logged/traced data, and descriptions make definitions understandable without external documentation.

**Independent Test**: Create a definition with variables that have `defaultValue`, `sensitive`, and `description` fields and verify the engine applies defaults for missing runtime values and redacts sensitive variables from diagnostic output.

**Acceptance Scenarios**:

1. **Given** a variable with `defaultValue: 25` and `source: "config"`, **When** no config value is provided at runtime, **Then** the engine uses `25` as the variable value.
2. **Given** a variable with `sensitive: true`, **When** the engine logs or traces variable values, **Then** the sensitive variable's value is redacted (e.g., shown as `***`).
3. **Given** a variable with a `description`, **When** the definition is loaded, **Then** the description is available for diagnostic reporting and documentation generation.

---

### User Story 5 - Backward Compatibility with V1 Definitions (Priority: P1)

As a system operator, I need existing v1 definitions to continue working unchanged so that the schema upgrade does not break any currently operational scraping flows.

**Why this priority**: Five production flow definitions currently use v1 schema. Any schema migration must not require rewriting them immediately.

**Independent Test**: Run the full test suite with all existing v1 definitions after deploying v2 engine support and verify identical behavior.

**Acceptance Scenarios**:

1. **Given** the five existing v1 flow definitions, **When** the engine starts up with v2 support enabled, **Then** all v1 definitions load and execute identically to before.
2. **Given** a v1 definition with `schemaVersion: 1` (integer), **When** the engine loads it, **Then** it is treated as v1 and processed using existing logic.
3. **Given** a mix of v1 and v2 definitions in the definitions directory, **When** the engine loads all definitions, **Then** each is parsed according to its own schema version.

---

### User Story 6 - Output Schema Validation (Priority: P3)

As a definition author, I need to define a JSON Schema for the output variable so that the engine can validate extraction results against the expected structure and catch data quality issues early.

**Why this priority**: Output validation catches extraction errors at scrape time rather than downstream, but the system works without it (current v1 has no output validation).

**Independent Test**: Create a v2 definition with an `output.schema` block, extract results that violate the schema, and verify the engine reports a validation warning.

**Acceptance Scenarios**:

1. **Given** a v2 definition with `output.schema` defined, **When** the extraction result conforms to the schema, **Then** execution completes normally.
2. **Given** a v2 definition with `output.schema` defined, **When** the extraction result is missing required fields, **Then** the engine logs a warning with details about the schema violation (execution continues — validation is advisory, not blocking).

---

### User Story 7 - Action Identifiers and Ordering (Priority: P3)

As a definition author, I need actions to have unique `id` and explicit `order` fields so that logs, diagnostics, and error messages can reference specific actions unambiguously.

**Why this priority**: Improves debuggability and makes error messages actionable, but the system functions without explicit IDs (current v1 uses positional indexing).

**Independent Test**: Create a v2 definition with action `id` and `order` fields, trigger an error in a specific action, and verify the error message includes the action ID.

**Acceptance Scenarios**:

1. **Given** a v2 definition with action IDs like `"action-001"`, **When** an action fails, **Then** the error message includes the action ID.
2. **Given** a v2 definition with explicit `order` values, **When** the engine loads the definition, **Then** actions are sorted by `order` before execution (regardless of their position in the JSON array).
3. **Given** a v2 definition with duplicate action IDs, **When** the definition is loaded, **Then** validation fails with a clear error.

---

### Edge Cases

- What happens when a v2 definition has a `target` with zero selectors? — Validation must reject the definition at load time.
- What happens when a v2 definition references a `scriptRef` within a loop's nested actions? — The engine must resolve script references for nested actions identically to top-level actions.
- How does the system handle a `schemaVersion` value that is neither `1` (integer) nor `"1.0.0"` (string)? — The engine must reject the definition with a clear "unsupported version" error.
- What happens when a selector `strategy` is an unrecognized value (e.g., `"ai"`)? — Validation must reject the definition at load time with a clear error identifying the unknown strategy. Only `"css"`, `"xpath"`, `"role"`, and `"name"` are recognized (see FR-014, Assumption A-10).
- What happens when `environment.retries.maxAttempts` is 0? — The engine must not retry; the action fails immediately on first error.
- What happens when a v2 definition has an `output.schema` but the output variable contains valid JSON that doesn't match the schema? — Advisory warning logged, execution completes normally.
- How does the system handle a variable with both `defaultValue` and `required: true` but no runtime value provided? — The default value satisfies the requirement.

## Requirements *(mandatory)*

### Functional Requirements

#### Schema Structure

- **FR-001**: System MUST support a v2 schema identified by `schemaVersion: "1.0.0"` (string) alongside the existing v1 schema identified by `schemaVersion: 1` (integer).
- **FR-002**: V2 definitions MUST include a `metadata` section with at minimum: `id` (unique identifier), `name`, `state`, and `endpoint`.
- **FR-003**: V2 definitions MAY include optional metadata fields: `description`, `createdAt`, `updatedAt`, `author`, and `tags`.
- **FR-004**: V2 definitions MAY include an `environment` section with: `viewport` (width/height), `userAgent`, `locale`, `timeout`, `selectorTimeoutMs`, and `retries` (maxAttempts, delayMs, backoffMultiplier).
- **FR-005**: V2 definitions MAY include a `$schema` field referencing a JSON Schema URI for editor-time validation.

#### Variables

- **FR-006**: V2 variable declarations MUST support `defaultValue` — a fallback value used when no runtime or config value is provided.
- **FR-007**: V2 variable declarations MUST support `sensitive` (boolean) — when true, the engine MUST redact the variable's value in all log output and diagnostic traces.
- **FR-008**: V2 variable declarations MUST support `description` (string) — a human-readable explanation of the variable's purpose.
- **FR-009**: When a variable has `required: true` and a `defaultValue` is provided, the default value MUST satisfy the required constraint if no runtime value is supplied.

#### Actions

- **FR-010**: V2 actions MUST support an `id` field — a unique string identifier within the definition, used in logs and error messages.
- **FR-011**: V2 actions MUST support an `order` field — an integer that determines execution sequence. Actions MUST be sorted by `order` before execution.
- **FR-012**: V2 action `id` values MUST be unique within a definition (including within loop sub-actions). Duplicate IDs MUST cause a validation error at load time.

#### Multi-Selector Targeting

- **FR-013**: V2 actions MUST support a `target` object as an alternative to the flat `selector` property. The `target` MUST contain a `selectors` array and an optional `description`.
- **FR-014**: Each selector in the `target.selectors` array MUST have a `strategy` (e.g., `"css"`, `"xpath"`, `"role"`, `"name"`), a `value`, and a `confidence` score (0.0–1.0).
- **FR-015**: When resolving a `target`, the engine MUST attempt selectors in descending `confidence` order, using the first selector that successfully matches an element.
- **FR-032**: Each individual selector probe MUST be constrained by `environment.selectorTimeoutMs` (default: 5000ms). If a selector does not match within its timeout window, the engine MUST immediately move to the next selector by confidence. The timeout is configurable per definition via the `environment` section.
- **FR-016**: When no selector in a `target` matches (all selectors exhausted within their individual probe timeouts), the engine MUST report a failure listing all attempted selectors and their strategies.
- **FR-017**: The `target` with multi-selector resolution MUST also work within condition definitions (e.g., `terminateWhen` in loops) — not only in actions.
- **FR-018**: V2 actions that currently use flat `selector` MAY continue to use it. The engine MUST treat a flat `selector` as equivalent to a single-entry `target` with `strategy: "css"` and `confidence: 1.0`.

#### Scripts Section

- **FR-019**: V2 definitions MUST support a top-level `scripts` section — a dictionary of named scripts. Each script MUST have `language`, `body`, and an optional `description`.
- **FR-020**: V2 actions MUST support a `scriptRef` property that references a named script from the `scripts` section, as an alternative to inline `javascript`.
- **FR-021**: If an action specifies `scriptRef` referencing a script name that does not exist in the `scripts` section, validation MUST fail at load time with a clear error.
- **FR-022**: An action MUST NOT specify both `scriptRef` and inline `javascript` simultaneously. If both are present, validation MUST fail at load time.

#### Output Schema

- **FR-023**: V2 output declarations MAY include a `schema` object containing a JSON Schema definition that describes the expected structure of the output variable.
- **FR-024**: When `output.schema` is defined, the engine MUST validate the output variable against the schema after execution and log an advisory warning for any violations (execution MUST NOT be blocked). See FR-040 for implementation details.

#### Backward Compatibility

- **FR-025**: During the migration transition period (before FR-037–FR-039 are completed), the engine MUST continue to load and execute v1 definitions (with `schemaVersion: 1`) without any modification to existing definition files. After all v1 files are migrated to v2 and verified, v1 runtime support is removed per FR-039.
- **FR-026**: V1 and v2 definitions MUST coexist in the same definitions directory. The engine MUST detect the schema version and apply the appropriate parsing/validation logic.
- **FR-030**: The engine MUST normalize v1 definitions into the v2 internal model at load time. After normalization, a single execution path processes all definitions regardless of their original schema version. V1 files remain unchanged on disk.

#### Action Metadata

- **FR-031**: V2 actions MAY include a `metadata` object (with fields such as `timestamp`, `pageUrl`, `pageTitle`). The engine MUST deserialize and preserve this data for logging and diagnostic output but MUST NOT actively populate or execute behavior based on its contents. The metadata is recorder-captured context only.

#### Error Messages

- **FR-033**: V2 `check-text` actions MAY include an `errorMessage` field containing a human-readable message template with `${variable}` placeholders. When `onMatch` is `"throw"`, the engine MUST resolve variable placeholders in `errorMessage` and include the resolved message in the `CheckTextMatchException`, making error responses self-descriptive without requiring code changes per definition.

#### Compound Conditions

- **FR-034**: The `ConditionEvaluator` MUST support an `and` condition type containing a `conditions` array. The condition evaluates to `true` only when ALL inner conditions evaluate to `true`.
- **FR-035**: The `ConditionEvaluator` MUST support an `or` condition type containing a `conditions` array. The condition evaluates to `true` when ANY inner condition evaluates to `true`.
- **FR-036**: Compound conditions MUST support nesting — an `and` or `or` condition MAY contain other `and`, `or`, or `not` conditions as children.

#### V1-to-V2 File Migration

- **FR-037**: All five existing v1 JSON definition files MUST be migrated to v2 format on disk using the in-memory normalization logic as the mechanical mapping basis.
- **FR-038**: Migrated v2 files MUST retain functional equivalence — the engine MUST produce identical results with the migrated v2 files as with the original v1 files.
- **FR-039**: After migration, v1 files MUST be removed from the definitions directory. The engine no longer needs to support v1 at runtime once all files are migrated.

#### Output Schema Validation

- **FR-040**: When `output.schema` is defined, the engine MUST validate the output variable against the schema using `System.Text.Json` — no third-party JSON Schema library. Validation covers basic structural checks: required fields, property types (`string`, `number`, `boolean`, `array`, `object`), and array item types.
- **FR-041**: Output schema validation failures MUST be logged as advisory warnings (execution MUST NOT be blocked). The warning MUST include the specific field(s) that failed validation.

#### Environment & Retries

- **FR-027**: When a v2 definition specifies `environment.viewport`, the engine MUST configure the browser viewport to the specified dimensions before executing any actions.
- **FR-028**: When a v2 definition specifies `environment.retries`, the engine MUST retry failed actions up to `maxAttempts` times, with `delayMs` initial delay multiplied by `backoffMultiplier` for each subsequent retry.
- **FR-029**: When `environment.retries` is not specified, the engine MUST NOT retry failed actions (preserving current behavior).

### Key Entities

- **FlowDefinition (v2)**: The root object for a scraping flow. Contains metadata, environment, variables, actions, scripts, and output. Identified by `schemaVersion: "1.0.0"`.
- **Metadata**: Descriptive information about a flow definition — id, name, description, state, endpoint, timestamps, author, tags. Used for cataloging, logging, and diagnostics.
- **Environment**: Browser and execution configuration — viewport, user agent, locale, global timeout, retry policy. Controls how the browser session is set up. 
- **Variable (v2)**: An input/output data slot with extended properties — name, source, required, defaultValue, sensitive, description. Supports runtime, config, and extracted sources.
- **ActionTarget**: A resilient element targeting specification with multiple selectors ranked by confidence. Replaces flat `selector` string. Contains selectors array and description.
- **Selector**: A single element-finding strategy within an ActionTarget — strategy (css, xpath, role, name), value, and confidence score.
- **Script**: A named, reusable JavaScript function defined in the `scripts` section — language, description, body. Referenced by actions via `scriptRef`.
- **OutputSchema**: A JSON Schema definition embedded in the output declaration. Describes the expected structure of extracted data for advisory validation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All five existing v1 flow definitions continue to execute with identical results after the v2 engine upgrade — zero regressions.
- **SC-002**: A new v2 flow definition can be created that produces the same scraping results as its v1 equivalent, validating full feature parity.
- **SC-003**: When a primary selector breaks (simulated by using an invalid CSS selector), the engine successfully falls back to the next selector and completes the scraping flow.
- **SC-004**: Duplicated inline JavaScript is eliminated from flow definitions — scripts referenced via `scriptRef` reduce definition file size by at least 30% compared to equivalent v1 definitions.
- **SC-005**: Sensitive variable values (marked `sensitive: true`) never appear in application logs or diagnostic output.
- **SC-006**: Engine startup fails fast with a clear, actionable error message when a v2 definition has validation issues (missing script reference, duplicate action ID, empty selectors array).
- **SC-007**: When `environment.retries` is configured, transient action failures are recovered automatically without operator intervention.

## Assumptions

- V2 schema version will use semantic versioning strings (e.g., `"1.0.0"`) to distinguish from v1's integer `1`, enabling unambiguous version detection during deserialization.
- The `$schema` URI field is for editor/IDE-time JSON Schema validation only — the engine itself does not fetch or validate against external schema URIs.
- `output.schema` validation is advisory (warnings only) — blocking validation could prevent data delivery for legitimate edge cases where scraped data doesn't perfectly match expectations.
- The `metadata.id` field is generated by the recorder or definition author — the engine does not auto-generate or enforce UUID format, only that it is non-empty.
- The `scripts` section only supports `"javascript"` as a language value in the initial release. Other languages may be added in the future.
- The `environment.userAgent` field is informational for the initial release — the engine may or may not apply it depending on browser automation capabilities.
- Existing v1 definitions will be migrated to v2 format on disk as part of this feature using auto-generated normalization. V1 support can be removed from runtime after migration is verified.
- The `order` field on actions is optional in v2 — when omitted, actions are executed in array order (matching v1 behavior).
- Retry logic applies at the individual action level, not at the flow level — if a retried action exhausts all attempts, the flow fails.
- Only `"css"`, `"xpath"`, `"role"`, and `"name"` are recognized selector strategies in the initial release.
