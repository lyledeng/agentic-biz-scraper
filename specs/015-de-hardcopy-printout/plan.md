# Implementation Plan: German Hardcopy Printout Retrieval with FAB Translation

**Branch**: `015-de-hardcopy-printout` | **Date**: 2026-03-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/015-de-hardcopy-printout/spec.md`

## Summary

Add a new API endpoint `POST /api/v1/germany-search/current-hardcopy-printout` that retrieves the German "Aktueller Druck" (AD) PDF from the Handelsregister for a specific business registration, sends the base64-encoded PDF to the FAB Generic Document Processor agent for German→English translation (returned as Markdown), converts the Markdown to HTML via Markdig, renders the HTML to PDF via Playwright `page.pdf()`, stores both original and translated PDFs in blob storage, and returns the translated PDF to the caller. The Angular test-ui is enhanced with an "AD" action button on Germany search results. A new `FabDocumentTranslatorOptions` options class provides independent configuration. The feature follows the existing GermanySearch vertical-slice pattern with audit trail, diagnostics, semaphore-based concurrency control, and structured logging.

## Technical Context

**Language/Version**: C# 14 / .NET 10
**Primary Dependencies**: Microsoft.Playwright 1.58.0, LiteBus 4.3.0, Polly 8.6.6, Flurl.Http 4.0.2, Azure.Storage.Blobs 12.24.0, Markdig 1.1.2 (BSD-2-Clause, to be added), System.Text.Json (source-generated)
**Storage**: Azure SQL Managed Instance (audit trail via ADO.NET), Azure Blob Storage (PDFs + diagnostics), local filesystem (JSON flow definitions)
**Testing**: xUnit 2.9.3, NSubstitute 5.3.0, Testcontainers.MsSql 4.11.0, Microsoft.AspNetCore.Mvc.Testing 10.0.5
**Target Platform**: Linux container (Playwright .NET runtime image), AKS deployment via Helm
**Project Type**: Web service (ASP.NET Core Minimal API) + Angular test-ui
**Performance Goals**: End-to-end hardcopy retrieval + translation + PDF generation within 90 seconds
**Constraints**: Single concurrent hardcopy request (semaphore=1); FAB agent timeout configurable (default 60s); Handelsregister AD link is a PrimeFaces form-based PDF download (not page navigation); Markdown translation must preserve document structure
**Scale/Scope**: 1 new vertical slice (GermanySearch/HardcopyPrintout); 1 new FAB integration service; 1 new options class; test-ui Germany search enhancement; Markdig package addition; no Liquibase changes (reuse existing audit table)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | Unit tests for handler, FAB translator service, Markdown→PDF pipeline, endpoint; integration tests for audit persistence; E2E tests via k6 — all Red-Green-Refactor |
| II. SOLID Design | PASS | New feature is self-contained vertical slice (SRP); reuses existing engine/blob/audit (OCP); new `IDocumentTranslator` abstraction (DIP); no speculative features (YAGNI); follows existing patterns (KISS) |
| III. Quality Attributes | PASS | Resilience: Polly retry on FAB agent calls; Reliability: download event handling for PrimeFaces; Supportability: diagnostic screenshots; Auditability: audit trail entry per request; Observability: OTel metrics + structured logging |
| IV. Versioning | PASS | No schema changes; reuse existing audit table; Markdig version pinned in Directory.Packages.props |
| V. Deployment | PASS | No manual steps; JSON flow definitions bundled; Markdig added via NuGet; Helm values unchanged |
| Design Standards | PASS | Source-generated JSON contexts for all new types; typed exceptions; RFC 9457 Problem Details; Options pattern for FAB translator config |
| Quality Gates | PASS | All gates (test, static analysis, security, review, build) apply — no deviations |

**Gate result: PASS** — No violations. Proceeding to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/015-de-hardcopy-printout/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/BizScraper.Api/
├── Features/
│   └── GermanySearch/
│       ├── Endpoints/
│       │   └── GermanySearchEndpoints.cs          # MODIFIED: add POST hardcopy-printout endpoint
│       ├── Commands/
│       │   └── GetHardcopyPrintoutCommand.cs      # NEW: LiteBus command record
│       ├── Handlers/
│       │   ├── SearchGermanyBusinessesHandler.cs   # MODIFIED: map RegistrationId from NormalizedSearchResult
│       │   └── GetHardcopyPrintoutHandler.cs       # NEW: orchestrates scrape→translate→PDF→store→audit
│       ├── Logging/
│       │   └── GermanySearchLog.cs                 # MODIFIED: add hardcopy-specific log messages
│       ├── Metrics/
│       │   └── GermanySearchMetrics.cs             # MODIFIED: add hardcopy-specific counters
│       ├── Models/
│       │   ├── GermanySearchResponse.cs            # EXISTING (unchanged)
│       │   ├── GermanyBusinessResult.cs            # MODIFIED: add RegistrationId field
│       │   ├── HardcopyPrintoutRequest.cs          # NEW: API request body
│       │   └── HardcopyPrintoutResult.cs           # NEW: internal result record
│       └── Queries/
│           └── SearchGermanyBusinessesQuery.cs      # EXISTING (unchanged)
│
├── Common/
│   ├── Configuration/
│   │   ├── FabAgentOptions.cs                      # EXISTING (unchanged)
│   │   └── FabDocumentTranslatorOptions.cs         # NEW: independent config class
│   ├── Interfaces/
│   │   ├── IBlobStorageClient.cs                   # EXISTING (unchanged)
│   │   └── IDocumentTranslator.cs                  # NEW: translate interface
│   └── Serialization/
│       └── AppJsonSerializerContext.cs              # MODIFIED: register new request/response types
│
├── Infrastructure/
│   ├── DocumentTranslation/
│   │   ├── FabDocumentTranslator.cs                # NEW: FAB HTTP call + Polly pipeline
│   │   ├── FabTranslationRequest.cs                # NEW: { input: { format, data } }
│   │   └── FabTranslationResponse.cs               # NEW: { output: { translatedMarkdown, sourceLanguage } }
│   ├── Pdf/
│   │   └── MarkdownToPdfConverter.cs               # NEW: Markdig→HTML→Playwright PDF
│   ├── Scraping/
│   │   └── Definitions/
│   │       └── de-business-search.json             # MODIFIED: extract registration header from span.fontWeightBold
│   └── Storage/
│       └── (no changes)
│
└── Program.cs                                       # MODIFIED: register new services, options, endpoint

src/test-ui/src/app/
├── core/services/
│   └── germany-search.service.ts                    # MODIFIED: add getHardcopyPrintout method
├── features/germany-search/
│   ├── germany-search.component.ts                  # MODIFIED: add AD button handler
│   └── germany-search.component.html                # MODIFIED: add AD button column

tests/
├── BizScraper.UnitTests/
│   └── Features/GermanySearch/
│       ├── GetHardcopyPrintoutHandlerTests.cs       # NEW
│       ├── HardcopyPrintoutEndpointTests.cs         # NEW
│       └── FabDocumentTranslatorTests.cs            # NEW
│   └── Infrastructure/
│       └── Pdf/
│           └── MarkdownToPdfConverterTests.cs       # NEW
│
├── BizScraper.IntegrationTests/
│   └── Features/GermanySearch/
│       └── HardcopyPrintoutIntegrationTests.cs      # NEW
│
└── k6/
    └── germany-hardcopy-printout.js                 # NEW: k6 functional test

deploy/  # NO CHANGES — reuse existing audit table, no Liquibase changelog needed
```

**Structure Decision**: Extends the existing `GermanySearch` vertical slice with hardcopy-specific commands/handlers. New infrastructure folders for `DocumentTranslation` and `Pdf` keep translation and PDF generation separate from the scraping engine.

## Phase 0: Research

**Output**: [research.md](research.md)

All unknowns resolved:

| Topic | Decision | Reference |
|-------|----------|-----------|
| AD link behavior | PrimeFaces form-based PDF download; capture via `page.waitForEvent('download')` | R1 |
| Markdown-to-HTML library | Markdig 1.1.2 (BSD-2-Clause, .NET 10 compatible, pipe tables) | R2 |
| FAB integration pattern | New `FabDocumentTranslator` with independent `FabDocumentTranslatorOptions`; Flurl + Polly | R3 |
| PDF generation | Markdown → HTML (Markdig) → Playwright `page.pdf()` via `SetContentAsync()` | R4 |
| Concurrency | Reuse existing `DE`-keyed `SemaphoreSlim(1, 1)` — search + hardcopy share semaphore | R5 |
| Audit trail | Reuse existing `SearchAuditEntry` table; hardcopy uses `HardcopySuccess`/`HardcopyError` statuses | R6 |
| FAB agent config | Separate markdown doc for manual FAB dashboard setup | R7 |
| Registration ID in search | Enhance extraction JS + `GermanyBusinessResult` to capture `span.fontWeightBold` header | R8 |

**NEEDS CLARIFICATION items**: None — all resolved.

## Phase 1: Design & Contracts

**Outputs**:
- [data-model.md](data-model.md) — Entity definitions, field tables, relationships, state flow
- [quickstart.md](quickstart.md) — Build, run, test, and API usage guide
- [contracts/hardcopy-printout-api.md](contracts/hardcopy-printout-api.md) — POST endpoint contract with all response variants
- [contracts/fab-document-translator-config.md](contracts/fab-document-translator-config.md) — FAB dashboard setup instructions

### Key Design Decisions

1. **Vertical slice extension**: The hardcopy feature extends `Features/GermanySearch/` rather than creating a new top-level feature folder — it shares the same Handelsregister domain, semaphore, metrics prefix, and logging event range.

2. **Command vs Query**: Uses `ICommand<HardcopyPrintoutResult>` (not `IQuery`) because the operation has side effects: downloads a PDF, stores blobs, writes audit entries.

3. **IDocumentTranslator abstraction**: Enables unit testing the handler without hitting the FAB agent. The `FabDocumentTranslator` implementation follows the `FabCaptchaSolver` pattern exactly (Flurl + Polly).

4. **MarkdownToPdfConverter**: Encapsulates the Markdig → HTML → Playwright PDF pipeline. Takes a Playwright `IBrowser` or page factory to avoid coupling to browser lifecycle.

5. **Blob naming convention**: `{correlationId}/de-hardcopy-{slug}-original.pdf` (German) and `{correlationId}/de-hardcopy-{slug}.pdf` (English) — consistent with existing diagnostics folder structure.

6. **Response streaming**: The endpoint streams PDF bytes directly via `Results.File()` — the `HardcopyPrintoutResult` is never serialized to JSON.

7. **Search enhancement prerequisite**: The Germany search extraction must be enhanced to return `RegistrationId` (from `span.fontWeightBold`) so callers have the data needed to invoke the hardcopy endpoint. This modifies `de-business-search.json` (extraction JS), `GermanyBusinessResult` (+1 field), and `SearchGermanyBusinessesHandler` (mapping). This is a prerequisite — must be implemented before the test-ui AD button can work.

## Constitution Re-Check (Post-Design)

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Test-First | PASS | All new types (command, handler, translator, converter) have corresponding test files in project structure |
| II. SOLID Design | PASS | `IDocumentTranslator` provides DIP; vertical slice maintains SRP; no speculative abstractions |
| III. Quality Attributes | PASS | Polly retry for FAB resilience; semaphore for concurrency; audit trail for traceability; diagnostics screenshots |
| IV. Versioning | PASS | Markdig pinned in Directory.Packages.props; no schema changes; additive endpoint only |
| V. Deployment | PASS | No manual steps beyond FAB dashboard config (documented); Markdig via NuGet restore |
| Design Standards | PASS | Source-gen JSON contexts for all new serializable types; Options pattern; RFC 9457 errors |
| Quality Gates | PASS | No deviations required |

**Post-design gate result: PASS** — Design is constitution-compliant.
