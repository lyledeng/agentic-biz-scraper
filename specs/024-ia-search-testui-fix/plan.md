# Implementation Plan: 024 - Fix Iowa Search and Test UI

**Branch**: `024-ia-search-testui-fix` | **Date**: 2026-04-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/024-ia-search-testui-fix/spec.md`

## Summary

Fix non-functional Iowa business search and entity details by writing new v2 engine-compatible definition files (the 022 contracts used unsupported action types), with selectors validated against the live Iowa SOS site via native Playwright MCP. Extend C# and TypeScript models with explicit Iowa-specific fields (`chapterCode`, `certificateNote`, `iowaNames`), update `MapIaDetails` to populate those fields, enhance the test UI entity detail view for Iowa rendering, and update the global specification snapshot in `specs/current-specs/`.

## Technical Context

**Language/Version**: .NET 10 with C# 14 (`net10.0`); Angular 17 (test UI)
**Primary Dependencies**: Playwright, System.Text.Json, OpenTelemetry, Litebus, Flurl.Http
**Storage**: Azure Blob Storage (diagnostics/screenshots), Azure SQL Managed Instance (audit)
**Testing**: xUnit, NSubstitute, Testcontainers.NET; Angular testing with Jasmine/Karma
**Target Platform**: Azure AKS
**Project Type**: Vertical-slice API service + Angular 17 test UI
**Performance Goals**: <= 30s response for Iowa search and detail operations
**Constraints**: 1 concurrent IA request; return 503 + Retry-After when exceeded
**Scale/Scope**: Fix existing Iowa definitions and extend models — no new vertical slices, no new endpoints

## Constitution Check

### Pre-Design Gate

- **I. Test-First**: ✅ Unit tests for MapIaDetails model extension; integration tests for end-to-end Iowa search/detail flow with Testcontainers
- **II. SOLID Design**: ✅ Model extension uses nullable properties — open for extension, no modification to existing field semantics; MapIaDetails update is single-responsibility (Iowa mapping only)
- **III. Architectural Quality**: ✅ Diagnostics/screenshots preserved; resilience via Polly; structured logs; fallback selectors for reliability
- **IV. Versioning**: ✅ No API contract version bump needed — additive nullable fields are backward-compatible
- **V. Hands-Free Deployment**: ✅ Definition files deployed via existing CI/CD; no manual steps
- **VI. Documentation**: ✅ JSON definition `description` fields document each action; current-specs updated (FR-013); scraping definitions include metadata headers

### Post-Design Gate

- **I. Test-First**: ✅ data-model.md specifies MapIaDetails mapping changes with before/after; quickstart.md includes verification steps and regression checks
- **II. SOLID Design**: ✅ IowaNameEntry is a single-purpose record; DetailSection extension is additive (nullable fields, no breaking changes); no new abstractions needed
- **III. Architectural Quality**: ✅ research.md confirms all infrastructure reuse; no new failure modes; existing diagnostics/screenshots/concurrency controls apply
- **IV. Versioning**: ✅ No version bump required — additive nullable fields on DetailSection are backward-compatible with existing serialization
- **V. Hands-Free Deployment**: ✅ Definition files are embedded resources; CI builds and deploys them automatically
- **VI. Documentation**: ✅ quickstart.md provides verification guide; data-model.md documents all field semantics; contracts/ contains complete JSON definitions with description fields; current-specs updates scoped in FR-013

### Design Standards

- **Error Handling**: ✅ Existing check-text/throw pattern for Iowa-specific errors preserved in adopted definitions
- **Configuration**: ✅ No new configuration required — definitions are file-based, loaded at startup
- **API Design**: ✅ No endpoint changes — existing v2 execute-script surface reused; additive model changes only
- **Global Specification Snapshot**: ✅ FR-013 requires `specs/current-specs/spec.md` and `data-model.md` updates; scoped in data-model.md section 9-10

## Project Structure

### Documentation (this feature)

```text
specs/024-ia-search-testui-fix/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (validated Iowa definitions)
│   ├── us-ia-business-search.json
│   └── us-ia-entity-details.json
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Common/
│   ├── Models/
│   │   ├── DetailSection.cs             # Add Iowa fields (chapterCode, certificateNote, namesList)
│   │   ├── UnifiedEntityDetailResponse.cs  # No structural change (DetailSection extended)
│   │   └── IowaNameEntry.cs             # NEW: Iowa names list entry record
│   └── Serialization/
│       └── AppJsonSerializerContext.cs   # Register IowaNameEntry
│
├── Features/
│   └── ExecuteScript/
│       └── Mappers/
│           └── EntityDetailMapper.cs    # Update MapIaDetails for new fields
│
└── Infrastructure/
    └── Scraping/
        └── Definitions/
            ├── us-ia-business-search.json   # Replace stub with complete definition
            └── us-ia-entity-details.json    # Replace stub with complete definition

src/test-ui/
└── src/app/
    ├── shared/models/
    │   └── unified-entity.model.ts      # Add Iowa fields to DetailSection interface
    └── features/entity-details/
        └── entity-details.component.ts  # Render Iowa-specific sections
        └── entity-details.component.html # Iowa detail template additions

tests/
├── BizScraper.UnitTests/
│   └── Features/ExecuteScript/
│       └── Mappers/
│           └── EntityDetailMapperIaTests.cs  # Unit tests for updated MapIaDetails
└── BizScraper.IntegrationTests/
    └── Features/ExecuteScript/
        └── IowaExecuteScriptTests.cs    # E2E tests for Iowa search/detail

specs/current-specs/
├── spec.md              # Update with Iowa model extensions
└── data-model.md        # Update with IowaNameEntry, DetailSection Iowa fields
```

**Structure Decision**: Reuse existing execute-script vertical slice, extend models in-place with nullable fields, and replace stub definitions. No new vertical slices, endpoints, or architectural changes.

## Complexity Tracking

No constitution violations require justification.
