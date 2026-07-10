# Implementation Plan: Recorder-Friendly JSON Schema (V2)

**Branch**: `014-recorder-friendly-schema` | **Date**: 2026-03-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/014-recorder-friendly-schema/spec.md`

## Summary

Evolve the JSON flow definition schema from v1 to v2, adding multi-selector targeting with confidence-ranked fallback, named reusable scripts, rich metadata, environment/viewport configuration, enriched variables (defaults, sensitivity, descriptions), compound conditions (`and`/`or`), per-definition retry policies, action `errorMessage` with variable interpolation, and advisory output schema validation. The engine normalizes v1 definitions to v2 internally; once verified, all 5 existing v1 JSON files are mechanically migrated to v2 on disk and v1 runtime support is removed. Output schema validation uses `System.Text.Json` (no third-party library) for basic structural checks.

## Technical Context

**Language/Version**: C# 14 / .NET 10  
**Primary Dependencies**: Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0  
**Storage**: Azure SQL MI (audit trail), Azure Blob Storage (diagnostics), local filesystem (JSON flow definitions as content files)  
**Testing**: xUnit 2.9.3, NSubstitute 5.3.0, Testcontainers.MsSql 4.11.0, Microsoft.AspNetCore.Mvc.Testing 10.0.5  
**Target Platform**: Linux container (Playwright .NET runtime image), AKS via Helm  
**Project Type**: Web service (ASP.NET Core Minimal API)  
**Performance Goals**: <10% overhead vs. current engine; selector resolution within 5s per selector probe  
**Constraints**: AOT-ready (source-generated JSON contexts), no reflection-based serialization, no 3rd-party JSON Schema library  
**Scale/Scope**: 5 flow definitions (CO search, CO entity-details, WY search, WY entity-details, DE search), ~11 action types, 1 execution engine, 1 condition evaluator

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | V2 model types, target resolver, script resolver, condition evaluator extensions, normalization adapter, and output validator each have clear unit test boundaries. Engine integration tests validate full v2 definitions. |
| II. SOLID Design | PASS | SRP: Each new component has exactly one responsibility (TargetResolver resolves selectors, ScriptResolver resolves scriptRefs, V1Normalizer converts v1→v2, OutputSchemaValidator validates output). OCP: New compound condition types added without modifying existing evaluator switch arms. DIP: All new components injectable via abstraction. KISS: No speculative features beyond spec FRs. YAGNI: Only v2 features that the spec requires. |
| III. Quality Attributes | PASS | Reliability: Startup validation catches invalid definitions. Resilience: Multi-selector fallback reduces definition fragility. Security: Sensitive variable redaction. Supportability: Action IDs in error messages. Observability: Per-selector probe logging. |
| IV. Versioning | PASS | Schema moves from integer `1` to semver string `"1.0.0"`. Version detection at deserialization time. |
| V. Deployment | PASS | JSON files bundled as content files. No manual steps. Migration to v2 happens in the codebase. |
| Design Standards | PASS | Source-generated JSON contexts for all new types. Typed errors. All new model types AOT-safe. |
| Quality Gates | PASS | All gates (test, static analysis, security, review, build) apply. |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/014-recorder-friendly-schema/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (v2 JSON schema contract)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Infrastructure/
│   ├── Scraping/
│   │   ├── Engine/
│   │   │   ├── Models/
│   │   │   │   ├── FlowDefinition.cs           # EXISTING: V1 records (retained during migration, removed in Phase 11)
│   │   │   │   ├── FlowDefinitionV2.cs          # NEW: V2 record types (FlowDefinitionV2, FlowActionV2, ConditionV2, etc.)
│   │   │   │   ├── FlowDefinitionJsonContext.cs # EXISTING: V1 JSON context (removed in Phase 11)
│   │   │   │   └── FlowDefinitionV2JsonContext.cs # NEW: V2 source-generated JSON context
│   │   │   ├── Actions/
│   │   │   │   ├── ClickActionHandler.cs        # MODIFIED: TargetResolver integration
│   │   │   │   ├── FillActionHandler.cs         # MODIFIED: TargetResolver integration
│   │   │   │   ├── ExtractActionHandler.cs      # MODIFIED: ScriptResolver integration
│   │   │   │   ├── CheckTextActionHandler.cs    # MODIFIED: errorMessage support
│   │   │   │   └── LoopActionHandler.cs         # MODIFIED: v2 condition/target support
│   │   │   ├── TargetResolver.cs                # NEW: Multi-selector resolution with timeouts
│   │   │   ├── ScriptResolver.cs                # NEW: scriptRef → script body lookup
│   │   │   ├── V1Normalizer.cs                  # NEW: v1 FlowDefinition → v2 normalization
│   │   │   ├── OutputSchemaValidator.cs         # NEW: Advisory JSON schema validation
│   │   │   ├── ConditionEvaluator.cs            # MODIFIED: and/or compound conditions + target
│   │   │   ├── ScrapingFlowEngine.cs            # MODIFIED: v2 environment, action ordering, action IDs in logs
│   │   │   ├── FlowDefinitionLoader.cs          # MODIFIED: Dual-version detection, v2 validation
│   │   │   ├── VariableSubstitution.cs          # MODIFIED: Sensitive variable redaction helper
│   │   │   ├── ActionContext.cs                 # MODIFIED: FlowEnvironment available to handlers
│   │   │   └── EngineLogging.cs                 # MODIFIED: Action ID in log messages
│   │   └── Definitions/                         # MIGRATED: All 5 files rewritten to v2 format
│   │       ├── co-business-search.json          # v2 with scripts section
│   │       ├── co-entity-details.json           # v2 with scripts section
│   │       ├── wy-business-search.json          # v2 with scripts section
│   │       ├── wy-entity-details.json           # v2 with scripts section
│   │       └── de-business-search.json          # v2 with scripts section

tests/
├── BizScraper.UnitTests/
│   └── Infrastructure/Scraping/Engine/
│       ├── TargetResolverTests.cs               # NEW
│       ├── ScriptResolverTests.cs               # NEW
│       ├── V1NormalizerTests.cs                 # NEW
│       ├── OutputSchemaValidatorTests.cs        # NEW
│       ├── ConditionEvaluatorTests.cs           # MODIFIED: and/or tests
│       ├── FlowDefinitionLoaderTests.cs         # MODIFIED: v2 loading tests
│       └── Actions/
│           ├── ClickActionHandlerTests.cs       # MODIFIED: target resolution tests
│           ├── FillActionHandlerTests.cs        # MODIFIED: target resolution tests
│           ├── ExtractActionHandlerTests.cs     # MODIFIED: scriptRef tests
│           └── CheckTextActionHandlerTests.cs   # MODIFIED: errorMessage tests
```

**Structure Decision**: In-place modifications to the existing engine codebase. Four new classes (`TargetResolver`, `ScriptResolver`, `V1Normalizer`, `OutputSchemaValidator`) are added under `Engine/`. Existing action handlers are modified to accept the new target/script resolution. No new projects or folders beyond what already exists.

## Complexity Tracking

No constitution violations to justify.

## Post-Design Constitution Re-check

*Re-evaluated after Phase 1 design artifacts (data-model.md, contracts/, quickstart.md).*

| Principle | Status | Post-Design Notes |
|-----------|--------|-------------------|
| I. Test-First | PASS | V2 model types, TargetResolver, ScriptResolver, V1Normalizer, OutputSchemaValidator, ConditionEvaluator (and/or), and migrated JSON definitions all have clear unit test boundaries. Integration tests validate full v2 flow execution. |
| II. SOLID Design | PASS | SRP: TargetResolver (selector resolution), ScriptResolver (script lookup), V1Normalizer (model mapping), OutputSchemaValidator (schema validation) — each has exactly one responsibility. OCP: New condition types (`and`/`or`) added as new switch arms without modifying existing evaluation logic. DIP: TargetResolver injected into handlers and evaluator. YAGNI: Only v2 features from spec FRs. KISS: Flat action record pattern preserved (not discriminated union). DRY: TargetResolver shared between action handlers and ConditionEvaluator. |
| III. Quality Attributes | PASS | Reliability: Load-time validation catches invalid definitions fast. Resilience: Multi-selector fallback + per-action retries. Security: Sensitive variable redaction in all log output. Supportability: Action IDs in all error messages. Observability: Per-selector probe logging, per-action retry logging, schema violation warnings. |
| IV. Versioning | PASS | Schema version string `"1.0.0"` (semver). Two-pass detection for v1/v2 during migration transition. |
| V. Deployment | PASS | V2 JSON files bundled as content files. No manual deployment steps. Migration happens in source code. |
| Design Standards | PASS | Source-generated `FlowDefinitionV2JsonContext` for all new types. Typed `TargetResolutionException`. All validation rules documented. No 3rd-party JSON Schema library (manual validation with System.Text.Json). |
| Quality Gates | PASS | All gates apply. New unit tests for every new class. Existing integration tests validate backward-compatible behavior with migrated v2 files. |

**Post-design gate result: PASS** — No violations. No complexity tracking entries needed.
