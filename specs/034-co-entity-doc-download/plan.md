# Implementation Plan: CO Entity Certified Document Download

**Branch**: `034-co-entity-doc-download` | **Date**: 2026-04-08 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/034-co-entity-doc-download/spec.md`

## Summary

Enhance the CO entity details scraper to download certified document copies when a "Get certified copies of documents" link is available on the entity details page. The flow navigates through three CO SOS pages (Details → Certified Copies list → Certificate Print), extracts the PDF viewer URL, downloads the combined "all documents" PDF via the browser API request context, uploads to Azure Blob Storage, and returns a proxy URL as `certifiedDocumentsUrl` in the response. The test UI is updated with a download button for the new document type.

## Technical Context

**Language/Version**: .NET 10, C# 14  
**Primary Dependencies**: Playwright (browser automation), Azure Blob Storage SDK, System.Text.Json source generation  
**Storage**: Azure Blob Storage (document PDFs), Azure SQL MI (audit trail)  
**Testing**: xUnit + NSubstitute (unit), Testcontainers (integration)  
**Target Platform**: AKS (Linux containers) + Windows VM (headed proxy)  
**Project Type**: Web service (ASP.NET Core)  
**Constraints**: Session-cookie-dependent PDF download; 30s document download timeout  
**Scale/Scope**: Single definition file change + mapper update + test UI button

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Status | Notes |
|------|--------|-------|
| I. Test-First | PASS | Unit tests for new action handling, mapper update, and output compose changes |
| II. SOLID Design | PASS | Extends existing JSON definition schema; no new abstractions needed |
| III. Quality Attributes | PASS | Error isolation per document; audit trail via existing correlation ID; observability via action logging |
| IV. Versioning | N/A | No schema version bump needed — additive change to existing definition |
| V. Hands-Free Deployment | PASS | No infrastructure changes; existing CI/CD pipeline handles definition file changes |
| VI. Documentation | PASS | JSON definition self-documents actions; quickstart updated |
| Global Spec Snapshot | WILL UPDATE | current-specs/data-model.md updated post-implementation |

## Project Structure

### Documentation (this feature)

```text
specs/034-co-entity-doc-download/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (updated definition JSON)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (files to modify)

```text
src/BizScraper.Api/
├── Infrastructure/Scraping/Definitions/
│   └── us-co-entity-details.json          # Add actions 005-008 + new variable + compose entry
├── Infrastructure/Scraping/Engine/Actions/
│   └── (no new handlers — reuses existing navigate, click, screenshot, extract-href, download-via-api-request patterns)
├── Features/ExecuteScript/Mappers/
│   └── EntityDetailMapper.cs              # Map certifiedDocumentsUrl in MapCoDetails
└── Features/ExecuteScript/Models/
    └── (no model changes — certifiedDocumentsUrl is a string field like certificateUrl)

src/test-ui/
└── src/app/
    └── (entity-details component — add Certified Documents download button)

tests/BizScraper.UnitTests/
├── Infrastructure/Scraping/Engine/
│   └── GenericScriptScraperCoCertDocsTests.cs  # Test ProcessCoCertifiedDocumentsAsync
└── Features/ExecuteScript/Mappers/
    └── EntityDetailMapperTests.cs             # Add CO certifiedDocumentsUrl mapping tests
```

**Structure Decision**: No new files or abstractions required. Changes are additive to existing `us-co-entity-details.json`, the CO mapper in `EntityDetailMapper.cs`, and the test UI entity details component.

## Complexity Tracking

No constitution violations. Feature is an additive extension of existing patterns.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
