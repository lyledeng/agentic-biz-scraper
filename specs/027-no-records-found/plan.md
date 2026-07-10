# Implementation Plan: Show No Records Found in Both UI and API

**Branch**: `027-no-records-found` | **Date**: 2026-04-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/027-no-records-found/spec.md`

## Summary

Add a `resultCount` field to `ExecuteScriptResponse` for business-search requests (omitted for non-search) and introduce distinct UI states in the Angular search component: initial prompt, no-records-found (informational notification), and truncated-empty (truncation warning). Uses existing `JsonIgnoreCondition.WhenWritingNull` convention and Jumpstart `notification-pds3` component.

## Technical Context

**Language/Version**: .NET 10 / C# 14 (API), Angular 17 / TypeScript 5.4 (UI)
**Primary Dependencies**: System.Text.Json source generation (API), @wk/components-v3-angular17 Jumpstart PDS3 (UI)
**Storage**: N/A — no schema changes
**Testing**: xUnit + NSubstitute (unit), Jasmine (Angular component specs)
**Target Platform**: Azure AKS (Linux containers)
**Project Type**: Web API + SPA
**Performance Goals**: No added latency — `resultCount` is derived from array length already in memory
**Constraints**: Additive-only API change; existing consumers must not break
**Scale/Scope**: 3 C# files modified, 3 TypeScript files modified, 2 test files extended

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Unit tests for `resultCount` population in handler, Angular component spec tests for all 4 UI states |
| II. SOLID Design | ✅ PASS | Single property addition to existing response model; UI state machine extended with one signal — no new abstractions |
| III. Quality Attributes | ✅ PASS | Self-Documentability (XML doc-comment on new property), Auditability (no change — audit trail unaffected) |
| IV. Hybrid SemVer+CalVer | ✅ PASS | No schema changes; additive API field — no version bump required |
| V. Hands-Free Deployment | ✅ PASS | No config or infrastructure changes |
| VI. Documentation | ✅ PASS | XML doc-comment on `ResultCount`, current-specs data-model + contract updates |

## Project Structure

### Documentation (this feature)

```text
specs/027-no-records-found/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── execute-script.md  # Updated response contract
└── tasks.md             # Phase 2 output (not created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── BizScraper.Api/
│   └── Features/
│       └── ExecuteScript/
│           ├── Models/
│           │   └── ExecuteScriptResponse.cs      # + ResultCount (int?) with WhenWritingNull
│           └── Handlers/
│               └── ExecuteScriptHandler.cs       # Set ResultCount from UnifiedSearchResult[].Length
│
├── test-ui/
│   └── src/app/
│       ├── features/search/
│       │   ├── search.component.ts               # + hasSearched signal, lastSearchTerm, lastSearchState
│       │   ├── search.component.html             # Split empty-state into 3 conditional blocks
│       │   └── search.component.spec.ts          # + tests for no-records, initial, truncated states
│       └── shared/models/
│           └── execute-script.model.ts           # + resultCount?: number

tests/
├── BizScraper.UnitTests/
│   └── Features/ExecuteScript/
│       └── ExecuteScriptHandlerTests.cs          # + test for resultCount population
```

**Structure Decision**: All changes are modifications to existing files. No new folders or modules required. The feature is a targeted extension of the existing response envelope and UI state machine.

## Complexity Tracking

No constitution violations. No complexity justifications needed.

## Constitution Re-Check (Post-Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | ✅ PASS | Plan includes handler unit test for `resultCount` population, Angular spec tests for all 4 UI states (initial, no-records, results, truncated-empty) |
| II. SOLID | ✅ PASS | Single nullable property on existing record; one boolean signal on existing component. No new abstractions, no SRP violations |
| III. Quality Attributes | ✅ PASS | Self-Documentability (XML doc-comment + `[JsonPropertyName]`), zero performance impact (count from in-memory array), no security surface changes |
| IV. Versioning | ✅ PASS | Additive-only API change; no breaking contract changes; no DB schema changes |
| V. Hands-Free Deployment | ✅ PASS | No config, infrastructure, or Helm changes required |
| VI. Documentation | ✅ PASS | data-model.md, contracts/execute-script.md, quickstart.md, and current-specs updates planned |


