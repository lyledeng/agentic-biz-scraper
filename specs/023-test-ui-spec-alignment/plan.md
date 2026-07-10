# Implementation Plan: Test-UI And Spec Consistency Alignment

**Branch**: `023-test-ui-spec-alignment` | **Date**: 2026-04-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/023-test-ui-spec-alignment/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Align canonical execute-script contracts across API docs/spec/tasks/test-ui and resolve prior critical/high/medium consistency findings. The implementation standardizes request shape (`definition` + `parameters`), enforces search/detail slug separation (`us-ia-business-search`, `us-ia-entity-details`), updates test-ui contract usage and error messaging, and ensures constitution-mandated updates to `specs/current-specs/` are included in delivery workflow.

## Technical Context

**Language/Version**: C# 14 / .NET 10 (`net10.0`) for API, TypeScript with Angular for test-ui  
**Primary Dependencies**: ASP.NET minimal APIs, Playwright-backed execution engine, Angular HttpClient, RxJS  
**Storage**: Existing audit trail storage and blob diagnostics (no new persistence model introduced by this feature)  
**Testing**: xUnit + integration tests for API; Angular unit/component tests for test-ui service + pages  
**Target Platform**: API on AKS + browser-based test-ui
**Project Type**: Web API + SPA alignment/documentation consistency feature  
**Performance Goals**: No regression to existing execute-script response behavior; preserve current latency envelope  
**Constraints**: Backward compatibility for CO/WY behavior; canonical contract normalization for new UI/spec flows; constitution snapshot update required  
**Scale/Scope**: Touches execute-script docs/contracts/models/tasks/test-ui paths only; no broad architecture change

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Pre-Design Gate:

- PASS: Test-first remains required by plan/tasks generation.
- PASS: SOLID and separation of concerns preserved by limiting changes to contract boundaries, mappers, validators, and UI service/model layers.
- PASS: Documentation principle addressed with explicit contract and quickstart artifacts.
- PASS: Global Specification Snapshot requirement addressed in this feature requirements and planned outputs.

Post-Design Gate (after Phase 1 artifacts):

- PASS: No new constitutional violations introduced.
- PASS: `specs/current-specs/` update requirement is explicit in spec and quickstart.
- PASS: Design remains incremental and non-disruptive (YAGNI/KISS compliant).

## Project Structure

### Documentation (this feature)

```text
specs/023-test-ui-spec-alignment/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
```text
src/
├── BizScraper.Api/
│   ├── Features/ExecuteScript/
│   │   ├── Endpoints/ExecuteScriptEndpoints.cs
│   │   ├── Models/ExecuteScriptRequest.cs
│   │   ├── Models/ExecuteScriptResponse.cs
│   │   ├── Validation/ExecuteScriptValidator.cs
│   │   ├── Mappers/SearchResultMapper.cs
│   │   └── Mappers/EntityDetailMapper.cs
│   └── Infrastructure/Scraping/Definitions/
│       ├── us-ia-business-search.json
│       └── us-ia-entity-details.json
└── test-ui/
  └── src/app/
    ├── core/services/execute-script.service.ts
    ├── shared/models/execute-script.model.ts
    ├── features/search/search.component.ts
    └── features/entity-details/entity-details.component.ts

tests/
├── BizScraper.UnitTests/Features/ExecuteScript/
└── BizScraper.IntegrationTests/Features/ExecuteScript/
```

**Structure Decision**: Keep existing API + SPA structure; perform targeted contract-alignment changes in execute-script feature and test-ui service/model/components. Ensure plan/task references use runtime-accurate definition path `src/BizScraper.Api/Infrastructure/Scraping/Definitions`.

## Complexity Tracking

No constitutional complexity exceptions are required for this feature.
