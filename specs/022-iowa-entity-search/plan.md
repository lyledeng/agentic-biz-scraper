# Implementation Plan: 022 - Iowa Business Entity Search

**Branch**: `022-iowa-entity-search` | **Date**: 2026-04-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/022-iowa-entity-search/spec.md`

## Summary

Integrate Iowa business entity search through the unified v2 `/api/v2/execute-script` endpoint using JSON script definitions (`us-ia-business-search`, `us-ia-entity-details`). Reuse existing execute-script orchestration, Playwright automation, diagnostics, and per-state concurrency controls.

## Technical Context

**Language/Version**: .NET 10 with C# 14 (`net10.0`)
**Primary Dependencies**: Playwright, System.Text.Json, OpenTelemetry, Litebus
**Storage**: Azure Blob Storage (diagnostics), Azure SQL Managed Instance (audit)
**Testing**: xUnit, NSubstitute, Testcontainers.NET
**Target Platform**: Azure AKS
**Project Type**: Vertical-slice API service
**Performance Goals**: <= 30s response for search and detail operations
**Constraints**: 1 concurrent IA request; return 503 + Retry-After when exceeded
**Scale/Scope**: Add IA as v2-native state while preserving existing CO/WY behavior

## Constitution Check

- Test-first and measurable acceptance criteria are defined in `spec.md`
- SOLID and vertical slice architecture are preserved
- Existing platform conventions are preserved: file-scoped namespaces, source-generated logging, central package management
- No new constitution violations introduced

## Project Structure

### Documentation (this feature)

```text
specs/022-iowa-entity-search/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│  ├── us-ia-business-search.json
│  └── us-ia-entity-details.json
└── tasks.md
```

### Source Code (repository root)

```text
src/BizScraper.Api/
└── Features/
   └── ExecuteScript/
      ├── ExecuteScriptHandler.cs
      ├── Scripts/
      │  └── ScriptOrchestrator.cs
      └── Models/

src/BizScraper.Api/
└── Infrastructure/
   └── Scraping/
      └── Definitions/
         ├── us-ia-business-search.json
         └── us-ia-entity-details.json

tests/
├── BizScraper.UnitTests/
└── BizScraper.IntegrationTests/
```

**Structure Decision**: Reuse existing execute-script vertical slice and add Iowa definitions/models/tests incrementally with no broad refactor.

## Complexity Tracking

No constitution violations require justification.
