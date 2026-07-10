# Implementation Plan: WY Filing History Document Download Fix

**Branch**: `029-wy-history-doc-fix` | **Date**: 2026-04-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/029-wy-history-doc-fix/spec.md`

## Summary

WY filing history documents are downloaded locally during scraping (using session cookies) but never uploaded to Azure Blob Storage — so users receive raw WY SOS URLs they cannot access. The fix adds a generic blob-upload step inside `JsonDrivenEntityDetailScraper` (after the existing local download loop) that uploads each successfully downloaded document and replaces `StorageUrl` with a proxy URL. The existing `/api/v1/documents/{blobPath}` proxy endpoint, `IBlobStorageClient`, `EntityDetailMapper`, and test-ui document viewer already handle everything downstream — only the scraper needs the upload step. Additionally, the test-ui document buttons will be upgraded from custom CSS `doc-open-btn` to Jumpstart `button-field-pds3` components for better visibility and PDS3 compliance.

## Technical Context

**Language/Version**: .NET 10 with C# 14  
**Primary Dependencies**: Playwright (browser context APIRequest), Azure.Storage.Blobs, System.Text.Json  
**Storage**: Azure Blob Storage (existing container), document proxy endpoint (`/api/v1/documents/{blobPath}`)  
**Testing**: xUnit + NSubstitute (unit), Testcontainers (integration)  
**Target Platform**: Azure AKS (Linux containers)  
**Project Type**: Web service + Angular 17 SPA  
**Performance Goals**: Sequential document upload within existing 30s per-document timeout  
**Constraints**: No new dependencies; reuse existing `IBlobStorageClient` and proxy URL patterns  
**Scale/Scope**: Typically 5–20 history documents per WY entity detail request

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for upload logic, integration test for end-to-end flow |
| II. SOLID | PASS | Single responsibility: scraper handles download+upload; no new classes needed |
| III. Quality Attributes | PASS | Resilience via existing Polly pipeline on blob upload; structured logging; metrics unchanged |
| IV. Versioning | PASS | No new versioned artifacts; uses existing API contract |
| V. Deployment | PASS | No infrastructure changes; existing Docker, Helm, CI/CD |
| VI. Documentation | PASS | Doc-comments on new method; quickstart updated |
| Design Standards | PASS | Global spec snapshot updated; existing API contracts unchanged |
| Quality Gates | PASS | All tests, static analysis, security gates apply |

## Project Structure

### Documentation (this feature)

```text
specs/029-wy-history-doc-fix/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (no new contracts — existing proxy endpoint unchanged)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── BizScraper.Api/
│   ├── Infrastructure/
│   │   └── Scraping/
│   │       └── Engine/
│   │           └── JsonDrivenEntityDetailScraper.cs  # MODIFY: add blob upload after local download
│   ├── Features/
│   │   └── ExecuteScript/
│   │       └── Mappers/
│   │           └── EntityDetailMapper.cs             # NO CHANGE: already maps storageUrl → ProxyUrl
│   └── Common/
│       └── Interfaces/
│           └── IBlobStorageClient.cs                 # NO CHANGE: UploadAsync already suitable
├── test-ui/
│   └── src/app/features/entity-details/
│       ├── entity-details.component.html             # MODIFY: replace doc-open-btn with button-field-pds3
│       ├── entity-details.component.css              # MODIFY: remove custom doc-open-btn styles, add Jumpstart overrides
│       ├── entity-details.component.ts               # MODIFY: add Jumpstart imports
│       └── entity-details.component.spec.ts          # MODIFY: update test selectors

tests/
├── BizScraper.UnitTests/
│   └── Infrastructure/Scraping/Engine/
│       └── JsonDrivenEntityDetailScraperTests.cs     # ADD/MODIFY: test upload + proxy URL replacement
└── BizScraper.IntegrationTests/                      # Existing tests cover proxy endpoint
```

**Structure Decision**: No new files or directories in the API. The change is a surgical addition to `JsonDrivenEntityDetailScraper.cs` — adding an upload step inside the existing history document download loop. The test-ui changes are limited to the entity-details component template, styles, and imports.

## Complexity Tracking

No constitution violations. No justified complexity needed.
